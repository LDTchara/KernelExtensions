using Hacknet;
using KernelExtensions.Utilities;
using Pathfinder.Action;
using Pathfinder.Util;
using CustomHeartDaemon = KernelExtensions.Daemons.CustomHeartDaemon;

namespace KernelExtensions.Actions.Misc
{
    /// <summary>
    /// 显式触发目标节点的 CustomHeartDaemon 心碎序列（剧情触发入口）。
    ///
    /// 用法（全部参数可选，缺省用 daemon 自身 XML 配置）：
    ///   &lt;BreakHeart NodeID="heart" OnComplete="Actions/HeartBroken" /&gt;
    ///   &lt;BreakHeart NodeID="heart" Music="" LockInput="false" /&gt;
    ///
    /// 参数：NodeID（必填，目标节点）；其余（Title/Music/FadeoutDelay/FadeoutDuration/
    ///   AlignTime/HeartDuration/FlashOutTime/OnComplete/OnHeartbreak/LockInput）可选覆盖——
    ///   注：HeartDuration 目前是保留项（原版序列固定节奏，暂未生效）。
    ///   null/未提供 = 不覆盖（用 daemon 自身配置）；字符串项写 NONE/空 = 覆盖为禁用
    ///   （如 Music="NONE" 表示不切歌、OnComplete="NONE" 表示不执行）。
    /// </summary>
    public class BreakHeartAction : KEAction
    {
        [XMLStorage] public string NodeID;

        // 覆盖项：可空/空 = 不覆盖，用 daemon 自身配置
        [XMLStorage] public string Title;
        [XMLStorage] public string Music;
        [XMLStorage] public float? FadeoutDelay;
        [XMLStorage] public float? FadeoutDuration;
        [XMLStorage] public float? AlignTime;
        [XMLStorage] public float? HeartDuration;
        [XMLStorage] public float? FlashOutTime;
        [XMLStorage] public string OnComplete;
        [XMLStorage] public string OnHeartbreak;
        [XMLStorage] public bool? LockInput;

        public override void Trigger(OS os)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(NodeID))
                {
                    os.write("[BreakHeart] Missing NodeID");
                    return;
                }

                var comp = ComputerLookup.FindById(NodeID);
                if (comp == null)
                {
                    KELog.WarnT($"[BreakHeart] node not found: {NodeID}");
                    return;
                }

                CustomHeartDaemon daemon = null;
                foreach (var d in comp.daemons)
                    if (d is CustomHeartDaemon phd) { daemon = phd; break; }

                if (daemon == null)
                {
                    KELog.WarnT($"[BreakHeart] node '{NodeID}' has no CustomHeartDaemon");
                    return;
                }

                // 心碎序列由 daemon 的 draw 驱动，而 DisplayModule 只会绘制"当前连接机器"的
                // daemon。玩家在别的节点上触发时序列会后台空转、看不到效果；因此 BreakHeart
                // 直接强制切到目标节点（不经过 connect 的端口/防火墙校验）。
                // Display 的接管与 command 切换由 CustomHeartDisplayPatch / daemon.BreakHeart 完成。
                if (os.connectedComp != comp)
                {
                    try { os.connectedComp?.disconnecting(os.thisComputer.ip); } catch { }
                    os.connectedComp = comp;
                    os.connectedIP = comp.ip;
                    os.terminal.prompt = comp.ip + "@> ";
                    os.navigationPath.Clear();
                    int nodeIdx = os.netMap.nodes.IndexOf(comp);
                    if (nodeIdx >= 0 && !os.netMap.visibleNodes.Contains(nodeIdx))
                        os.netMap.visibleNodes.Add(nodeIdx);
                    KELog.Info($"[BreakHeart] force-connected to '{NodeID}' for the heartbreak sequence.");
                }

                ApplyOverrides(daemon);
                daemon.BreakHeart();
            }
            catch (Exception ex)
            {
                KELog.ErrorT("[BreakHeart] Trigger failed: " + ex.Message);
            }
        }

        private void ApplyOverrides(CustomHeartDaemon d)
        {
            if (Title != null) d.Title = Title;
            if (Music != null) d.Music = Music;              // 空 = 显式不切歌
            if (FadeoutDelay.HasValue) d.FadeoutDelay = FadeoutDelay.Value;
            if (FadeoutDuration.HasValue) d.FadeoutDuration = FadeoutDuration.Value;
            if (AlignTime.HasValue) d.AlignTime = AlignTime.Value;
            if (HeartDuration.HasValue) d.HeartDuration = HeartDuration.Value;
            if (FlashOutTime.HasValue) d.FlashOutTime = FlashOutTime.Value;
            if (OnComplete != null) d.OnComplete = OnComplete;
            if (OnHeartbreak != null) d.OnHeartbreak = OnHeartbreak;
            if (LockInput.HasValue) d.LockInput = LockInput.Value;
        }
    }
}
