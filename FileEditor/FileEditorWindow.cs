using System;
using System.Collections.Generic;
using System.Text;
using Hacknet;
using ImGuiNET;
using KernelExtensions.Utilities;

namespace KernelExtensions.FileEditor
{
    /// <summary>
    /// 多标签页纯文本编辑器窗体（ImGui）。
    /// 数据来源为"纯文本内容"（不确定长度的字符串），而非磁盘文件——
    /// 外部模块通过 <see cref="OpenTextTab"/> 把一个文本注入为新标签页，
    /// 编辑结果经 <see cref="SaveActive"/> 触发 <see cref="OnSave"/> 回调取回。
    /// 提供：可拖动/可最小化的窗口、多标签页、输入编辑、右上角 Save 按钮、Ctrl+S 快捷键。
    /// </summary>
    public static class FileEditorWindow
    {
        // ================= 数据来源（纯文本，非文件） =================

        /// <summary>保存（Ctrl+S / Save 按钮）触发时由外部注册的回调；参数为标签页索引与编辑后的纯文本。</summary>
        public static Action<int, string> OnSave;

        // ================= 窗体外壳状态 =================
        /// <summary>
        /// 整个窗体是否可见（含最小化）。
        /// 默认隐藏：不再预置任何标签页，窗体由 Editor 程序（#FE#）打开文件时置为可见；
        /// 点击窗体 X 或关闭全部标签页后同样会回到隐藏状态。
        /// </summary>
        public static bool Visible = false;

        /// <summary>当前标签页数量（供诊断日志）。</summary>
        internal static int TabCount => Tabs.Count;

        /// <summary>最小化标记（仅 UI 参考；折叠主逻辑由 ImGui 内置标题栏按钮处理）。</summary>
        public static bool Minimized = false;

        /// <summary>窗口初始位置（可拖动后由 ImGui 记忆）。</summary>
        private static System.Numerics.Vector2 _windowPos = new(80f, 60f);

        // ================= 多标签页数据 =================
        private sealed class Tab
        {
            public string Title;          // 标签显示名
            public string Text;           // 编辑缓冲（纯文本）
            public bool Dirty;            // 是否有未保存修改（标题栏加 * 前缀）
            public object Tag;            // 可选：外部随文本传入的上下文对象
        }

        /// <summary>
        /// 标签页保存上下文：记录文件来源电脑（idName）与从根目录起的完整路径，
        /// 保存时据此把编辑内容写回对应的虚拟文件。
        /// </summary>
        public class FileSaveContext
        {
            /// <summary>来源电脑 idName（如 "playerComp"）。</summary>
            public string ComputerId;

            /// <summary>文件从电脑根目录起的完整路径（如 "home/help.txt"）。</summary>
            public string FilePath;
        }

        private static readonly List<Tab> Tabs = new();
        private static int _activeTab = -1;

        // ================= 关闭确认状态 =================
        // 关闭前如有未保存(Dirty)的文件页，弹出模态确认框。
        // _pendingClose 记录待确认动作：-1 = 无待确认（也用于已取消/已执行后复位）；-2 = 关闭全部；
        // >=0 = 关闭该标签页索引（注意：0 是合法索引，不再充当哨兵值）。
        private static int _pendingClose = -1;
        private static readonly List<int> _pendingDirtyTitles = new(); // 待确认的脏页索引快照

        // 输入框缓冲上限（ImGui ref string 会按此分配；文本内容长度不限，
        // 超此值的输入会被 ImGui 裁剪到该长度内）
        private const uint MaxTextLength = 1_000_000;

        // ================= 每帧绘制 =================

        /// <summary>
        /// 绘制编辑器窗体。应在 <see cref="ImGuiRenderer.BeforeLayout"/> 之后、
        /// <see cref="ImGuiRenderer.AfterLayout"/> 之前调用。
        /// </summary>
        public static void Draw()
        {
            if (!Visible)
            {
                return;
            }

            // 显式 PushFont：正文用 TinyFont、标题栏用 TitleFont（不依赖 io.FontDefault，避免无 CJK 字体渲染成乱码）
            bool tinyPushed = false, titlePushed = false;
            var renderer = FileEditorPatch.Renderer;
            if (renderer != null)
            {
                unsafe
                {
                    tinyPushed = renderer.TinyFont.NativePtr != null;
                    titlePushed = renderer.TitleFont.NativePtr != null;
                }
                if (tinyPushed)
                {
                    ImGui.PushFont(renderer.TinyFont); // 栈底：正文（菜单、标签、输入框）
                }
                if (titlePushed)
                {
                    ImGui.PushFont(renderer.TitleFont); // 栈顶：标题栏
                }
            }

            // 外层窗口：带标题栏（可拖动）、菜单栏；标题栏右侧自带 X（经 ref Visible 关闭）
            ImGui.Begin("File Editor", ref Visible,
                ImGuiWindowFlags.MenuBar);
            if (titlePushed)
            {
                ImGui.PopFont();
            }

            // ---- 菜单栏 ----
            if (ImGui.BeginMenuBar())
            {
                if (ImGui.BeginMenu("File"))
                {
                    if (ImGui.MenuItem("New Tab"))
                        NewTab();

                    if (ImGui.MenuItem("Save", "Ctrl+S"))
                        SaveActive();

                    if (ImGui.MenuItem("Close Editor"))
                        RequestCloseAll();   // 关闭全部（含未保存询问）

                    ImGui.EndMenu();
                }
                ImGui.EndMenuBar();
            }

            // ---- 多标签页栏 ----
            if (ImGui.BeginTabBar("##fileeditor_tabs"))
            {
                for (int i = 0; i < Tabs.Count; i++)
                {
                    var tab = Tabs[i];
                    string label = (tab.Dirty ? "* " : "") + tab.Title;
                    bool open = true;

                    if (ImGui.BeginTabItem(label, ref open))
                    {
                        _activeTab = i;   // 切换当前页

                        // ---- 临时诊断：当前标签页文件名（字体问题排查用，可删除）----
                        ImGui.TextUnformatted($"DBG[{tab.Title}]");
                        // ---- 诊断结束 ----

                        // 多行文本编辑框，填满剩余空间；内容被修改即置脏（标题栏加 * 前缀）
                        if (ImGui.InputTextMultiline(
                            "##editor",
                            ref tab.Text,
                            MaxTextLength,
                            new System.Numerics.Vector2(-1f, -1f)))
                        {
                            tab.Dirty = true;
                        }

                        ImGui.EndTabItem();
                    }

                    if (!open && RequestCloseTab(i))
                    {
                        // 已实际移除（非脏页）：原 i+1 前移到 i，i-- 抵消 for 的 i++ 后仍处理它
                        i--;
                    }
                }

                // "+" 新建页：必须用 TabItemButton（按钮式，点击仅触发一次）。
                // 不能再用 BeginTabItem：它被点击后保持"选中"状态，之后每帧都返回 true → NewTab 无限执行。
                if (ImGui.TabItemButton("+", ImGuiTabItemFlags.Trailing))
                {
                    NewTab();
                }

                ImGui.EndTabBar();
            }

            // ---- 右上角 Save 按钮（TabBar 之后、End 之前，SameLine 排到右侧）----
            ImGui.SameLine(ImGui.GetContentRegionAvail().X - 80f);
            if (ImGui.Button("Save", new System.Numerics.Vector2(70f, 22f)))
            {
                SaveActive();
            }

            ImGui.End();

            if (tinyPushed)
            {
                ImGui.PopFont(); // 与正文 PushFont 配对
            }

            // ---- 关闭确认模态框（在所有标签页绘制后，保证叠在窗口上方）----
            DrawCloseConfirmModal();
        }

        /// <summary>
        /// 关闭前的未保存确认模态框。若无待确认关闭则空转。
        /// </summary>
        private static void DrawCloseConfirmModal()
        {
            // _pendingClose：-1 = 无待确认/已清空；-2 = 关闭全部；>=0 = 关闭该标签页索引
            if (_pendingClose == -1)
            {
                return;
            }

            // 模态框：需放在所有其它窗口之后 Begin 才会置顶（已由 Draw 末尾调用保证）
            bool tinyPushed = false, titlePushed = false;
            var renderer = FileEditorPatch.Renderer;
            if (renderer != null)
            {
                unsafe
                {
                    tinyPushed = renderer.TinyFont.NativePtr != null;
                    titlePushed = renderer.TitleFont.NativePtr != null;
                }
                if (tinyPushed)
                {
                    ImGui.PushFont(renderer.TinyFont);
                }
                if (titlePushed)
                {
                    ImGui.PushFont(renderer.TitleFont);
                }
            }

            if (ImGui.Begin("Unsaved Changes",
                ImGuiWindowFlags.Modal | ImGuiWindowFlags.AlwaysAutoResize))
            {
                if (titlePushed)
                {
                    ImGui.PopFont();
                }
                if (_pendingClose == -2)
                    ImGui.Text("Close all tabs with unsaved changes?");
                else
                    ImGui.Text($"Save \"{TitleOf(_pendingClose)}\" before closing?");

                // 三个动作按钮
                if (ImGui.Button("Save"))
                {
                    bool ok = _pendingClose == -2 ? SaveAll() : SaveTab(_pendingClose);
                    if (ok)
                    {
                        ExecutePendingClose();
                    }
                    // 保存失败：保留确认框，用户可重试或改选 Don't Save
                }
                ImGui.SameLine();
                if (ImGui.Button("Don't Save"))
                {
                    ExecutePendingClose();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                {
                    _pendingClose = -1;
                    _pendingDirtyTitles.Clear();
                }

                ImGui.End();
                if (tinyPushed)
                {
                    ImGui.PopFont(); // 与模态框正文 PushFont 配对
                }
            }
            else
            {
                if (titlePushed)
                {
                    ImGui.PopFont();
                }
                if (tinyPushed)
                {
                    ImGui.PopFont();
                }
            }
        }

        // ================= 快捷键 =================

        /// <summary>
        /// 处理全局快捷键（Ctrl+S 保存当前页）。每帧在 <see cref="Draw"/> 前调用。
        /// </summary>
        public static void HandleShortcuts()
        {
            if (!Visible)
            {
                return;
            }

            // 有关闭确认框弹出时，忽略快捷键，避免误触 Ctrl+S
            // （_pendingClose：-1 = 无待确认/已清空；-2 = 关闭全部；>=0 = 该标签页）
            if (_pendingClose != -1)
            {
                return;
            }

            var io = ImGui.GetIO();
            if (io.KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.S))
            {
                SaveActive();
            }
        }

        // ================= 调取目标文件（纯文本输出） =================

        /// <summary>
        /// 调取指定虚拟文件的内容，输出为不限长度的字符串。
        /// 读取路径与游戏原生 cat 指令一致：
        ///   getCurrentFolder(os) → Folder.searchForFile(name) → FileEntry.data。
        /// </summary>
        /// <param name="os">当前 OS 实例。</param>
        /// <param name="fileName">目标文件名（可带路径，如 "sys/x-server.sys"）。</param>
        /// <param name="clean">true 时用 LocalizedFileLoader.SafeFilterString 清洗
        ///   （把 tinyfont 不支持的字符替换为 '?'，与 cat 显示行为一致）；false 返回原始 data。</param>
        /// <returns>文件内容字符串；找不到返回 null。</returns>
        public static string GetFileContent(OS os, string fileName, bool clean = true)
        {
            if (os == null || string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            try
            {
                Folder folder = Programs.getCurrentFolder(os);
                if (folder == null)
                {
                    return null;
                }

                // 支持 "目录/文件" 相对路径：逐级下钻后取最终文件
                if (fileName.Contains('/'))
                {
                    string[] parts = fileName.Split('/');
                    for (int i = 0; i < parts.Length - 1; i++)
                    {
                        folder = folder.searchForFolder(parts[i]);
                        if (folder == null)
                        {
                            return null;
                        }
                    }
                    fileName = parts[parts.Length - 1];
                }

                FileEntry entry = folder.searchForFile(fileName);
                if (entry == null)
                {
                    return null;
                }

                string data = entry.data;
                if (data == null)
                {
                    return null;
                }

                return clean
                    ? LocalizedFileLoader.SafeFilterString(data)
                    : data;
            }
            catch (Exception)
            {
                // TODO(FileEditor): 记录读取失败 —— 可经 KELog.Error 上报
                // KELog.Error($"[FileEditor] GetFileContent failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 打开文件并附带保存上下文（来源电脑 idName + 从根目录起的完整路径），
        /// 供 Save 按钮 / Ctrl+S 把编辑内容写回目标虚拟文件。
        /// 返回新标签页索引；文件不存在或读取失败返回 -1。
        /// </summary>
        public static int OpenFileInEditorWithContext(OS os, string fileName, bool clean = false)
        {
            if (!TryGetFile(os, fileName, out string content, out FileEntry entry, out string fullPath))
            {
                return -1;
            }

            string title = fileName.Contains('/')
                ? fileName.Substring(fileName.LastIndexOf('/') + 1)
                : fileName;

            var ctx = new FileSaveContext
            {
                ComputerId = os?.thisComputer?.idName ?? "",
                FilePath = fullPath ?? fileName
            };

            return OpenTextTab(title, content, ctx);
        }

        /// <summary>
        /// 解析文件（当前目录 + 可选相对路径），返回内容、FileEntry 引用与从根目录起的完整路径。
        /// </summary>
        private static bool TryGetFile(OS os, string fileName, out string content, out FileEntry entry, out string fullPath)
        {
            content = null;
            entry = null;
            fullPath = null;

            if (os == null || string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            try
            {
                Folder folder = Programs.getCurrentFolder(os);
                if (folder == null)
                {
                    return false;
                }

                if (fileName.Contains('/'))
                {
                    string[] parts = fileName.Split('/');
                    for (int i = 0; i < parts.Length - 1; i++)
                    {
                        folder = folder.searchForFolder(parts[i]);
                        if (folder == null)
                        {
                            return false;
                        }
                    }
                    fileName = parts[parts.Length - 1];
                }

                entry = folder.searchForFile(fileName);
                if (entry == null || entry.data == null)
                {
                    return false;
                }
                content = entry.data;

                // 完整路径：从电脑根目录递归定位该 entry（引用匹配，避免同名文件歧义）
                Computer comp = os.thisComputer;
                if (comp?.files?.root != null)
                {
                    fullPath = FindEntryPath(comp.files.root, "", entry);
                }

                return true;
            }
            catch (Exception ex)
            {
                KELog.Error($"[FileEditor] TryGetFile failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>递归查找 entry 在文件树中的完整路径（根起，"dir/file"）。找不到返回 null。</summary>
        private static string FindEntryPath(Folder folder, string prefix, FileEntry target)
        {
            if (folder == null || target == null)
            {
                return null;
            }

            foreach (FileEntry f in folder.files)
            {
                if (ReferenceEquals(f, target))
                {
                    return string.IsNullOrEmpty(prefix) ? f.name : prefix + "/" + f.name;
                }
            }

            foreach (Folder sub in folder.folders)
            {
                string subPrefix = string.IsNullOrEmpty(prefix) ? sub.name : prefix + "/" + sub.name;
                string res = FindEntryPath(sub, subPrefix, target);
                if (res != null)
                {
                    return res;
                }
            }

            return null;
        }

        /// <summary>
        /// 把目标虚拟文件的内容直接注入为一个新标签页（纯文本，不限长度）。
        /// 返回新标签页索引；文件不存在或读取失败返回 -1。
        /// </summary>
        public static int OpenFileInEditor(OS os, string fileName, bool clean = true)
        {
            string content = GetFileContent(os, fileName, clean);
            if (content == null)
            {
                return -1;
            }

            // 用文件名做标签标题
            string title = fileName.Contains('/')
                ? fileName.Substring(fileName.LastIndexOf('/') + 1)
                : fileName;

            return OpenTextTab(title, content);
        }

        // ================= 对外接口（纯文本来源） =================

        /// <summary>
        /// 把一个纯文本作为新标签页打开。文本长度不限（超 <see cref="MaxTextLength"/> 会被裁剪）。
        /// </summary>
        /// <param name="title">标签显示名。</param>
        /// <param name="content">初始文本内容。</param>
        /// <param name="tag">可选：随文本注入的上下文对象（外部自行取回）。</param>
        /// <returns>新标签页的索引；<c>-1</c> 表示 content 为 null 未创建。</returns>
        public static int OpenTextTab(string title, string content, object tag = null)
        {
            if (content == null)
            {
                return -1;
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                title = $"tab{Tabs.Count + 1}";
            }

            Tabs.Add(new Tab
            {
                Title = title,
                Text = content,
                Dirty = false,
                Tag = tag
            });
            _activeTab = Tabs.Count - 1;

            return _activeTab;
        }

        /// <summary>
        /// 替换当前页的编辑缓冲。不影响脏标记（由外部视情况置位）。
        /// </summary>
        public static void SetActiveTabText(string content)
        {
            var tab = ActiveTab();
            if (tab == null || content == null)
            {
                return;
            }
            tab.Text = content;
        }

        /// <summary>
        /// 手动置/清当前页的脏标记（有未保存修改时标题栏显示 * 前缀）。
        /// </summary>
        public static void SetActiveTabDirty(bool dirty)
        {
            var tab = ActiveTab();
            if (tab == null)
            {
                return;
            }
            tab.Dirty = dirty;
        }

        /// <summary>
        /// 读取当前页的编辑缓冲。
        /// </summary>
        public static string GetActiveTabText()
        {
            return ActiveTab()?.Text;
        }

        /// <summary>
        /// 读取当前页的 Tag 上下文对象（外部注入时使用）。
        /// </summary>
        public static object GetActiveTabTag()
        {
            return ActiveTab()?.Tag;
        }

        // ================= 内部 =================

        /// <summary>
        /// 请求关闭单个标签页。
        /// </summary>
        /// <returns>true = 已直接移除（无可保存内容的页）；false = 脏文件页已弹确认框（未移除）。</returns>
        private static bool RequestCloseTab(int index)
        {
            if (index < 0 || index >= Tabs.Count)
            {
                return false;
            }

            var tab = Tabs[index];
            // 只有“有写回目标(FileSaveContext)且被改过的文件页”需要弹未保存确认；
            // 新建的空白页/草稿页没有可保存的内容，点 X 直接关（含索引 0——_pendingClose 哨兵只用 -1，
            // 不再用 0 表示“已清空”，避免关闭第 0 个标签时被短路而无法关闭）。
            bool needsConfirm = tab.Dirty && tab.Tag is FileSaveContext;
            if (needsConfirm)
            {
                _pendingClose = index;      // 置待确认
                _pendingDirtyTitles.Clear();
                _pendingDirtyTitles.Add(index);
                return false;
            }
            else
            {
                RemoveTab(index);            // 无未保存内容：直接关
                return true;
            }
        }

        /// <summary>
        /// 请求关闭所有标签页。若有任一页未保存，弹确认框；否则直接全部关闭。
        /// </summary>
        private static void RequestCloseAll()
        {
            bool anyDirty = false;
            _pendingDirtyTitles.Clear();
            for (int i = 0; i < Tabs.Count; i++)
            {
                // 仅“有写回目标的脏文件页”需要确认（空白/草稿页无可保存内容，直接随全关清掉）
                if (Tabs[i].Dirty && Tabs[i].Tag is FileSaveContext)
                {
                    anyDirty = true;
                    _pendingDirtyTitles.Add(i);
                }
            }

            if (anyDirty)
            {
                _pendingClose = -2;         // -2 = 关闭全部（有待确认脏页）
            }
            else
            {
                Tabs.Clear();
                _activeTab = -1;
                Visible = false;             // 全部关完则隐藏窗体
            }
        }

        /// <summary>
        /// 保存指定标签页：若有保存上下文（来源电脑 + 路径）则写回目标虚拟文件。
        /// </summary>
        /// <returns>true = 已保存成功（或无可写回内容的无目标页）；false = 写回失败（脏标记保留）。</returns>
        private static bool SaveTab(int index)
        {
            if (index < 0 || index >= Tabs.Count)
            {
                return false;
            }

            var tab = Tabs[index];
            if (tab.Tag is FileSaveContext ctx)
            {
                if (!WriteBackFile(ctx, tab.Text))
                {
                    KELog.Error($"[FileEditor] save failed: '{tab.Title}' -> {ctx.ComputerId}:{ctx.FilePath}");
                    return false; // 保留脏标记，调用方不应继续关闭
                }
                KELog.Info($"[FileEditor] saved: '{tab.Title}' -> {ctx.ComputerId}:{ctx.FilePath}");
                OnSave?.Invoke(index, tab.Text);
                tab.Dirty = false;
                return true;
            }

            // 无目标文件（新建页）：无内容可写回，直接视为丢弃
            tab.Dirty = false;
            return true;
        }

        /// <summary>
        /// 保存所有脏页（关闭全部前的 Save 动作）。全部成功返回 true。
        /// </summary>
        private static bool SaveAll()
        {
            foreach (int idx in _pendingDirtyTitles)
            {
                if (!SaveTab(idx))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 把内容写回目标电脑上的虚拟文件：按 idName 找电脑、按完整路径从根目录定位文件并替换 data。
        /// </summary>
        private static bool WriteBackFile(FileSaveContext ctx, string content)
        {
            if (ctx == null || string.IsNullOrEmpty(ctx.ComputerId) || string.IsNullOrEmpty(ctx.FilePath))
            {
                return false;
            }

            try
            {
                var os = OS.currentInstance;
                if (os == null)
                {
                    return false;
                }

                Computer comp = Programs.getComputer(os, ctx.ComputerId);
                Folder root = comp?.files?.root;
                if (root == null)
                {
                    return false;
                }

                Folder folder = root;
                string[] parts = ctx.FilePath.Split('/');
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    folder = folder.searchForFolder(parts[i]);
                    if (folder == null)
                    {
                        return false;
                    }
                }

                FileEntry entry = folder.searchForFile(parts[parts.Length - 1]);
                if (entry == null)
                {
                    return false;
                }

                entry.data = content;
                return true;
            }
            catch (Exception ex)
            {
                KELog.Error($"[FileEditor] WriteBackFile failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 执行待确认的关闭动作（确认框内 Save / Don't Save 后调用）。
        /// </summary>
        private static void ExecutePendingClose()
        {
            if (_pendingClose == -2)
            {
                Tabs.Clear();
                _activeTab = -1;
                Visible = false;
            }
            else if (_pendingClose >= 0)
            {
                RemoveTab(_pendingClose);
            }

            _pendingClose = -1;
            _pendingDirtyTitles.Clear();
        }

        /// <summary>
        /// 取标签页标题（用于确认框文案）。越界时回退为 "unknown"。
        /// </summary>
        private static string TitleOf(int index)
        {
            if (index < 0 || index >= Tabs.Count)
            {
                return "unknown";
            }
            return Tabs[index].Title;
        }

        private static void NewTab()
        {
            Tabs.Add(new Tab
            {
                Title = $"untitled{Tabs.Count + 1}",
                Text = "",
                Dirty = true
            });
            _activeTab = Tabs.Count - 1;
        }

        private static void RemoveTab(int index)
        {
            if (index < 0 || index >= Tabs.Count)
            {
                return;
            }

            Tabs.RemoveAt(index);

            if (_activeTab >= Tabs.Count)
            {
                _activeTab = Tabs.Count - 1;
            }
        }

        private static Tab ActiveTab()
        {
            if (_activeTab < 0 || _activeTab >= Tabs.Count)
            {
                return null;
            }
            return Tabs[_activeTab];
        }

        /// <summary>
        /// 保存当前页：有保存上下文则写回目标文件，成功后清脏标记并触发 <see cref="OnSave"/> 回调。
        /// </summary>
        private static void SaveActive()
        {
            if (_activeTab < 0 || _activeTab >= Tabs.Count)
            {
                return;
            }

            SaveTab(_activeTab);
        }
    }
}