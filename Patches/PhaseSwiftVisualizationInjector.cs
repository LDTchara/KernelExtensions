using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using KernelExtensions.Modules;
using Microsoft.Xna.Framework.Media;

namespace KernelExtensions.Patches
{
    /// <summary>
    /// Prefix 注入：用 PhaseSwiftManager 的实时 PCM 数据填进 VisualizationData.sampList。
    /// 直接修改 backing List<float>，ReadOnlyCollection Samples 自动同步。
    /// </summary>
    [HarmonyPatch(typeof(MediaPlayer), "GetVisualizationData")]
    public class PhaseSwiftVisualizationInjector
    {
        private static FieldInfo _sampListField;
        private static bool _initDone = false;
        private const int SIZE = 256;

        private static void Init(VisualizationData data)
        {
            if (_initDone) return;

            // 优先找名含 "samp" 的 List<float>，再退回到任意 List<float>
            Type t = typeof(VisualizationData);
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (f.Name.IndexOf("samp", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    f.FieldType == typeof(List<float>))
                {
                    _sampListField = f;
                    break;
                }
            }
            if (_sampListField == null)
            {
                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                {
                    if (f.FieldType == typeof(List<float>))
                    {
                        _sampListField = f;
                        break;
                    }
                }
            }

            _initDone = true;
        }

        [HarmonyPrefix]
        static bool Prefix(VisualizationData data)
        {
            if (!PhaseSwiftManager.UseDualTrack) return true;
            Init(data);
            if (_sampListField == null)
                return true;

            var dst = _sampListField.GetValue(data) as List<float>;
            if (dst == null)
                return true;

            // 调用 Manager 从滚动缓冲实时采样
            PhaseSwiftManager.UpdateVisualization();

            // 读取 CurrentVisBands（UpdateVisualization 写入的 256 个值）
            var src = PhaseSwiftManager.CurrentVisBands;
            if (src == null || src.Length == 0)
                return true;

            dst.Clear();
            for (int i = 0; i < SIZE; i++)
            {
                float val = 0f;
                int idx = i * src.Length / SIZE;
                if (idx >= 0 && idx < src.Length)
                    val = src[idx];
                dst.Add(val);
            }

            return false; // 跳过原版 GetVisualizationData
        }
    }
}
