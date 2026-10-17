using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hacknet;
using KernelExtensions.Managers;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using Pathfinder.Util;

namespace KernelExtensions.Actions.Misc
{
    /// <summary>
    /// 临时修改当前 OS 的主题颜色字段——**只灌颜色，不切换主题**（布局与背景保持原样）。
    ///
    /// XML 用法：
    ///   &lt;ChangeThemeColor Fields="defaultHighlightColor:Red,warningColor:#FF0000" /&gt;
    ///   &lt;ChangeThemeColor Fields="defaultHighlightColor:RGB,warningColor:#FF0000" Duration="10" /&gt;
    ///
    /// · Fields 为逗号分隔的「字段名:颜色值」列表；字段名须是主题的颜色字段
    ///   （如 defaultHighlightColor / warningColor / moduleColorSolidDefault / terminalTextColor 等）。
    /// · 颜色值分静态与动态两类，同一条 Fields 里可混用：
    ///     静态 —— XNA 命名色（如 Red，大小写不敏感）、#RGB、#RRGGBB、#AARRGGBB、R,G,B[,A]，
    ///             走 <see cref="ColorHelper.ParseStaticColor"/>，只在触发时写一次；
    ///     动态 —— LDTchara[:速度[:透明度[:饱和度[:明度]]]]、RGB（LDTchara 的别名）、
    ///             Gradient:#起始色:#结束色:秒数、CustomColor 预设名[:速度倍率[:透明度]]，
    ///             走 <see cref="CustomColorManager.ParseColorString"/>，逐帧重算。
    /// · Duration：动态色的持续秒数；不写 / ≤ 0 / NaN / Infinity = 不限期（到玩家切主题为止）。
    /// · 字段值写 NONE / 空 = 该字段保持不变。
    /// · 未知字段名、格式不符的键值对 → KELog.Warn 并跳过，不影响其余字段。
    ///
    /// 撤销（"临时"语义）：
    ///   · 只改 OS 上的当前值，不写存档、不改玩家已加载的主题对象；
    ///   · **静态**字段：玩家切换主题后 OS 字段被主题重新写入，本次修改即自然失效；
    ///   · **动态**字段逐帧回写，必须显式收尾——Duration 到期 → 还原触发前的原值
    ///     （动态字段本身 + 派生色 topBarColor / highlightColor / moduleColorSolid）；
    ///     玩家切主题 → 立即停手且**不还原**（颜色已归新主题所有，还原会把新主题压回去）。
    ///
    /// 开销：动态字段走 os.UpdateSubscriptions 逐帧重算，但单通道变化不到 <c>RefreshEpsilon</c>
    /// 字节就既不写字段也不刷派生色；主题副本的 LoadIntoOS 只在触发时做一次，
    /// 逐帧只做「命中字段的 SetValue + RefreshTheme」。
    /// </summary>
    public class ChangeThemeColorAction : KEAction
    {
        /// <summary>逗号分隔的「字段名:颜色值」列表，如 defaultHighlightColor:Red,warningColor:#FF0000。</summary>
        [XMLStorage] public string Fields = "";

        /// <summary>
        /// 动态色的持续秒数。不写 / ≤ 0 / NaN / Infinity = 不限期（到玩家切主题为止）。
        /// 到期后把触发前的颜色写回（只还原动态字段与 topBarColor / highlightColor / moduleColorSolid；
        /// 静态字段按"切主题才失效"的既有语义保持改动）。
        /// </summary>
        [XMLStorage] public float Duration = -1f;

        /// <summary>同一 OS 上仍在运行的临时主题色实例（弱引用：不阻止 OS 被回收）。</summary>
        private static readonly ConditionalWeakTable<OS, ThemeOverride> ActiveOverrides = new();

        /// <summary>
        /// 节流阈值：单通道变化不到该字节数就不写字段、不刷派生色。
        /// 2/255 约 0.8%，肉眼不可辨；慢速彩虹（速度 0.1 → 一周期 10 秒）在 60fps 下约 5 帧
        /// 才跨过一个阈值，可省掉大部分无意义的反射写入与派生色重算。
        /// </summary>
        private const int RefreshEpsilon = 2;

        /// <summary>由 RefreshTheme 从各自 default 字段重算的三个派生色。</summary>
        private static readonly string[] DerivedColorFields = { "topBarColor", "highlightColor", "moduleColorSolid" };

        public override void Trigger(OS os)
        {
            if (os == null || string.IsNullOrWhiteSpace(Fields)) return;

            Type themeType = typeof(CustomTheme);
            Type osType = typeof(OS);

            // ── 1. 解析与校验：这一步不碰 OS，全部无效时连上一次效果都不打断 ──
            var statics = new List<KeyValuePair<string, string>>();
            var dynamics = new List<DynamicTarget>();

            foreach (string pair in Fields.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = pair.Split(new[] { ':' }, 2);
                if (kv.Length != 2)
                {
                    KELog.WarnT($"[ChangeThemeColor] malformed pair '{pair}', expected 'field:colour'");
                    continue;
                }

                string key = kv[0].Trim();
                string value = kv[1].Trim();

                if (ConfigValue.IsNone(value)) continue;   // NONE / 空 = 不改该字段

                var themeField = themeType.GetField(key);
                if (themeField == null || themeField.FieldType != typeof(Color))
                {
                    KELog.WarnT($"[ChangeThemeColor] unknown colour field '{key}'");
                    continue;
                }

                // 动态关键字（彩虹 / 渐变 / CC 预设名）优先：这些值不是静态颜色写法
                var dyn = CustomColorManager.ParseColorString(value);
                if (dyn != null)
                {
                    var osField = osType.GetField(key);
                    if (osField == null || osField.FieldType != typeof(Color))
                    {
                        KELog.WarnT($"[ChangeThemeColor] '{key}' has no matching OS colour field, dynamic value skipped");
                        continue;
                    }
                    dynamics.Add(new DynamicTarget(key, osField, dyn));
                    continue;
                }

                if (!LooksLikeColour(value))
                {
                    KELog.WarnT($"[ChangeThemeColor] cannot parse colour '{value}' for '{key}', field left unchanged");
                    continue;
                }

                statics.Add(new KeyValuePair<string, string>(key, value));
            }

            if (statics.Count == 0 && dynamics.Count == 0) return;

            // ── 2. 重复触发 = 刷新：先停掉同 OS 上仍在跑的旧效果（它会还原自己的快照）──
            if (ActiveOverrides.TryGetValue(os, out var old))
            {
                old.Cancel();
                ActiveOverrides.Remove(os);
            }

            // ── 3. 以当前 OS 的颜色字段打底新建一份主题副本，只覆盖本次指定的字段 ──
            // 刻意不复用 ThemeManager.LastLoadedCustomTheme：那是玩家已加载的主题对象，
            // 改它会污染后续切回该主题时的颜色（本 Action 的语义是"临时"）。
            var theme = new CustomTheme();
            foreach (var f in themeType.GetFields())
            {
                var src = osType.GetField(f.Name);
                if (src != null) f.SetValue(theme, src.GetValue(os));
            }

            foreach (var s in statics)
            {
                var field = themeType.GetField(s.Key);
                field.SetValue(theme, ColorHelper.ParseStaticColor(s.Value, (Color)field.GetValue(theme)));
            }

            // 动态字段：首帧值也写进副本，使 LoadIntoOS 之后 OS 上的值与节流基线一致
            double now = OS.currentElapsedTime;
            foreach (var target in dynamics)
            {
                Color first = CustomColorManager.CalcColor(target.Config, now);
                themeType.GetField(target.Key).SetValue(theme, first);
                target.Last = first;
            }

            // 还原快照必须在 LoadIntoOS / RefreshTheme 之前读（此时 OS 还是触发前的样子）
            ThemeOverride pending = dynamics.Count > 0 ? new ThemeOverride(os, dynamics, Duration) : null;

            // 只把颜色灌入当前 OS：不切主题，保留现有布局与背景。
            theme.LoadIntoOS(os);
            os.RefreshTheme();   // 同步派生色 topBarColor / highlightColor / moduleColorSolid

            if (pending != null)
            {
                pending.Attach();
                KELog.Info($"[ChangeThemeColor] applied {statics.Count} static / {dynamics.Count} dynamic field(s), "
                           + $"dynamic lifetime {FormatLifetime(pending)}");
            }
            else
            {
                KELog.Info($"[ChangeThemeColor] applied {statics.Count} static field(s)");
            }
        }

        /// <summary>粗判是否为本解析链认得的**静态**颜色写法（仅用于解析失败时给出 Warn；动态关键字在它之前已分流）。</summary>
        private static bool LooksLikeColour(string value)
        {
            if (value.StartsWith("#", StringComparison.Ordinal)) return true;
            if (value.Contains(',')) return true;
            return ColorHelper.TryGetNamedColor(value, out _);
        }

        /// <summary>两个颜色在 R/G/B/A 四通道上的最大字节差（节流判据）。</summary>
        private static int MaxChannelDelta(Color a, Color b)
        {
            int d = Math.Abs(a.R - b.R);
            d = Math.Max(d, Math.Abs(a.G - b.G));
            d = Math.Max(d, Math.Abs(a.B - b.B));
            return Math.Max(d, Math.Abs(a.A - b.A));
        }

        /// <summary>日志用：不限期 / N 秒。</summary>
        private static string FormatLifetime(ThemeOverride o)
            => o.Unlimited ? "indefinite" : o.Duration.ToString("0.##", CultureInfo.InvariantCulture) + "s";

        /// <summary>一个动态字段的运行时状态（引用类型：节流基线要能原地更新）。</summary>
        private sealed class DynamicTarget
        {
            public readonly string Key;
            public readonly FieldInfo Field;
            public readonly CustomColorManager.DynColorConfig Config;

            /// <summary>上次真正写入 OS 的颜色（节流基线）。</summary>
            public Color Last;

            /// <summary>触发前的原值，Duration 到期时写回。</summary>
            public Color Original;

            public DynamicTarget(string key, FieldInfo field, CustomColorManager.DynColorConfig config)
            {
                Key = key;
                Field = field;
                Config = config;
            }
        }

        /// <summary>
        /// 一次「临时主题色」的运行时实例：持有动态字段与还原快照，由 os.UpdateSubscriptions 驱动。
        /// 按 OS 存在 <see cref="ActiveOverrides"/> 里：同一 OS 重复触发 = 刷新（先还原旧的再起新的），
        /// 且不阻止 OS 被回收。
        /// </summary>
        private sealed class ThemeOverride
        {
            private readonly OS os;
            private readonly List<DynamicTarget> targets;
            private readonly Dictionary<FieldInfo, Color> derivedOriginal = new();
            private readonly string themeKey;
            private float remaining;
            private bool finished;

            /// <summary>动态色持续秒数（≤ 0 视为不限期，见 <see cref="Unlimited"/>）。</summary>
            public float Duration { get; }

            public bool Unlimited => Duration <= 0f;

            /// <summary>在 LoadIntoOS / RefreshTheme 之前构造：此时 OS 上还是触发前的颜色。</summary>
            public ThemeOverride(OS os, List<DynamicTarget> targets, float duration)
            {
                this.os = os;
                this.targets = targets;
                Duration = (float.IsNaN(duration) || float.IsInfinity(duration)) ? -1f : duration;
                remaining = Duration;
                themeKey = CurrentThemeKey();

                foreach (var t in targets) t.Original = (Color)t.Field.GetValue(os);

                // 派生色快照：RefreshTheme 会把这三个字段改成各自的 default 值
                Type osType = os.GetType();
                foreach (string name in DerivedColorFields)
                {
                    var f = osType.GetField(name);
                    if (f != null && f.FieldType == typeof(Color)) derivedOriginal[f] = (Color)f.GetValue(os);
                }
            }

            public void Attach()
            {
                ActiveOverrides.Remove(os);
                ActiveOverrides.Add(os, this);
                os.UpdateSubscriptions += Update;
            }

            private void Update(float dt)
            {
                if (finished) return;
                try
                {
                    Step(dt);
                }
                catch (Exception ex)
                {
                    // 每帧钩子抛异常会一路冒到 OS.Update → Game1.Update（直接崩帧），
                    // 所以这里兜底：记 Error 并停手，OS 上保持当前颜色不改。
                    KELog.ErrorT("[ChangeThemeColor] dynamic update failed: " + ex.Message);
                    Finish();
                }
            }

            private void Step(float dt)
            {
                // 1. 生存期：Duration 到期 → 还原触发前的颜色
                if (!Unlimited)
                {
                    remaining -= dt;
                    if (remaining <= 0f)
                    {
                        Restore();
                        Finish();
                        return;
                    }
                }

                // 2. 玩家切了主题 → 停手但不还原（这些字段已属于新主题）
                if (CurrentThemeKey() != themeKey)
                {
                    Finish();
                    return;
                }

                // 3. 逐帧重算 + 节流：真的看得出变化才写字段、刷派生色
                bool changed = false;
                double now = OS.currentElapsedTime;
                foreach (var t in targets)
                {
                    Color c = CustomColorManager.CalcColor(t.Config, now);
                    if (MaxChannelDelta(c, t.Last) < RefreshEpsilon) continue;
                    t.Last = c;
                    t.Field.SetValue(os, c);
                    changed = true;
                }
                if (changed) os.RefreshTheme();
            }

            /// <summary>重复触发 / 外部取消：还原并停手。</summary>
            public void Cancel()
            {
                if (finished) return;
                Restore();
                Finish();
            }

            /// <summary>把动态字段与派生色写回触发前的值（静态字段不还原：其语义是"切主题才失效"）。</summary>
            private void Restore()
            {
                foreach (var t in targets) t.Field.SetValue(os, t.Original);
                foreach (var kv in derivedOriginal) kv.Key.SetValue(os, kv.Value);
            }

            private void Finish()
            {
                finished = true;
                os.UpdateSubscriptions -= Update;
                if (ActiveOverrides.TryGetValue(os, out var current) && ReferenceEquals(current, this))
                    ActiveOverrides.Remove(os);
            }

            /// <summary>当前主题标识：切主题（含换自定义主题文件）时它变。</summary>
            private static string CurrentThemeKey()
                => ThemeManager.currentTheme + "|" + ThemeManager.LastLoadedCustomThemePath;
        }
    }
}
