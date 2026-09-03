using System.Reflection;
using System.Runtime.InteropServices;
using Hacknet;
using Hacknet.Extensions;
using Hacknet.Screens;
using HarmonyLib;
using ImGuiNET;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace KernelExtensions.FileEditor
{
    /// <summary>
    /// FileEditor 挂载点：负责创建 ImGui 单例并在每帧驱动其更新/绘制。
    ///
    /// 机制（参照 HacknetThemeEditor 已验证方案）：
    ///  - 创建：Patch OS.LoadContent（每个游戏会话创建 OS 时必然调用一次，创建 ImGuiRenderer 单例）。
    ///    ※ 不能挂 MainMenu.LoadContent：本模组是作为 Hacknet 扩展加载的，扩展在"主菜单显示之后"
    ///      才由 ExtensionLoader 加载，MainMenu.LoadContent 早已执行完毕，钩子永远不触发
    ///      → Renderer 恒为 null → 指令正常输出但窗体不出现、无任何报错。
    ///  - 驱动：Patch OS.drawScanlines（游戏所有绘制路径的统一末尾钩子，每帧必然调用），
    ///    在其 Prefix 里执行 BeforeLayout → HandleShortcuts → Draw → AfterLayout。
    /// </summary>
    [HarmonyPatch]
    public static class FileEditorPatch
    {
        /// <summary>ImGui 渲染器单例（一次创建、全程复用）。</summary>
        internal static ImGuiRenderer Renderer;

        /// <summary>动态补丁（TextInputHook 守卫）使用的 Harmony 实例。</summary>
        private static Harmony _harmony;

        /// <summary>kernel32.LoadLibrary：预加载插件目录下的原生 cimgui.dll（P/Invoke 不搜扩展目录）。</summary>
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        /// <summary>ImGui 渲染器内部读取键盘的窗口（此时放行真实状态，否则 ImGui 自己拿不到按键）。</summary>
        internal static bool ImGuiReadingKeyboard;

        /// <summary>ImGui 当前需要独占键盘（窗体聚焦 / 活动输入框）。</summary>
        internal static bool ImGuiWantsKeyboard()
        {
            return Renderer != null && ImGui.GetIO().WantCaptureKeyboard;
        }

        /// <summary>
        /// Hacknet 侧键盘屏蔽：ImGui 捕获键盘期间，游戏所有 Keyboard.GetState() 读取返回空状态——
        /// 终端字符、热键、滚动等 Hacknet 输入全部暂时失效；ImGui 内部读取（ImGuiReadingKeyboard）放行真实状态。
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Keyboard), nameof(Keyboard.GetState), new Type[] { })] // 显式空参数：避免与 GetState(PlayerIndex) 重载歧义
        static bool KeyboardGetStatePrefix(ref KeyboardState __result)
        {
            if (ImGuiReadingKeyboard || !ImGuiWantsKeyboard())
            {
                return true; // ImGui 内部读取、或 ImGui 未捕获键盘：放行
            }
            __result = new KeyboardState(); // Hacknet 侧读取：屏蔽
            return false;
        }

        /// <summary>
        /// Hacknet 侧键盘屏蔽（带 PlayerIndex 重载）：Hacknet.InputState.Update 每帧用此重载轮询键盘
        /// （GuiData.getKeyboadState() 返回 InputState.CurrentKeyboardStates，终端/全局键都从这里来），
        /// 无参重载的屏蔽覆盖不到它——必须一并屏蔽，否则编辑器聚焦时游戏仍会响应方向键/回车等输入。
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Keyboard), nameof(Keyboard.GetState), new Type[] { typeof(PlayerIndex) })]
        static bool KeyboardGetStatePlayerIndexPrefix(ref KeyboardState __result)
        {
            if (ImGuiReadingKeyboard || !ImGuiWantsKeyboard())
            {
                return true;
            }
            __result = new KeyboardState();
            return false;
        }

        // ================= 创建单例 =================

        /// <summary>
        /// 游戏会话创建时初始化 ImGui 单例并重建字体图集。幂等（仅首次生效）。
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(OS), nameof(OS.LoadContent))]
        static void CreateRenderer()
        {
            if (Renderer != null)
            {
                return;
            }

            // P/Invoke 解析原生 DLL 只按 游戏根目录/CWD/System32/PATH 搜索，扩展的 Plugins 目录不在其中，
            // 必须先按完整路径 LoadLibrary 预加载 cimgui.dll。任何失败只停用编辑器，不炸游戏。
            try
            {
                if (!PreloadCImgui())
                {
                    return; // 具体原因已在日志中输出
                }
            }
            catch (Exception ex)
            {
                KELog.Error($"[FileEditor] cimgui preload failed: {ex.Message}");
                return;
            }

            var graphicsDevice = GuiData.spriteBatch.GraphicsDevice;

            Renderer = new ImGuiRenderer(Game1.singleton);
            Renderer.RebuildFontAtlas();

            // Hacknet 文本输入守卫：须在游戏会话内（类型已随主程序集加载）再动态补丁。
            HacknetTextInputGuard.Apply(_harmony ??= new Harmony("com.LDTchara.KernelExtensions.FileEditor"));

            // 不再预置默认空白标签页：FileEditorWindow.Visible 初始为 false，
            // 仅当运行 Editor 程序（#FE#）打开文件时才显示窗体。

            KELog.Info("[FileEditor] ImGui renderer created.");
        }

        /// <summary>
        /// 按候选目录查找并预加载 cimgui.dll。成功返回 true；失败输出日志并返回 false。
        /// 注意：BepInEx/Chainloader 可能用字节数组加载插件，Assembly.Location 为空字符串，
        /// 必须先判空再取目录（直接 Path.GetDirectoryName("") 会抛 ArgumentException）。
        /// </summary>
        private static bool PreloadCImgui()
        {
            var candidates = new List<string>();

            string asmPath = Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(asmPath))
            {
                try { candidates.Add(Path.GetDirectoryName(asmPath)); } catch { }
            }

            string codeBase = Assembly.GetExecutingAssembly().CodeBase;
            if (!string.IsNullOrEmpty(codeBase) && codeBase.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                try { candidates.Add(Path.GetDirectoryName(new Uri(codeBase).LocalPath)); } catch { }
            }

            if (ExtensionLoader.ActiveExtensionInfo != null)
            {
                candidates.Add(Path.Combine(ExtensionLoader.ActiveExtensionInfo.FolderPath, "Plugins"));
            }
            candidates.Add(AppDomain.CurrentDomain.BaseDirectory); // 兜底：游戏根目录

            string dir = candidates
                .Where(d => !string.IsNullOrEmpty(d))
                .FirstOrDefault(d => File.Exists(Path.Combine(d, "cimgui.dll")));
            if (dir == null)
            {
                KELog.Error($"[FileEditor] cimgui.dll NOT found in: {string.Join(" | ", candidates.Where(d => !string.IsNullOrEmpty(d)))} — deploy it into the Plugins folder beside KernelExtensions.dll");
                return false;
            }

            string cimguiPath = Path.Combine(dir, "cimgui.dll");
            IntPtr handle = LoadLibrary(cimguiPath);
            if (handle == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                KELog.Error($"[FileEditor] cimgui.dll found but failed to load (Win32 0x{err:X8}): {cimguiPath} — missing VC++ runtime dependency?");
                return false;
            }

            KELog.Info($"[FileEditor] cimgui.dll preloaded from: {cimguiPath}");
            return true;
        }

        // ================= 每帧驱动 =================

        /// <summary>
        /// 每帧驱动 ImGui。返回 true 放行原 drawScanlines 继续执行。
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(OS), "drawScanlines")]
        static bool DriveImGui(OS __instance)
        {
            // 渲染器尚未创建（理论上不会，因主菜单先加载）则直接放行
            if (Renderer == null)
            {
                return true;
            }

            // 帧时钟来源：OS.Update 每帧填充的 lastGameTime
            GameTime gameTime = __instance.lastGameTime ?? new GameTime();

            try
            {
                Renderer.BeforeLayout(gameTime);
                FileEditorWindow.HandleShortcuts();
                FileEditorWindow.Draw();
                Renderer.AfterLayout();
            }
            catch (Exception ex)
            {
                // 渲染异常不炸游戏（如字体图集无效导致绘制抛错），仅记录
                KELog.Error($"[FileEditor] drive exception: {ex}");
            }

            return true;
        }
    }

    /// <summary>
    /// Hacknet 文本输入守卫（手动补丁，不经 PatchAll）。
    /// 目标：Hacknet.Input.TextInputHook.OnTextInput(char)——Hacknet 5.069 所有文本控件
    /// （终端命令行、TextBox、登录框）的字符统一经此回调写入 TextInputHook.buffer。
    /// ImGui 编辑器捕获键盘（InputText 聚焦）期间跳过回调，游戏侧文本控件不再收到任何字符。
    /// </summary>
    internal static class HacknetTextInputGuard
    {
        /// <summary>补丁是否已安装（CreateRenderer 每次会话都执行，只装一次）。</summary>
        internal static bool Applied;

        internal static void Apply(Harmony harmony)
        {
            if (Applied)
            {
                return;
            }
            try
            {
                MethodBase target = AccessTools.Method("Hacknet.Input.TextInputHook:OnTextInput");
                if (target == null)
                {
                    // 运行时 Hacknet 无此类型（版本不符）时降级：仅键盘轮询屏蔽仍生效
                    KELog.Error("[FileEditor] TextInputHook.OnTextInput not found — 文本事件通道互斥不可用（仅键盘轮询被屏蔽）");
                    Applied = true;
                    return;
                }
                harmony.Patch(target, prefix: new HarmonyMethod(typeof(HacknetTextInputGuard), nameof(OnTextInputPrefix)));
                Applied = true;
            }
            catch (Exception ex)
            {
                KELog.Error($"[FileEditor] failed to patch TextInputHook guard: {ex.Message}");
            }
        }

        /// <summary>prefix：ImGui 捕获键盘时跳过游戏侧文本回调。返回 true = 放行原方法。</summary>
        private static bool OnTextInputPrefix(char c)
        {
            return !FileEditorPatch.ImGuiWantsKeyboard();
        }
    }
}