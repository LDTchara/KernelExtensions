using BepInEx.Hacknet;
using Hacknet;
using Hacknet.Gui;
using Hacknet.UIUtils;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace KernelExtensions.Patches
{
    /// <summary>
    /// 条件补丁（手动安装/卸载），仅在 Stuxnet 插件存在时生效。
    /// 完全不引用 Stuxnet_HN.Gamemode 命名空间，通过反射访问。
    /// </summary>
    public class PatchStuxnetDrawFGamemodeMenu
    {
        private const int CONFIRM_BUTTON_ID = 16392804;

        // ---------- SavefileLoginScreen 反射字段 ----------
        private static FieldInfo _answersField;
        private static FieldInfo _isReadyField;
        private static bool _fieldsInit;

        // ---------- GamemodeMenu 反射缓存 ----------
        private static Type _gamemodeMenuType;
        private static PropertyInfo _visibleEntriesProp;
        private static PropertyInfo _stateProp;
        private static MethodInfo _openMenuMethod;
        private static object _disabledStateValue;
        private static bool _gamemodeReflectionReady;

        // ---------- Harmony 控制 ----------
        private static Harmony _harmony;
        private static bool _isPatched;

        // ==================== 反射初始化 ====================

        private static void EnsureFields()
        {
            if (_fieldsInit) return;
            _fieldsInit = true;
            var t = typeof(SavefileLoginScreen);
            var f = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            _answersField = t.GetField("Answers", f);
            _isReadyField = t.GetField("IsReady", f);
        }

        private static void EnsureGamemodeMenuReflection()
        {
            if (_gamemodeReflectionReady) return;
            _gamemodeReflectionReady = true;

            // 在所有已加载程序集中查找 Stuxnet_HN.Gamemode.GamemodeMenu
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                _gamemodeMenuType = asm.GetType("Stuxnet_HN.Gamemode.GamemodeMenu");
                if (_gamemodeMenuType != null) break;
            }

            if (_gamemodeMenuType == null) return;

            var flags = BindingFlags.Public | BindingFlags.Static;
            _visibleEntriesProp = _gamemodeMenuType.GetProperty("VisibleEntries", flags);
            _stateProp = _gamemodeMenuType.GetProperty("State", flags);
            _openMenuMethod = _gamemodeMenuType.GetMethod("OpenMenu", flags, null, Type.EmptyTypes, null);

            // 获取嵌套枚举 GamemodeMenuState 的 Disabled 值
            var stateType = _gamemodeMenuType.GetNestedType("GamemodeMenuState", BindingFlags.Public);
            if (stateType != null)
                _disabledStateValue = Enum.Parse(stateType, "Disabled");
        }

        // ==================== 补丁方法（无 Harmony 特性） ====================

        public static void OnReset(SavefileLoginScreen __instance)
        {
            try
            {
                EnsureFields();
                if (_answersField?.GetValue(__instance) is List<string> answers)
                    answers.Clear();
                __instance.CanReturnEnter = false;
            }
            catch { }
        }

        public static bool OnDraw(SavefileLoginScreen __instance)
        {
            try
            {
                EnsureFields();
                if (_answersField == null || _isReadyField == null) return true;
                if (!(_answersField.GetValue(__instance) is List<string> answers)) return true;

                int count = answers.Count;
                bool isReady = (bool)_isReadyField.GetValue(__instance);

                if (count >= 3 && !isReady)
                {
                    answers.Clear();
                    return true;
                }

                if (count >= 3 && isReady)
                {
                    // ----- 通过反射访问 GamemodeMenu -----
                    EnsureGamemodeMenuReflection();
                    if (_gamemodeMenuType == null) return true;

                    // 获取 VisibleEntries.Count
                    var visibleEntries = _visibleEntriesProp?.GetValue(null);
                    int visibleCount = 0;
                    if (visibleEntries != null)
                    {
                        var countProp = visibleEntries.GetType().GetProperty("Count");
                        if (countProp != null)
                            visibleCount = (int)countProp.GetValue(visibleEntries);
                    }
                    if (visibleCount <= 0) return true;

                    // 检测 Enter 键或透明按钮
                    bool hitEnter = __instance.CanReturnEnter &&
                                    Utils.keyPressed(GuiData.lastInput, Keys.Enter, null);
                    bool btnClicked = Button.doButton(CONFIRM_BUTTON_ID,
                        -100, -100, 10, 10, "", Color.Transparent);

                    if (!(hitEnter || btnClicked)) return true;

                    // 检查 State 是否等于 Disabled
                    if (_stateProp != null && _disabledStateValue != null)
                    {
                        object currentState = _stateProp.GetValue(null);
                        if (currentState != null && currentState.Equals(_disabledStateValue))
                        {
                            _openMenuMethod?.Invoke(null, null);
                        }
                    }
                }
            }
            catch { }
            return true;
        }

        // ==================== 安装 / 卸载（精细控制） ====================

        /// <summary>
        /// 安装补丁（仅当 Stuxnet 插件存在时调用）
        /// </summary>
        public static void Install()
        {
            if (_isPatched) return;

            // 确保 GamemodeMenu 类型存在，避免安装后反射失败
            EnsureGamemodeMenuReflection();
            if (_gamemodeMenuType == null)
            {
                // 可以记录日志，但不安装
                return;
            }

            _harmony = new Harmony("com.LDTchara.KernelExtensions.patchstuxnet");
            var resetMethod = AccessTools.Method(typeof(SavefileLoginScreen), "ResetForNewAccount");
            var drawMethod = AccessTools.Method(typeof(SavefileLoginScreen), "Draw");
            var patchReset = AccessTools.Method(typeof(PatchStuxnetDrawFGamemodeMenu), "OnReset");
            var patchDraw = AccessTools.Method(typeof(PatchStuxnetDrawFGamemodeMenu), "OnDraw");

            _harmony.Patch(resetMethod, postfix: new HarmonyMethod(patchReset));
            _harmony.Patch(drawMethod, prefix: new HarmonyMethod(patchDraw));

            _isPatched = true;
        }

        /// <summary>
        /// 卸载补丁（仅移除本类安装的两个补丁，不影响其他补丁）
        /// </summary>
        public static void Uninstall()
        {
            if (!_isPatched || _harmony == null) return;

            var resetMethod = AccessTools.Method(typeof(SavefileLoginScreen), "ResetForNewAccount");
            var drawMethod = AccessTools.Method(typeof(SavefileLoginScreen), "Draw");

            // 只卸载属于当前 Harmony ID 的补丁
            _harmony.Unpatch(resetMethod, HarmonyPatchType.Postfix, _harmony.Id);
            _harmony.Unpatch(drawMethod, HarmonyPatchType.Prefix, _harmony.Id);

            _isPatched = false;
            _harmony = null;
        }

        // ==================== 条件控制 ====================

        /// <summary>
        /// 静态初始化（在插件加载时调用一次）
        /// 根据 Stuxnet 插件是否存在决定是否安装补丁
        /// </summary>
        public static void Initialize()
        {
            bool pluginExists = HacknetChainloader.Instance?.Plugins?.ContainsKey("autumnrivers.stuxnet") == true;
            if (pluginExists)
                Install();
            else
                Uninstall(); // 确保已卸载
        }
        /*
        /// <summary>
        /// 动态更新补丁状态（如果插件可能热加载/卸载，则定期调用）
        /// </summary>
        public static void UpdatePatchStatus()
        {
            bool pluginExists = HacknetChainloader.Instance?.Plugins?.ContainsKey("autumnrivers.stuxnet") == true;
            if (pluginExists && !_isPatched)
                Install();
            else if (!pluginExists && _isPatched)
                Uninstall();
        }
        */
    }
}