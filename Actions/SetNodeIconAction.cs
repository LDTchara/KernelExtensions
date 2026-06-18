using System;
using System.Collections.Generic;
using System.Reflection;
using Hacknet;
using Microsoft.Xna.Framework.Graphics;
using Pathfinder.Action;
using Pathfinder.Util;
using Pathfinder.Util.XML;
using KernelExtensions.Storage;
using KernelExtensions.Patches;
using KernelExtensions.Utility;

namespace KernelExtensions.Actions
{
    public class SetNodeIconAction : DelayablePathfinderAction
    {
        public const string RESET_MARKER = "#RESET#";

        [XMLStorage] public string TargetComp;
        [XMLStorage] public string Icon;

        private static FieldInfo _compAltIconsField;
        private static bool _compAltIconsFieldLookedUp;

        private static readonly string[] SecLevelTextures =
        {
            "Sprites/CompLogos/Sec0Computer", "Sprites/CompLogos/Sec1Computer",
            "Sprites/CompLogos/Computer", "Sprites/CompLogos/OldServer",
            "Sprites/CompLogos/Sec2Computer", "Sprites/CompLogos/Sec2Computer",
            "Sprites/CompLogos/Computer",
        };

        public override void Trigger(OS os)
        {
            var comp = ComputerLookup.Find(TargetComp, SearchType.Id) ?? Programs.getComputer(os, TargetComp);
            if (comp == null) { os.write($"SetNodeIcon: 未找到节点 {TargetComp}"); return; }

            if (Icon == RESET_MARKER)
            {
                if (!NodeIconStorage.HasOrgIcon(comp.idName))
                {
                    os.write($"SetNodeIcon: 节点 {TargetComp} 没有记录的原始图标，无法重置");
                    return;
                }
                string orgIcon = NodeIconStorage.ResetIcon(comp.idName);
                ApplyIcon(comp, string.IsNullOrEmpty(orgIcon) ? null : orgIcon, os);
            }
            else
            {
                if (string.IsNullOrEmpty(Icon)) { os.write("SetNodeIcon: Icon 参数为空"); return; }
                NodeIconStorage.SetCurrIcon(comp.idName, Icon);
                ApplyIcon(comp, Icon, os);
            }
        }

        private static void ApplyIcon(Computer comp, string iconKey, OS os)
        {
            if (iconKey == null) { comp.icon = null; return; }

            // 记录原始图标（仅首次），不管是 Set 还是 Reset 都保护性记录
            NodeIconStorage.InitOrgIcon(comp.idName, comp.icon);

            // @ 前缀 → 自定义图标（从 CustomTextures 取，不依赖 compAltIcons）
            if (iconKey.StartsWith("@"))
            {
                if (NodeIconRenderPatch.CustomTextures.TryGetValue(iconKey, out var tex) && tex != null)
                {
                    comp.icon = iconKey;
                    return;
                }
                comp.icon = iconKey;
                KELog.Warn($"[SetNodeIcon] custom texture not found: {iconKey}");
                return;
            }

            // 预设名称 → 走 compAltIcons
            var dict = GetCompAltIcons(os);
            if (dict != null && dict.ContainsKey(iconKey)) { comp.icon = iconKey; return; }

            if (dict != null && iconKey.StartsWith(NodeIconStorage.SEC_PREFIX))
            {
                int seclv = Math.Max(0, Math.Min(comp.securityLevel, SecLevelTextures.Length - 1));
                var tex = TextureBank.load(SecLevelTextures[seclv], os.content);
                if (tex != null) { dict[iconKey] = tex; comp.icon = iconKey; return; }
            }

            comp.icon = iconKey;
        }

        public static void ApplyIconFromStorage(Computer comp, OS os)
        {
            string key = NodeIconStorage.GetCurrIcon(comp.idName);
            if (!string.IsNullOrEmpty(key)) ApplyIcon(comp, key, os);
        }

        internal static Dictionary<string, Texture2D> GetCompAltIcons(OS os)
        {
            if (!_compAltIconsFieldLookedUp)
            {
                _compAltIconsField = typeof(DisplayModule).GetField("compAltIcons",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                _compAltIconsFieldLookedUp = true;
            }
            if (_compAltIconsField == null) return null;
            var dm = os?.display ?? OS.currentInstance?.display;
            if (dm == null) return null;
            return _compAltIconsField.GetValue(dm) as Dictionary<string, Texture2D>;
        }
    }
}
