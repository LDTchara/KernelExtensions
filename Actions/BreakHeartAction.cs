using System;
using Hacknet;
using KernelExtensions.Utility;
using Pathfinder.Action;
using Pathfinder.Util;
using PorthackHeartDaemon = KernelExtensions.Daemons.PorthackHeartDaemon;

namespace KernelExtensions.Actions
{
    /// <summary>
    /// 9.47 显式触发目标节点的 PorthackHeartDaemon 心碎序列（剧情触发入口）。
    ///
    /// 用法（全部参数可选，缺省用 daemon 自身 XML 配置）：
    ///   &lt;BreakHeart NodeID="heart" OnComplete="Actions/HeartBroken" /&gt;
    ///   &lt;BreakHeart NodeID="heart" Music="" LockInput="false" /&gt;
    ///
    /// 参数：NodeID（必填，目标节点）；其余（Title/Music/FadeoutDelay/FadeoutDuration/
    ///   AlignTime/HeartDuration/FlashOutTime/OnComplete/OnHeartbreak/LockInput）可选覆盖——
    ///   null/未提供 = 不覆盖（用 daemon 自身配置）；字符串项写 NONE/空 = 覆盖为禁用
    ///   （如 Music="NONE" 表示不切歌、OnComplete="NONE" 表示不执行）。
    /// </summary>
    public class BreakHeartAction : DelayablePathfinderAction
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
                    KELog.Warn($"[BreakHeart] node not found: {NodeID}");
                    return;
                }

                PorthackHeartDaemon daemon = null;
                foreach (var d in comp.daemons)
                    if (d is PorthackHeartDaemon phd) { daemon = phd; break; }

                if (daemon == null)
                {
                    KELog.Warn($"[BreakHeart] node '{NodeID}' has no PorthackHeartDaemon");
                    return;
                }

                ApplyOverrides(daemon);
                daemon.BreakHeart();
            }
            catch (Exception ex)
            {
                KELog.Error("[BreakHeart] Trigger failed: " + ex.Message);
            }
        }

        private void ApplyOverrides(PorthackHeartDaemon d)
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
