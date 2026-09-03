using Hacknet;
using Hacknet.Gui;
using HarmonyLib;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace KernelExtensions.Patches
{
    /// <summary>
    /// ThemeChanger 界面改造：把原版 Remote / Local Theme Files 两个列表改为 Static / Dynamic 两个栏，
    /// 每栏带右侧滚动条。
    ///   - Static   ：合并原版解析逻辑的结果（当前目录 + ~/sys + ~/home 中可解析为有效主题的文件）。
    ///   - Dynamic  ：动态壁纸列表（解析逻辑暂时留空，后续实现）。
    /// 底部保留 Apply 区（选中文件 → 备份 x-server.sys → 写入 → switchTheme）。
    /// ThemeChangerExe 为游戏 internal 类，编译期不可引用，故用 TargetMethod 动态定位 + 反射读写字段。
    /// </summary>
    [HarmonyPatch]
    internal static class ThemeChangerUIPatch
    {
        // 选中 / 滚动状态（替换原 private 字段：原 DrawListing 被跳过，这些状态由本地静态管理）
        private static int _staticSelected = -1;
        private static int _dynamicSelected = -1;
        private static int _staticScroll = 0;
        private static int _dynamicScroll = 0;

        // 反射缓存
        private static Type _tcType;
        private static FieldInfo _fBounds;
        private static FieldInfo _fOs;
        private static FieldInfo _fPid;
        private static FieldInfo _fIsExiting;

        internal static MethodBase TargetMethod() => AccessTools.Method("Hacknet.ThemeChangerExe:DrawListing");

        [HarmonyPrefix]
        private static bool DrawListingPrefix(object __instance, Rectangle dest, SpriteBatch sb)
        {
            if (!EnsureFields(__instance))
            {
                return true; // 反射失败：放行原方法
            }

            Rectangle bounds = (Rectangle)_fBounds.GetValue(__instance);
            OS os = (OS)_fOs.GetValue(__instance);
            int pid = (int)_fPid.GetValue(__instance);
            if ((bool)_fIsExiting.GetValue(__instance))
            {
                return false;
            }

            Color themeColor = os.highlightColor;

            // ---- Static：合并原解析逻辑（当前目录 + ~/sys + ~/home 的有效主题文件）----
            var staticNames = new List<string>();
            var staticData = new List<string>();
            CollectStaticThemes(os, staticNames, staticData);

            // ---- Dynamic：动态壁纸解析逻辑（留空）----
            var dynamicNames = new List<string>();
            var dynamicData = new List<string>();

            Vector2 pos = new Vector2(dest.X + 2, dest.Y + 2);

            // ===== Static 栏 =====
            TextItem.doFontLabel(pos, "Static", GuiData.smallfont, themeColor, bounds.Width - 20, 20f);
            pos.Y += 18f;
            sb.Draw(Utils.white, new Rectangle(bounds.X + 2, (int)pos.Y, bounds.Width - 6, 1), Utils.AddativeWhite);
            pos.Y += 2f;

            if (staticNames.Count > 0)
            {
                SelectableTextList.scrollOffset = _staticScroll;
                int sel = SelectableTextList.doFancyList(8139191 + pid,
                    (int)pos.X, (int)pos.Y, bounds.Width - 6, 54,
                    staticNames.ToArray(), _staticSelected,
                    Color.Lerp(os.topBarColor, Utils.AddativeWhite, 0.2f),
                    HasDraggableScrollbar: true);
                if (SelectableTextList.selectionWasChanged)
                {
                    _dynamicSelected = -1;
                }
                _staticScroll = SelectableTextList.scrollOffset;
                if (sel >= 0 && sel < staticNames.Count)
                {
                    _staticSelected = sel;
                }
                else
                {
                    _staticSelected = -1;
                }
            }
            else
            {
                var empty = new Rectangle((int)pos.X, (int)pos.Y, bounds.Width - 6, 54);
                sb.Draw(Utils.white, empty, Utils.VeryDarkGray);
                TextItem.doFontLabelToSize(empty, "    -- No Valid Files --    ", GuiData.smallfont, Utils.AddativeWhite);
            }
            pos.Y += 54 + 6;

            // ===== Dynamic 栏 =====
            TextItem.doFontLabel(pos, "Dynamic", GuiData.smallfont, themeColor, bounds.Width - 20, 20f);
            pos.Y += 18f;
            sb.Draw(Utils.white, new Rectangle(bounds.X + 2, (int)pos.Y, bounds.Width - 6, 1), Utils.AddativeWhite);
            pos.Y += 2f;

            if (dynamicNames.Count > 0)
            {
                SelectableTextList.scrollOffset = _dynamicScroll;
                int sel = SelectableTextList.doFancyList(839192 + pid,
                    (int)pos.X, (int)pos.Y, bounds.Width - 6, 72,
                    dynamicNames.ToArray(), _dynamicSelected,
                    Color.Lerp(os.topBarColor, Utils.AddativeWhite, 0.2f),
                    HasDraggableScrollbar: true);
                if (SelectableTextList.selectionWasChanged)
                {
                    _staticSelected = -1;
                }
                _dynamicScroll = SelectableTextList.scrollOffset;
                if (sel >= 0 && sel < dynamicNames.Count)
                {
                    _dynamicSelected = sel;
                }
                else
                {
                    _dynamicSelected = -1;
                }
            }
            else
            {
                var empty = new Rectangle((int)pos.X, (int)pos.Y, bounds.Width - 6, 72);
                sb.Draw(Utils.white, empty, Utils.VeryDarkGray);
                TextItem.doFontLabelToSize(empty, "    -- No Valid Files --    ", GuiData.smallfont, Utils.AddativeWhite);
            }
            pos.Y += 72 + 2;

            // ===== Apply 区（取当前选中项）=====
            string selectedName = null;
            string selectedData = null;
            if (_staticSelected >= 0 && _staticSelected < staticNames.Count)
            {
                selectedName = staticNames[_staticSelected];
                selectedData = staticData[_staticSelected];
            }
            else if (_dynamicSelected >= 0 && _dynamicSelected < dynamicNames.Count)
            {
                selectedName = dynamicNames[_dynamicSelected];
                selectedData = dynamicData[_dynamicSelected];
            }

            Rectangle applyBounds = new Rectangle(bounds.X + 4, (int)pos.Y + 2,
                bounds.Width - 8, (int)(dest.Height - (pos.Y - dest.Y)) - 4);
            DrawApplyField(os, pid, selectedName, selectedData, applyBounds, sb);

            return false; // 跳过原方法
        }

        // ================= Static 解析（原 Remote + Local 合并） =================

        private static void CollectStaticThemes(OS os, List<string> names, List<string> data)
        {
            var seen = new HashSet<string>();

            // 原 Remote：当前目录
            Folder current = Programs.getCurrentFolder(os);
            CollectFolderThemes(current, names, data, seen);

            // 原 Local：~/sys + ~/home
            if (os.thisComputer?.files?.root != null)
            {
                CollectFolderThemes(os.thisComputer.files.root.searchForFolder("sys"), names, data, seen);
                CollectFolderThemes(os.thisComputer.files.root.searchForFolder("home"), names, data, seen);
            }
        }

        private static void CollectFolderThemes(Folder folder, List<string> names, List<string> data, HashSet<string> seen)
        {
            if (folder == null)
            {
                return;
            }

            foreach (FileEntry f in folder.files)
            {
                if (ThemeManager.getThemeForDataString(f.data) == 0)
                {
                    continue;
                }

                string key = f.name + "\n" + f.data;
                if (!seen.Add(key))
                {
                    continue; // 去重（同名同数据）
                }

                names.Add(f.name);
                data.Add(f.data);
            }
        }

        // ================= Apply 区（对齐原 DrawApplyField + ApplyTheme） =================

        private static void DrawApplyField(OS os, int pid, string selectedFilename, string selectedFileData, Rectangle bounds, SpriteBatch sb)
        {
            sb.Draw(Utils.white, bounds, Utils.VeryDarkGray);
            sb.Draw(Utils.white, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), Utils.AddativeWhite);

            if (selectedFileData == null || selectedFilename == null)
            {
                return;
            }

            OSTheme theme = ThemeManager.getThemeForDataString(selectedFileData);
            Color representative = ThemeManager.GetRepresentativeColorForTheme(theme);

            Rectangle destRect = new Rectangle(bounds.X + bounds.Width / 4 * 3, bounds.Y + 2, bounds.Width / 4, bounds.Height - 2);
            sb.Draw(Utils.white, destRect, representative);
            destRect.X += destRect.Width - 15;
            destRect.Width = 15;
            sb.Draw(Utils.white, destRect, Color.Black * 0.6f);

            TextItem.doFontLabel(new Vector2(bounds.X, bounds.Y + 2), selectedFilename, GuiData.smallfont, Utils.AddativeWhite, bounds.Width / 4 * 3, 25f);

            if (Button.doButton(3837791 + pid, bounds.X, bounds.Y + 25, bounds.Width / 6 * 5, 30, "Activate Theme", representative))
            {
                ApplyTheme(os, selectedFileData);
            }
        }

        private static void ApplyTheme(OS os, string fileData)
        {
            const string backupPrefix = "x-serverBACKUP";
            Folder sys = os.thisComputer.files.root.searchForFolder("sys");
            if (sys == null)
            {
                return;
            }

            FileEntry xs = sys.searchForFile("x-server.sys");
            if (xs != null)
            {
                bool alreadyBackedUp = false;
                for (int i = 0; i < sys.files.Count; i++)
                {
                    if (sys.files[i].name.StartsWith(backupPrefix) && sys.files[i].data == xs.data)
                    {
                        alreadyBackedUp = true;
                        break;
                    }
                }

                if (!alreadyBackedUp)
                {
                    string backupName = Utils.GetNonRepeatingFilename(backupPrefix, ".sys", sys);
                    sys.files.Add(new FileEntry(xs.data, backupName));
                }
            }

            if (xs != null)
            {
                xs.data = fileData;
                ThemeManager.switchTheme(os, ThemeManager.getThemeForDataString(fileData));
            }
        }

        // ================= 反射 =================

        private static bool EnsureFields(object instance)
        {
            if (_tcType == null)
            {
                _tcType = instance.GetType();
                _fBounds = AccessTools.Field(_tcType, "bounds");
                _fOs = AccessTools.Field(_tcType, "os");
                _fPid = AccessTools.Field(_tcType, "PID");
                _fIsExiting = AccessTools.Field(_tcType, "isExiting");

                if (_fBounds == null || _fOs == null || _fPid == null || _fIsExiting == null)
                {
                    KELog.Error("[ThemeChanger] reflection failed (bounds/os/PID/isExiting not found)");
                    return false;
                }
            }

            return true;
        }
    }
}
