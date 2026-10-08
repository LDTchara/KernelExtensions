using Hacknet;
using Hacknet.Extensions;
using KernelExtensions.Configs;
using KernelExtensions.Patches;
using KernelExtensions.Saving;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using NVorbis;
using System.Collections.Concurrent;
using System.Reflection;
using System.Xml.Serialization;

namespace KernelExtensions.Managers
{
    /// <summary>
    /// PhaseSwift 核心管理器（静态类）。
    ///
    /// 提供不依赖 Exe 实例的完整 PhaseSwift 功能：
    ///   音频系统（DSEI 流式 OGG 解码 + 交叉淡化）
    ///   场景系统（拓扑/可见性/节点发现）
    ///   主题切换（布局保护/自定义路径）
    ///   可视化数据导出（CurrentVisBands）
    ///
    /// 使用方式：
    ///   1. Initialize(os, configName)  — 加载配置
    ///   2. Start()                     — 启动音频 + 应用场景
    ///   3. SwitchToScene()             — 切换场景
    ///   4. Stop()                      — 清理
    ///
    /// 不依赖 Exe，可通过 PhaseSwiftInitAction 等 Action 直接调用。
    /// </summary>
    public static class PhaseSwiftManager
    {
        public static bool IsInitialized { get; private set; }
        public static bool IsRunning { get; private set; }
        public static PhaseSwiftConfig Config { get; private set; }
        public static string ExtensionRoot { get; private set; }
        public static int CurrentScene { get; set; }
        public static int CurrentMusicPhase { get; set; }
        public static bool UseDualTrack { get; private set; } = true;
        public static Color CachedBackgroundColor { get; set; } = Color.Transparent;
        public static string DefaultTheme { get; set; }
        public static OS CurrentOS { get; private set; }

        public static float[] CurrentVisBands = Array.Empty<float>();
        public static float[] PreviousVisBands = Array.Empty<float>();
        public static DateTime LastBandUpdateTime = DateTime.UtcNow;
        /// <summary>
        /// 可视化滚动缓冲：**按轨各一份**（~500ms mono PCM），按各自采样率算大小。
        /// C1：双轨模式下必须分开——两轨都往同一个环形缓冲写时，波形条取“最近 256 样本”
        /// 会取到“最后写入那条轨”的数据，于是可能显示的是没在响的那一轨。
        /// </summary>
        private static float[][] _rollingBufs = Array.Empty<float[]>();
        private static int[] _rollingBufPos = Array.Empty<int>();
        private static int[] _rollingBufCount = Array.Empty<int>();
        /// <summary>各轨采样率（C4 步进采样算跳距用）。</summary>
        private static int[] _trackSampleRate = Array.Empty<int>();
        /// <summary>各轨一块的标称帧数（C2 估算“队列里还有多少帧没播”用）。</summary>
        private static int[] _chunkFrames = Array.Empty<int>();

        private static DynamicSoundEffectInstance[] _dseInstances = Array.Empty<DynamicSoundEffectInstance>();
        private static FileStream[] _trackStreams = Array.Empty<FileStream>();
        private static VorbisReader[] _trackReaders = Array.Empty<VorbisReader>();
        private static int[] _trackChannels = Array.Empty<int>();
        private static volatile bool _stopped;
        private static bool _isFading;
        private static float _fadeProgress, _targetFadeDuration;
        private static float[] _startVolumes = Array.Empty<float>();
        private static float[] _targetVolumes = Array.Empty<float>();

        /// <summary>
        /// 播放队列维持的块数（9.40）。一块 = 1/24 秒 ≈ 41.7ms，
        /// 8 块 ≈ 333ms 余量，可扛约 3 FPS 的帧间隔。
        /// 再深则启动延迟与可视化超前涨得快、收益递减（详见改造清单 B1）。
        /// 需与 <see cref="OnBufferNeeded"/>、<see cref="UpdateAudioBuffers"/> 及初始填充保持一致。
        /// </summary>
        private const int TargetPendingBuffers = 8;

        // —— 循环点（9.38）运行时状态，按轨 ——
        /// <summary>该轨已播出的帧数（**自维护**：VorbisReader.SamplePosition 的 getter 有 packet 粒度
        /// 滞后，实测读 480000 帧后报 479552，不能用来判断循环边界）。</summary>
        private static long[] _framesPlayed = Array.Empty<long>();
        /// <summary>循环起点（帧）。0 = 文件开头。</summary>
        private static long[] _loopStartFrames = Array.Empty<long>();
        /// <summary>循环终点（帧）。等于总帧数时为整曲循环。</summary>
        private static long[] _loopEndFrames = Array.Empty<long>();
        /// <summary>单曲音量倍率（PhaseSwiftTrack.Volume，缺省 1）。在音量最终赋值处相乘。</summary>
        private static float[] _trackVolumeMul = Array.Empty<float>();
        /// <summary>按轨复用的提交缓冲（D1：免去每块两次数组分配）。
        /// 长度 = 一块的 interleaved 样本数（帧数 × 声道）。</summary>
        private static float[][] _chunkBuf = Array.Empty<float[]>();

        private static Dictionary<string, List<int>> _originalLinks = new();
        private static HashSet<string> _controlledNodeIds = new();
        private static HashSet<string> _currentVisibleNodeIds = new();
        private static List<HashSet<string>> _sceneStartIds;
        private static List<HashSet<string>> _sceneVisibleIds;
        private static List<HashSet<string>> _sceneBlockedIds;
        private static Dictionary<int, HashSet<string>> _sceneDiscoveredNodeIds = new();
        internal static PhaseSwiftPersistentState PendingRestore;
        private static Dictionary<int, HashSet<string>> _runtimeBlockedNodeIds = new();

        public static void Initialize(OS os, string configName)
        {
            // D3：碰配置/音频的入口一律回到主线程（Action 可能跑在 loadactions 起的线程上）
            if (!IsOnMainThread()) { RunOnMainThread(() => Initialize(os, configName)); return; }
            CurrentOS = os;
            if (ExtensionLoader.ActiveExtensionInfo != null)
                ExtensionRoot = ExtensionLoader.ActiveExtensionInfo.FolderPath.Replace("\\", "/");

            string configPath = KEPath.ResolveInside("PhaseSwift/" + configName + ".xml", ExtensionRoot);
            if (configPath == null)
            {
                KELog.Warn($"[PhaseSwift] config name escapes the extension folder: {configName}");
                return;
            }
            configPath = configPath.Replace("\\", "/");
            if (!File.Exists(configPath)) { return; }

            try
            {
                using var fs = new FileStream(configPath, FileMode.Open);
                var serializer = new XmlSerializer(typeof(PhaseSwiftConfig));
                Config = (PhaseSwiftConfig)serializer.Deserialize(fs);
            }
            catch { return; }

            CachedBackgroundColor = ParseColor(Config.BackgroundColor);
            _sceneStartIds = Config.Scenes.Select(s => s.StartNodes.Select(n => n.Id).ToHashSet()).ToList();
            _sceneVisibleIds = Config.Scenes.Select(s => s.VisibleNodes.Select(n => n.Id).ToHashSet()).ToList();
            _sceneBlockedIds = Config.Scenes.Select(s => s.BlockedNodes.ToHashSet()).ToList();

            _controlledNodeIds.Clear();
            foreach (var scene in Config.Scenes)
            {
                foreach (var n in scene.StartNodes) _controlledNodeIds.Add(n.Id);
                foreach (var n in scene.VisibleNodes) _controlledNodeIds.Add(n.Id);
                foreach (var link in scene.Topology)
                {
                    _controlledNodeIds.Add(link.From);
                    _controlledNodeIds.Add(link.To);
                }
                foreach (var b in scene.BlockedNodes) _controlledNodeIds.Add(b);
            }

            _originalLinks.Clear();
            foreach (var id in _controlledNodeIds)
            {
                var comp = Programs.getComputer(os, id);
                if (comp != null && !_originalLinks.ContainsKey(id))
                    _originalLinks[id] = new List<int>(comp.links);
            }

            CurrentScene = Config.InitialScene;
            CurrentMusicPhase = 0;

            if (ThemeManager.currentTheme != OSTheme.Custom)
                DefaultTheme = ThemeManager.currentTheme.ToString();
            else
                DefaultTheme = ThemeManager.LastLoadedCustomThemePath;

            UseDualTrack = Config.UseDualTrackMusic;
            IsInitialized = true;
        }

        private static int _conflictWatchFrames;   // 第三方音频冲突的观察窗口（见 UpdateAudioBuffers）

        public static void Start(int? overrideScene = null)
        {
            if (!IsOnMainThread()) { RunOnMainThread(() => Start(overrideScene)); return; }
            if (!IsInitialized || Config == null || IsRunning)
            {
                // PS 是全局单实例（PhaseSwiftManager 全 static）。重复启动时给可见反馈，不要静默忽略
                KELog.Warn(IsRunning
                    ? "[PhaseSwift] already running - ignoring this start request (PS is single-instance)."
                    : "[PhaseSwift] start ignored: not initialized or config missing.");
                return;
            }

            if (UseDualTrack)
            {
                // 停止 MusicManager 的正在播放，避免与 DSEI 叠加。
                // ⚠️ 不可加 `if (MusicManager.isPlaying)` 守卫：第三方（如 Stuxnet.Audio）用**自己的 DSEI**
                //    播放时，原版 isPlaying 恒为 false（它的 MediaPlayer.Play 拦截是 return false），
                //    守卫会让 stop() 根本不被调用，结果是 PS 多轨与第三方音轨**同时输出**
                //    （2026-09-22 实机证实）。stop() 本身幂等且会置 state=3，无条件调用无副作用。
                KELog.Debug($"[PS-diag] Start: entering dual-track branch (overrideScene={overrideScene.HasValue}, isPlaying={MusicManager.isPlaying})");
                MusicManager.stop();
                KELog.Debug($"[PS-diag] Start: MusicManager.stop() returned (isPlaying={MusicManager.isPlaying})");
                // 第三方（如 SASS）用自有 DSEI 播放时，上面的 stop() 停不到它；
                // 而它的起播是异步的，可能晚于本次调用（读档场景实测如此）→ 开一个观察窗口，
                // 在窗口内每帧问一次「有第三方在播吗」，一旦在播就精确停掉并收束窗口。
                // 具体覆盖哪些模组由 Compat/ModCompats 决定，此处不点名。
                _conflictWatchFrames = 1200;   // ≈20 秒，覆盖慢盘/大文件的加载
                if (Compat.ModCompats.IsConflictingAudioPlaying())
                {
                    Compat.ModCompats.StopConflictingAudio();
                    _conflictWatchFrames = 0;
                }
                if (Config.MusicPhases.Count > 0)
                    LoadMusicPhase(Config.MusicPhases[CurrentMusicPhase]);
            }
            else
            {
                // 单曲模式：MusicManager 播放指定音乐
                string singleTrack = Config.SingleTrack;
                if (ConfigValue.IsNone(singleTrack) && Config.MusicPhases.Count > 0 && Config.MusicPhases[0].Tracks.Count > 0)
                    singleTrack = Config.MusicPhases[0].Tracks[0].Path;
                if (!ConfigValue.IsNone(singleTrack))
                {
                    string resolved = MusicPathResolver.ResolveMusicPath(singleTrack, ExtensionRoot);
                    MusicManager.playSongImmediatley(resolved);
                }
            }
            int firstScene = overrideScene ?? Config.InitialScene;
            CurrentScene = -1;
            // AutoRestore（overrideScene 有值）无过渡直切，避免读档重进时主题/音乐闪烁
            SwitchToScene(firstScene, immediate: overrideScene.HasValue);
            IsRunning = true;
        }

        /// <summary>
        /// 停止 PhaseSwift 并清理全部运行时状态。
        /// finishMode — 结束后的节点可见性：none（全隐藏）/ full（全保留）/ scene_N（留场景 N）。
        /// topologyMode — 结束后的拓扑处理：restore（恢复 PS 启动时备份的原始链接，缺省）/
        ///                scene_N（原始链接 + 场景 N 的 Topology，与运行期切到该场景一致）/
        ///                merge（清除受控节点间链接后合并全部场景拓扑）。传 null 或空按 restore 处理。
        /// </summary>
        public static void Stop(string finishMode = "none", string topologyMode = null)
        {
            if (!IsOnMainThread()) { RunOnMainThread(() => Stop(finishMode, topologyMode)); return; }
            if (!IsInitialized) return;

            CleanupAudio();
            // 退出 PS 时同样要断开“连在受控节点上”的连接：此后节点的可见性/拓扑会变化，
            // 继续连着会在地图上残留连接指示（与切场景行为保持一致）。
            // ⚠️ 必须在下面 _controlledNodeIds.Clear() 之前调用，否则无从判断。
            DisconnectIfOnControlledNode();
            ApplyTopologyMode(topologyMode);

            // 根据 FinishMode 处理节点可见性
            if (finishMode == "full")
            {
                // 全整合：保留所有场景的起始节点和已发现节点
                for (int s = 0; s < Config.Scenes.Count; s++)
                {
                    if (s < _sceneStartIds.Count)
                    {
                        foreach (var id in _sceneStartIds[s])
                            MakeNodeVisible(id);
                    }
                    if (_sceneDiscoveredNodeIds.TryGetValue(s, out var discovered))
                    {
                        foreach (var id in discovered)
                            MakeNodeVisible(id);
                    }
                }
            }
            else if (finishMode != null && finishMode.StartsWith("scene_"))
            {
                // 只保留指定场景的节点
                int sceneIdx;
                if (int.TryParse(finishMode.Substring(6), out sceneIdx))
                {
                    HideAllControlledNodes();
                    if (sceneIdx >= 0 && sceneIdx < _sceneStartIds.Count)
                    {
                        foreach (var id in _sceneStartIds[sceneIdx])
                            MakeNodeVisible(id);
                    }
                    if (_sceneDiscoveredNodeIds.TryGetValue(sceneIdx, out var disc))
                    {
                        foreach (var id in disc)
                            MakeNodeVisible(id);
                    }
                }
            }
            else // "none" 或未知值
            {
                HideAllControlledNodes();
            }

            // 恢复主题（可通过 RestoreThemeOnStop 关闭）
            if (Config != null && !Config.RestoreThemeOnStop) { /* 不恢复 */ }
            else if (!ConfigValue.IsNone(DefaultTheme))
            {
                if (Enum.TryParse<OSTheme>(DefaultTheme, true, out OSTheme t))
                {
                    CurrentOS.EffectsUpdater.StartThemeSwitch(0.15f, t, CurrentOS, null);
                    ThemeManager.setThemeOnComputer(CurrentOS.thisComputer, t);
                }
                else if (CurrentOS.EffectsUpdater != null)
                {
                    CurrentOS.EffectsUpdater.StartThemeSwitch(0.15f, OSTheme.Custom, CurrentOS, DefaultTheme);
                    ThemeManager.setThemeOnComputer(CurrentOS.thisComputer, DefaultTheme);
                }
            }

            PhaseSwiftLayoutPatch.Clear();// 取消布局拦截，新会话由 ResetPatch 兜底
            // 清空所有运行时状态，确保下次 Initialize 从干净状态开始
            _controlledNodeIds.Clear();
            _currentVisibleNodeIds.Clear();
            _originalLinks.Clear();
            _sceneStartIds.Clear();
            _sceneVisibleIds.Clear();
            _sceneBlockedIds.Clear();
            _sceneDiscoveredNodeIds.Clear();
            _runtimeBlockedNodeIds.Clear();
            PendingRestore = null;
            _rollingBufs = Array.Empty<float[]>();
            _rollingBufPos = Array.Empty<int>();
            _rollingBufCount = Array.Empty<int>();
            _trackSampleRate = Array.Empty<int>();
            _chunkFrames = Array.Empty<int>();
            _visSampList = null;
            _fadeProgress = 0f;
            _isFading = false;
            CurrentScene = 0;
            Config = null;
            CurrentOS = null;
            ExtensionRoot = null;
            UseDualTrack = false;
            IsRunning = false;
            IsInitialized = false;
        }

        /// <summary>
        /// 若玩家当前连接的是受控节点，强制断开。
        /// 切场景与退出 PS 都要调：这两处之后受控节点的可见性或拓扑会变化，
        /// 继续连着会在地图上残留连接指示（实测：与 eosDevice 共存时表现明显）。
        /// 与切场景使用**同一个判据**，保证行为一致。
        /// </summary>
        private static void DisconnectIfOnControlledNode()
        {
            if (CurrentOS == null) return;
            var connected = CurrentOS.connectedComp;
            if (connected == null || connected == CurrentOS.thisComputer) return;
            if (!_controlledNodeIds.Contains(connected.idName)) return;

            CurrentOS.display.command = "dc";
            CurrentOS.connectedComp = null;
            if (CurrentOS.terminal != null)
                CurrentOS.terminal.writeLine("Connection Lost: Network Changed");
        }

        private static void MakeNodeVisible(string id)
        {
            var comp = Programs.getComputer(CurrentOS, id);
            if (comp == null) return;
            int idx = CurrentOS.netMap.nodes.IndexOf(comp);
            if (idx >= 0 && !CurrentOS.netMap.visibleNodes.Contains(idx))
                CurrentOS.netMap.visibleNodes.Add(idx);
        }

        private static void HideAllControlledNodes()
        {
            foreach (var id in _controlledNodeIds)
            {
                var comp = Programs.getComputer(CurrentOS, id);
                if (comp == null) continue;
                int idx = CurrentOS.netMap.nodes.IndexOf(comp);
                if (CurrentOS.netMap.visibleNodes.Contains(idx))
                    CurrentOS.netMap.visibleNodes.Remove(idx);
            }
        }

        private static void RestoreOriginalLinks()
        {
            foreach (var kv in _originalLinks)
            {
                var comp = Programs.getComputer(CurrentOS, kv.Key);
                if (comp != null) comp.links = new List<int>(kv.Value);
            }
        }

        public static void UpdateAudioBuffers()
        {
            // 本方法挂在 OS.Update Postfix（主线程）—— 在这里记录主线程 ID，
            // 供 IsOnMainThread() 判定（放在早返回之前，未运行时也要能记录）。
            if (_mainThreadId < 0) _mainThreadId = Thread.CurrentThread.ManagedThreadId;

            if (!UseDualTrack) return;
            if (!IsRunning) return;

            // 诊断（D3）：记录本方法所在线程。它挂在 OS.Update（主线程），
            // 而 LoadMusicPhase/CleanupAudio 来自 Action（Hacknet 的 loadactions 会起线程）——
            // 若两者线程 ID 不同，则音频 API 的跨线程竞态坐实（FNA 的池/queuedBuffers 均非线程安全）。
            // 只在线程变化时记一次，避免每帧刷屏。
            int tid = Thread.CurrentThread.ManagedThreadId;
            if (_audioUpdateThreadId != tid)
            {
                _audioUpdateThreadId = tid;
                KELog.Debug($"[PhaseSwift/diag] UpdateAudioBuffers: 线程 {tid}（首次/变更）；"
                    + $"FNA 动态池 {GetDynamicPoolCount()}");
            }

            // 第三方音频冲突：窗口内检测到在播就停掉（覆盖范围见 Compat/ModCompats）
            if (_conflictWatchFrames > 0)
            {
                _conflictWatchFrames--;
                if (Compat.ModCompats.IsConflictingAudioPlaying())
                {
                    Compat.ModCompats.StopConflictingAudio();
                    _conflictWatchFrames = 0;
                }
            }

            SyncVolume();
            for (int i = 0; i < _dseInstances.Length; i++)
            {
                if (_dseInstances[i] != null)
                {
                    try
                    {
                        int pending = _dseInstances[i].PendingBufferCount;
                        int needed = TargetPendingBuffers - pending;
                        for (int b = 0; b < needed; b++) SubmitNextChunk(i);
                    }
                    catch { }
                }
            }
        }

        public static void UpdateCrossfade(float dt)
        {
            if (!UseDualTrack) return;
            if (!_isFading) return;
            float volMul = MusicManager.getVolume();
            _fadeProgress += dt;
            float t = Math.Min(_fadeProgress / _targetFadeDuration, 1f);
            for (int i = 0; i < _dseInstances.Length; i++)
                if (_dseInstances[i] != null)
                    _dseInstances[i].Volume = MathHelper.Lerp(_startVolumes[i], _targetVolumes[i], t) * _trackVolumeMul[i] * volMul;
            if (_fadeProgress >= _targetFadeDuration)
            {
                _isFading = false;
                for (int i = 0; i < _dseInstances.Length; i++)
                    if (_dseInstances[i] != null)
                        _dseInstances[i].Volume = _targetVolumes[i] * _trackVolumeMul[i] * volMul;
            }
        }

        /// 淡出所有音轨：目标音量 0，时长 duration 秒。
        public static void StartFadeOut(float duration)
        {
            if (!IsOnMainThread()) { RunOnMainThread(() => StartFadeOut(duration)); return; }
            if (!IsRunning || _dseInstances.Length == 0) return;
            for (int i = 0; i < _dseInstances.Length; i++)
            {
                // _startVolumes 存的是**纯场景音量**（不含玩家音量/单曲音量）：
                // UpdateCrossfade 会再乘 _trackVolumeMul × MusicManager.getVolume()。
                // 若这里读 DSEI 的实际音量（已含两者），淡出会变成“音量平方”（既有问题，顺手修正）。
                _startVolumes[i] = _targetVolumes[i];
                _targetVolumes[i] = 0f;
            }
            _targetFadeDuration = duration;
            _fadeProgress = 0f;
            _isFading = true;
        }

        public static void SwitchToScene(int targetScene, float? fadeDurationOverride = null, string overrideTheme = null, bool immediate = false)
        {
            if (!IsOnMainThread())
            {
                RunOnMainThread(() => SwitchToScene(targetScene, fadeDurationOverride, overrideTheme, immediate));
                return;
            }
            if (Config == null || targetScene < 0 || targetScene >= Config.Scenes.Count || targetScene == CurrentScene) return;
            SaveCurrentSceneDiscovery();
            DisconnectIfOnControlledNode();

            if (UseDualTrack && _dseInstances.Length > 0)
            {
                if (immediate)
                {
                    // 无过渡直切：直接设音量，不启动交叉淡化
                    for (int i = 0; i < _dseInstances.Length; i++)
                    {
                        float sceneVol = (i == targetScene) ? 1f : 0f;
                        if (_dseInstances[i] != null)
                            _dseInstances[i].Volume = sceneVol * _trackVolumeMul[i] * MusicManager.getVolume();
                        // 必须同步 _targetVolumes：SyncVolume() 每帧用它覆盖 DSEI 音量，
                        // 只设 Volume 会在下一帧被覆盖回 0（LoadMusicPhase 初始化时全 0）→ 静音
                        _targetVolumes[i] = sceneVol;
                        _startVolumes[i] = sceneVol;
                    }
                    _isFading = false;
                }
                else
                {
                    for (int i = 0; i < _dseInstances.Length; i++)
                    {
                        _startVolumes[i] = _targetVolumes[i];   // 纯场景音量，见 StartFadeOut 注释
                        _targetVolumes[i] = (i == targetScene) ? 1f : 0f;
                    }
                    float dur = fadeDurationOverride ?? Config.DefaultFadeDuration;
                    _targetFadeDuration = dur;
                    _fadeProgress = 0f;
                    _isFading = true;
                }
            }

            string theme = overrideTheme ?? Config.Scenes[targetScene].Theme;
            if (ConfigValue.IsNone(theme)) theme = DefaultTheme;
            if (!ConfigValue.IsNone(theme))
            {
                if (!Config.ChangeLayout) PhaseSwiftLayoutPatch.SkipLayoutChange(immediate ? 0.05f : Config.ThemeFlickerDuration + 0.15f);
                if (Enum.TryParse<OSTheme>(theme, true, out OSTheme themeEnum))
                {
                    if (immediate)
                    {
                        // 无过渡直切：不走 StartThemeSwitch 闪烁动画
                        ThemeManager.switchTheme(CurrentOS, themeEnum);
                        ThemeManager.setThemeOnComputer(CurrentOS.thisComputer, themeEnum);
                    }
                    else
                    {
                        CurrentOS.EffectsUpdater.StartThemeSwitch(Config.ThemeFlickerDuration, themeEnum, CurrentOS, null);
                        ThemeManager.setThemeOnComputer(CurrentOS.thisComputer, themeEnum);
                    }
                }
                else
                {
                    // 自定义主题：直接传相对路径，由 ThemeManager 自行拼接扩展根目录。
                    // 之前先拼 ExtensionRoot 会产生双前缀，导致主题加载失败、
                    // x-server.sys 持久化后读档恢复失败（getThemeForDataString 解密+拼接 → TerminalOnlyBlack）
                    if (immediate)
                    {
                        // 无过渡直切：不走 StartThemeSwitch 闪烁动画
                        ThemeManager.switchTheme(CurrentOS, theme);
                        ThemeManager.setThemeOnComputer(CurrentOS.thisComputer, theme);
                    }
                    else
                    {
                        CurrentOS.EffectsUpdater.StartThemeSwitch(Config.ThemeFlickerDuration, OSTheme.Custom, CurrentOS, theme);
                        ThemeManager.setThemeOnComputer(CurrentOS.thisComputer, theme);
                    }
                }
                if (Config.ChangeLayout && !immediate)
                {
                    // ChangeLayout=true: 等待主题闪烁完成后才应用拓扑/可见性，避免特效与切换重叠
                    CurrentOS.delayer.Post(ActionDelayer.Wait(Config.ThemeFlickerDuration), () =>
                    {
                        ApplyTopology(targetScene);
                        UpdateVisibility(targetScene);
                        var onSwitch = Config.Scenes[targetScene].OnSwitch;
                        if (onSwitch != null && !ConfigValue.IsNone(onSwitch.FilePath))
                            ActionHelper.ExecuteActionFile(CurrentOS, onSwitch.FilePath, ExtensionRoot);
                    });
                    CurrentScene = targetScene;
                    return;
                }
                // immediate + ChangeLayout=true：无闪烁，直接落入下方同步 ApplyTopology/UpdateVisibility
            }

            ApplyTopology(targetScene);
            UpdateVisibility(targetScene);

            var onSwitch = Config.Scenes[targetScene].OnSwitch;
            if (onSwitch != null && !ConfigValue.IsNone(onSwitch.FilePath))
                ActionHelper.ExecuteActionFile(CurrentOS, onSwitch.FilePath, ExtensionRoot);

            CurrentScene = targetScene;
        }

        public static void SwitchMusicPhase(int phaseId)
        {
            if (!IsOnMainThread()) { RunOnMainThread(() => SwitchMusicPhase(phaseId)); return; }
            if (Config == null) return;
            var phase = Config.MusicPhases.FirstOrDefault(p => p.Id == phaseId);
            if (phase == null && phaseId >= 0 && phaseId < Config.MusicPhases.Count)
                phase = Config.MusicPhases[phaseId];
            if (phase == null) return;
            // 切换前快速淡化，减少刺耳声
            for (int i = 0; i < _dseInstances.Length; i++)
            {
                if (_dseInstances[i] != null)
                    _dseInstances[i].Volume = 0f;
            }
            CurrentMusicPhase = phaseId;
            LoadMusicPhase(phase);
        }

        public static void UpdateVisualization()
        {
            // 每次 GetVisualizationData 调用时触发，~24fps
            if (_visSampList == null) _visSampList = new List<float>(new float[256]);
            if (CurrentVisBands.Length != 256) CurrentVisBands = new float[256];

            // C1：只读“当前能听到那条轨”的缓冲（双轨时两轨各自独立）
            int track = CurrentScene;
            if (track < 0 || track >= _rollingBufs.Length) track = 0;
            if (track < 0 || track >= _rollingBufs.Length) return;
            float[] buf = _rollingBufs[track];
            if (buf == null) return;
            int bufSize = buf.Length;
            if (bufSize == 0 || _rollingBufCount[track] < 256) return;

            // C2：取样基准 = “已播放位置”，而不是“最新提交位置”。
            //     队列里的 pending 块是“已提交但还没播”的量，从写入位置往回退掉它才是真实播放头。
            // C3：Pitch 变速时消费更快、pending 下降更快，故该基准自动跟随变速（无需单独补偿）。
            int pending = 0;
            if (_dseInstances != null && track < _dseInstances.Length && _dseInstances[track] != null)
                pending = _dseInstances[track].PendingBufferCount;
            int chunkFrames = (track < _chunkFrames.Length && _chunkFrames[track] > 0) ? _chunkFrames[track] : 1;
            long behindFrames = (long)pending * chunkFrames;
            int maxBack = Math.Max(0, _rollingBufCount[track] - 256);
            if (behindFrames > maxBack) behindFrames = maxBack;
            int headPos = (int)(((_rollingBufPos[track] - behindFrames) % bufSize + bufSize) % bufSize);

            // C4：步进采样，让 256 个点铺满 ~1/60 秒（对齐原版），窗口不再随采样率收缩。
            //     15360 = 256 × 60；跳距随采样率缩放，高采样率时不会只取到几个毫秒。
            int sr = (track < _trackSampleRate.Length && _trackSampleRate[track] > 0) ? _trackSampleRate[track] : 44100;
            int step = (sr + 7680) / 15360;      // 四舍五入（+半跳距）；@44.1k→3、@48k→3、@96k→6
            if (step < 1) step = 1;
            while (step > 1 && 256 * step > bufSize) step--;      // 不能超出缓冲长度
            int span = 256 * step;
            if (span > bufSize) span = bufSize;
            int startPos = (int)(((headPos - span) % bufSize + bufSize) % bufSize);

            // 音量联动：FadeOut/交叉淡化时可视化同步衰减
            float visVolume = 1f;
            if (_dseInstances != null && track < _dseInstances.Length && _dseInstances[track] != null)
                visVolume = _dseInstances[track].Volume;

            for (int i = 0; i < 256; i++)
            {
                int srcIdx = (startPos + i * step) % bufSize;
                float val = Math.Abs(buf[srcIdx]) * visVolume;
                if (val > 1f) val = 1f;
                _visSampList[i] = val;
                CurrentVisBands[i] = val;
            }
        }
        private static List<float> _visSampList;

        public static HashSet<string> GetControlledNodeIds() { return _controlledNodeIds; }

        public static bool IsNodeAllowed(string id)
        {
            if (_controlledNodeIds.Contains(id))
            {
                bool inScene = _sceneVisibleIds[CurrentScene].Contains(id);
                bool notBlocked = !_sceneBlockedIds[CurrentScene].Contains(id);
                // 运行时黑名单同样拦截连接
                if (_runtimeBlockedNodeIds.TryGetValue(CurrentScene, out var runtimeBlocked)
                    && runtimeBlocked.Contains(id))
                {
                    notBlocked = false;
                }
                return inScene && notBlocked;
            }
            return true;
        }

        private static void LoadMusicPhase(PhaseSwiftMusicPhase phase)
        {
            CleanupAudio();
            if (phase.Tracks.Count == 0) return;

            _stopped = true;
            string root = ExtensionRoot ?? "";
            int trackCount = Math.Max(1, phase.Tracks.Count);
            _dseInstances = new DynamicSoundEffectInstance[trackCount];
            _trackStreams = new FileStream[trackCount];
            _trackReaders = new VorbisReader[trackCount];
            _trackChannels = new int[trackCount];
            _startVolumes = new float[trackCount];
            _targetVolumes = new float[trackCount];
            int created = 0;   // 诊断（D3）
            _framesPlayed = new long[trackCount];
            _loopStartFrames = new long[trackCount];
            _loopEndFrames = new long[trackCount];
            _trackVolumeMul = new float[trackCount];
            _chunkBuf = new float[trackCount][];
            _rollingBufs = new float[trackCount][];
            _rollingBufPos = new int[trackCount];
            _rollingBufCount = new int[trackCount];
            _trackSampleRate = new int[trackCount];
            _chunkFrames = new int[trackCount];
            for (int i = 0; i < trackCount; i++)
            {
                try
                {
                    // 解析文件路径：先按配置中的相对路径，再回退到文件名在 Music/ 下查找
                    // （两步均在扩展目录内，越界一律不予考虑）
                    var meta = phase.Tracks[i];
                    string relPath = (meta.Path ?? "").Replace('\\', '/');
                    string filePath = KEPath.ResolveInside(relPath, root);
                    if (filePath == null || !File.Exists(filePath))
                    {
                        // 回退：只取文件名，在 root/Music 下搜索
                        string fileName = Path.GetFileName(relPath);
                        string altPath = KEPath.ResolveInside("Music/" + fileName, root);
                        if (altPath != null && File.Exists(altPath))
                            filePath = altPath;
                        else
                        {
                            KELog.Warn($"[PhaseSwift] 找不到音轨 {i}: {relPath}（已尝试 Music/{fileName}）");
                            continue;
                        }
                    }
                    _trackStreams[i] = File.OpenRead(filePath);
                    _trackReaders[i] = new VorbisReader(_trackStreams[i], false);
                    int sr = _trackReaders[i].SampleRate;
                    int ch = _trackReaders[i].Channels;
                    _trackChannels[i] = ch;
                    // NVorbis 的 TotalSamples 是 **per-channel 帧数**（实测 == TotalTime × SampleRate）
                    long totalFrames = _trackReaders[i].TotalSamples;

                    // —— 循环点解析：未写/空/非数字/负数均在 PhaseSwiftTrack 内回退默认（见该类注释）——
                    float? loopStartSec = meta.LoopStartSeconds;
                    float? loopEndSec = meta.LoopEndSeconds;
                    long loopStart = loopStartSec.HasValue ? (long)(loopStartSec.Value * sr) : 0L;
                    long loopEnd = loopEndSec.HasValue ? (long)(loopEndSec.Value * sr) : totalFrames;
                    if (loopStart < 0L) loopStart = 0L;
                    if (loopEnd > totalFrames) loopEnd = totalFrames;
                    if (loopEnd <= loopStart)
                    {
                        KELog.Warn($"[PhaseSwift] 音轨 {i} 循环区间非法（LoopStart={meta.LoopStart}, LoopEnd={meta.LoopEnd}，"
                            + $"总长 {totalFrames / (double)sr:F2}s），回退整曲循环");
                        loopStart = 0L;
                        loopEnd = totalFrames;
                    }
                    // —— 尾部危险区：seek 到距文件末尾约 0.5 秒内必失败（实测边界），保守取 1 秒。
                    //    装载时就拦掉，比等到播完回跳时才降级更早、提示也更明确。
                    else if (loopStart > 0L && totalFrames - loopStart < sr)
                    {
                        KELog.Warn($"[PhaseSwift] 音轨 {i} 的 LoopStart 距文件末尾不足 1 秒"
                            + $"（总长 {totalFrames / (double)sr:F2}s）——seek 会失败，该轨回退整曲循环");
                        loopStart = 0L;
                        loopEnd = totalFrames;
                    }
                    _loopStartFrames[i] = loopStart;
                    _loopEndFrames[i] = loopEnd;

                    // —— 单曲音量（A8）：参与最终的音量乘法，而不是直写 DSEI.Volume ——
                    _trackVolumeMul[i] = meta.VolumeMultiplier ?? 1f;

                    // 按轨初始化滚动缓冲（~500ms mono）——C1：每轨一份，波形条按当前场景轨取数
                    _rollingBufs[i] = new float[Math.Max(1024, sr / 2)];
                    _rollingBufPos[i] = 0;
                    _rollingBufCount[i] = 0;
                    _trackSampleRate[i] = sr;

                    // —— D1：一块的 interleaved 样本数，按轨复用缓冲 ——
                    int chunkSamples = (sr * ch) / 24;
                    if (ch > 0 && chunkSamples % ch != 0) chunkSamples -= chunkSamples % ch;
                    if (chunkSamples < ch) chunkSamples = ch;
                    _chunkBuf[i] = new float[chunkSamples];
                    _chunkFrames[i] = chunkSamples / ch;   // C2：队列深度 → 帧数换算用

                    AudioChannels audioCh = (ch >= 2) ? AudioChannels.Stereo : AudioChannels.Mono;
                    _dseInstances[i] = new DynamicSoundEffectInstance(sr, audioCh);
                    created++;   // 诊断（D3）
                    _dseInstances[i].BufferNeeded += OnBufferNeeded;
                    // 同步场景音量（A8）：SwitchMusicPhase 路径不经过 SwitchToScene，
                    // 若这里不同步 _targetVolumes，SyncVolume() 下一帧就把音量覆盖回 0 → 切音乐组后静音。
                    float sceneVol = (i == CurrentScene) ? 1f : 0f;
                    _targetVolumes[i] = sceneVol;
                    _startVolumes[i] = sceneVol;
                    _dseInstances[i].Volume = sceneVol * _trackVolumeMul[i];

                    // —— A7：Pitch（PhaseSwiftTrack 内已解析并夹到 [-1, 1]）——
                    float pitch = meta.PitchValue;
                    if (pitch != 0f) _dseInstances[i].Pitch = pitch;

                    _dseInstances[i].Play();
                    _stopped = false;
                    // 起播位置：从文件开头播（SASS 语义）——
                    // 先播 0~LoopEnd，之后才在 [LoopStart, LoopEnd] 区间内循环；
                    // 即开头部分会完整播一次，而不是直接跳到 LoopStart。
                    _framesPlayed[i] = 0L;
                    for (int b = 0; b < TargetPendingBuffers; b++) SubmitNextChunk(i);
                }
                catch (Exception ex)
                {
                    KELog.Error($"[PhaseSwift] 加载音轨 {i} 失败: {ex.Message}");
                    if (_trackStreams[i] != null) { _trackStreams[i].Dispose(); _trackStreams[i] = null; }
                }
            }
            _isFading = false;
            // 诊断（D3 压测）：配合 CleanupAudio 的日志，可看出反复 Load/Cleanup 后池的累积曲线
            KELog.Debug($"[PhaseSwift/diag] LoadMusicPhase: 新建 {created}/{trackCount} 个播放器；"
                + $"FNA 动态池 {GetDynamicPoolCount()}；线程 {Thread.CurrentThread.ManagedThreadId}");
        }


        /// <summary>
        /// 向指定音轨的播放队列补一块。一块 = 1/24 秒（interleaved 样本数 = 采样率 × 声道 / 24）。
        ///
        /// 与旧实现的区别（9.38）：旧版“读一整块，读不够就回文件头”，只能在文件末尾循环；
        /// 新版主动用“离循环终点的剩余量”约束本次读取量，到界就跳回循环起点，
        /// 因此支持文件中段的循环区间，且一块内可跳（甚至多次跳）循环点。
        /// </summary>
        private static void SubmitNextChunk(int trackIdx)
        {
            if (_stopped) return;
            if (trackIdx < 0 || trackIdx >= _trackReaders.Length) return;
            if (_trackReaders[trackIdx] == null) return;
            int ch = _trackChannels[trackIdx];
            if (ch <= 0) return;
            float[] buf = _chunkBuf[trackIdx];
            if (buf == null) return;

            int wantFrames = buf.Length / ch;
            int filled = 0;
            int guard = 0;      // 防御：循环区间极短时一块内可多次回跳，设上限避免死循环

            try
            {
                while (filled < wantFrames && !_stopped)
                {
                    if (++guard > 64) break;

                    // ⚠️ 每轮必须重新取 reader：SeekToFrame / DegradeToWholeTrack 会 Dispose 并替换它
                    //    （frame<=0 的分支就是“重建 reader”）。若像之前那样在循环外捕获一次，
                    //    整曲循环回到起点后下一轮就会读到已释放对象 → ObjectDisposedException。
                    var reader = _trackReaders[trackIdx];
                    if (reader == null) break;

                    long remaining = _loopEndFrames[trackIdx] - _framesPlayed[trackIdx];
                    if (remaining <= 0L)
                    {
                        // 到达循环终点 → 跳回起点
                        if (!SeekToFrame(trackIdx, _loopStartFrames[trackIdx])) continue;   // 降级后 loop 已重置，重算 remaining
                        _framesPlayed[trackIdx] = _loopStartFrames[trackIdx];
                        continue;
                    }
                    int take = (int)Math.Min((long)(wantFrames - filled), remaining);
                    int got = reader.ReadSamples(buf, filled * ch, take * ch);
                    if (got <= 0)
                    {
                        // 读不动（EOF 与 TotalSamples 不符等异常）→ 也回跳一次，失败则退出
                        if (!SeekToFrame(trackIdx, _loopStartFrames[trackIdx])) break;
                        _framesPlayed[trackIdx] = _loopStartFrames[trackIdx];
                        continue;
                    }
                    filled += got / ch;
                    _framesPlayed[trackIdx] += got / ch;
                }
            }
            catch (Exception ex)
            {
                // 不让解码异常冒到游戏顶层（会直接崩游戏）。记 Error 并放弃本次补给，下一帧会再试。
                // 用完整 ToString（含堆栈）而非 Message：配合 Windows PDB 可直接看到源文件行号。
                KELog.Error($"[PhaseSwift] 音轨 {trackIdx} 读取失败: {ex}");
                return;
            }

            if (filled <= 0) return;
            int written = filled * ch;

            // 写入该轨自己的滚动缓冲（取第 0 声道）——C1：按轨分离，避免两轨互相覆盖
            float[] rbuf = (trackIdx < _rollingBufs.Length) ? _rollingBufs[trackIdx] : null;
            if (rbuf != null)
            {
                int p = _rollingBufPos[trackIdx];
                for (int j = 0; j < written; j += ch)
                {
                    rbuf[p] = buf[j];
                    p = (p + 1) % rbuf.Length;
                }
                _rollingBufPos[trackIdx] = p;
                _rollingBufCount[trackIdx] = Math.Min(rbuf.Length, _rollingBufCount[trackIdx] + filled);
            }

            // 从滚动缓冲的实时采样交给 UpdateVisualization (注入器触发)
            // 这里只更新 LastBandUpdateTime 标记，用于检测是否有新数据
            CurrentVisBands = Array.Empty<float>();
            LastBandUpdateTime = DateTime.UtcNow;

            // D2：直接提交 float（FNA 扩展方法，免去 float→int16→byte 的转换与临时数组）。
            // count 语义为 interleaved 样本数（见 FNA OpenALDevice.SetBufferFloatData：count * 4 字节）。
            try
            {
                _dseInstances[trackIdx].SubmitFloatBufferEXT(buf, 0, written);
            }
            catch (Exception ex_)
            {
                KELog.Error($"[PhaseSwift] SubmitBuffer error: {ex_.Message}");
            }
        }

        /// <summary>
        /// 把音轨读取位置跳到指定帧（per-channel frame）。
        /// 回到 0 用“重建 reader”的旧路径（已验证稳定）；其他位置用 SamplePosition setter。
        /// 失败（如 seek 到距文件尾 0.5 秒内触发 NVorbis 的 GranulePos 校验）则降级为整曲循环，返回 false。
        /// </summary>
        private static bool SeekToFrame(int trackIdx, long frame)
        {
            try
            {
                if (frame <= 0L)
                {
                    _trackStreams[trackIdx].Position = 0;
                    _trackReaders[trackIdx].Dispose();
                    _trackReaders[trackIdx] = new VorbisReader(_trackStreams[trackIdx], false);
                    return true;
                }
                _trackReaders[trackIdx].SamplePosition = frame;
                return true;
            }
            catch (Exception ex)
            {
                KELog.Warn($"[PhaseSwift] 音轨 {trackIdx} 跳到第 {frame} 帧失败（{ex.Message}），该轨降级为整曲循环");
                DegradeToWholeTrack(trackIdx);
                return false;
            }
        }

        /// <summary>seek 失败时的降级：重建 reader 回到文件开头，并把该轨循环范围重置为整曲。</summary>
        private static void DegradeToWholeTrack(int trackIdx)
        {
            try
            {
                _trackStreams[trackIdx].Position = 0;
                _trackReaders[trackIdx]?.Dispose();
                _trackReaders[trackIdx] = new VorbisReader(_trackStreams[trackIdx], false);
                _loopStartFrames[trackIdx] = 0L;
                _loopEndFrames[trackIdx] = _trackReaders[trackIdx].TotalSamples;
                _framesPlayed[trackIdx] = 0L;
            }
            catch (Exception ex)
            {
                KELog.Error($"[PhaseSwift] 音轨 {trackIdx} 降级失败: {ex.Message}");
                _loopEndFrames[trackIdx] = long.MaxValue;   // 至少避免外层因 remaining<=0 打转
            }
        }

        private static void OnBufferNeeded(object sender, EventArgs e)
        {
            var dsei = sender as DynamicSoundEffectInstance;
            if (dsei == null) return;
            for (int i = 0; i < _dseInstances.Length; i++)
            {
                if (_dseInstances[i] != dsei) continue;
                // B2：事件触发时一口气补到目标值，而不是只补 1 块
                int need = TargetPendingBuffers - dsei.PendingBufferCount;
                for (int b = 0; b < need; b++) SubmitNextChunk(i);
                return;
            }
        }

        private static void CleanupAudio()
        {
            _stopped = true;
            int dropped = 0;
            int poolBefore = GetDynamicPoolCount();   // 诊断（D3）
            for (int i = 0; i < _dseInstances.Length; i++)
            {
                if (_dseInstances[i] != null)
                {
                    _dseInstances[i].BufferNeeded -= OnBufferNeeded;
                    _dseInstances[i].Volume = 0f;   // 先把音量拉到底，避免释放瞬间爆音
                    // 显式释放（D3 遗留项）：Dispose 会释放 AL source、把实例移出 FNA 的
                    // DynamicInstancePool，并逐个删除三个 buffer 队列里的 AL buffer。
                    // 仅 Stop() 只能做到前两项，AL buffer 仍会泄漏。
                    //
                    // 为何现在才敢调：早前「不调 Stop/Dispose」是为了回避切歌卡死，
                    // 但那时的前提是**确实存在跨线程竞态**（Action 线程在动 FNA 内部状态）。
                    // 竞态已由主线程调度修复（af54ce9），且本方法现只在主线程执行，
                    // 与 AudioDevice.Update() 同线程、时序也不重叠 → 无并发风险。
                    // 不释放的代价：每帧遍历变慢，且 AL source 耗尽（默认 256 个）后新歌无法播放。
                    _dseInstances[i].Dispose();
                    _dseInstances[i] = null;
                    dropped++;
                }
                if (i < _trackReaders.Length && _trackReaders[i] != null) { _trackReaders[i].Dispose(); _trackReaders[i] = null; }
                if (i < _trackStreams.Length && _trackStreams[i] != null) { _trackStreams[i].Dispose(); _trackStreams[i] = null; }
            }
            _dseInstances = Array.Empty<DynamicSoundEffectInstance>();
            _trackReaders = Array.Empty<VorbisReader>();
            _trackStreams = Array.Empty<FileStream>();
            _trackChannels = Array.Empty<int>();
            _startVolumes = Array.Empty<float>();
            _targetVolumes = Array.Empty<float>();
            _framesPlayed = Array.Empty<long>();
            _loopStartFrames = Array.Empty<long>();
            _loopEndFrames = Array.Empty<long>();
            _trackVolumeMul = Array.Empty<float>();
            _chunkBuf = Array.Empty<float[]>();
            _isFading = false;
            // 诊断（D3 压测）：掉引用的播放器**不会**离开 FNA 的池（只有 Stop() 才会），
            // 所以池 “只增不减” 就是泄漏的直接证据。
            KELog.Debug($"[PhaseSwift/diag] CleanupAudio: 丢弃 {dropped} 个播放器（未关闭）；"
                + $"FNA 动态池 {poolBefore} → {GetDynamicPoolCount()}；线程 {Thread.CurrentThread.ManagedThreadId}");
        }


        private static void ApplyScene(int sceneIdx, bool force = false)
        {
            ApplyTopology(sceneIdx);
            UpdateVisibility(sceneIdx);
        }

        private static void ApplyTopology(int sceneIdx)
        {
            RemoveControlledLinks();
            foreach (var link in Config.Scenes[sceneIdx].Topology)
                AddLink(link.From, link.To);
        }

        /// <summary>清除受控节点「之间」的全部链接（受控节点与非受控节点的连接不受影响）。</summary>
        private static void RemoveControlledLinks()
        {
            foreach (var id in _controlledNodeIds)
            {
                var comp = Programs.getComputer(CurrentOS, id);
                if (comp == null) continue;
                comp.links.RemoveAll(idx =>
                {
                    if (idx < 0 || idx >= CurrentOS.netMap.nodes.Count) return false;
                    return _controlledNodeIds.Contains(CurrentOS.netMap.nodes[idx].idName);
                });
            }
        }

        /// <summary>添加一条有向链接（节点缺失或链接已存在则跳过）。</summary>
        private static void AddLink(string from, string to)
        {
            var fromComp = Programs.getComputer(CurrentOS, from);
            var toComp = Programs.getComputer(CurrentOS, to);
            if (fromComp == null || toComp == null) return;
            int toIndex = CurrentOS.netMap.nodes.IndexOf(toComp);
            if (toIndex >= 0 && !fromComp.links.Contains(toIndex))
                fromComp.links.Add(toIndex);
        }

        /// <summary>
        /// Stop 时的拓扑处理（来源：Config.TopologyMode 或 PhaseSwiftStop 的 TopologyMode 参数）：
        ///   restore / 空 / 未知值 — 恢复 PS 启动时备份的原始链接
        ///   scene_N               — 恢复原始链接后再叠加场景 N 的 Topology（与运行期切到该场景的语义一致）
        ///   merge                 — 清除受控节点间链接，再叠加全部场景的 Topology（按 from 到 to 单向去重）
        /// 无效或未知值一律回退 restore 并补 Warn，不静默。
        /// </summary>
        private static void ApplyTopologyMode(string mode)
        {
            if (string.IsNullOrEmpty(mode) || mode.Equals("restore", StringComparison.OrdinalIgnoreCase))
            {
                RestoreOriginalLinks();
                return;
            }

            if (mode.StartsWith("scene_", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(mode.Substring(6), out int sceneIdx) && sceneIdx >= 0 && sceneIdx < Config.Scenes.Count)
                {
                    RestoreOriginalLinks();
                    ApplyTopology(sceneIdx);
                }
                else
                {
                    KELog.Warn($"[PhaseSwift] TopologyMode 场景索引无效：{mode}（有效范围 0~{Config.Scenes.Count - 1}），回退 restore");
                    RestoreOriginalLinks();
                }
                return;
            }

            if (mode.Equals("merge", StringComparison.OrdinalIgnoreCase))
            {
                ApplyMergedTopology();
                return;
            }

            KELog.Warn($"[PhaseSwift] TopologyMode 未知：{mode}，回退 restore");
            RestoreOriginalLinks();
        }

        /// <summary>
        /// 合并全部场景的 Topology 后应用（按 from 到 to 单向去重）。
        /// 注意：会叠加后续场景的路径，可能让玩家提前抵达尚未经历的场景 ——
        /// 建议在「剧情线全部走完、交还自由探索」时使用。
        /// </summary>
        private static void ApplyMergedTopology()
        {
            RemoveControlledLinks();
            var seen = new HashSet<(string From, string To)>();
            foreach (var scene in Config.Scenes)
            {
                foreach (var link in scene.Topology)
                {
                    if (seen.Add((link.From, link.To)))
                        AddLink(link.From, link.To);
                }
            }
        }

        private static void UpdateVisibility(int sceneIdx)
        {
            foreach (var id in _controlledNodeIds)
            {
                var comp = Programs.getComputer(CurrentOS, id);
                if (comp == null) continue;
                int idx = CurrentOS.netMap.nodes.IndexOf(comp);
                if (CurrentOS.netMap.visibleNodes.Contains(idx))
                    CurrentOS.netMap.visibleNodes.Remove(idx);
            }
            var nodesToShow = new HashSet<string>(_sceneStartIds[sceneIdx]);
            if (_sceneDiscoveredNodeIds.TryGetValue(sceneIdx, out var prev))
                foreach (var id in prev) nodesToShow.Add(id);
            // GlobalDiscovery — 其他场景发现的节点如果本场景 VisibleNodes 也含有，一并显示
            if (Config != null && Config.GlobalDiscovery)
            {
                foreach (var kv in _sceneDiscoveredNodeIds)
                {
                    if (kv.Key == sceneIdx) continue;
                    foreach (var id in kv.Value)
                    {
                        if (_sceneVisibleIds[sceneIdx].Contains(id))
                            nodesToShow.Add(id);
                    }
                }
            }
            // 从 nodesToShow 中排除运行时黑名单中的节点
            if (_runtimeBlockedNodeIds.TryGetValue(sceneIdx, out var blocked))
            {
                foreach (var id in blocked)
                    nodesToShow.Remove(id);
            }
            foreach (var id in nodesToShow)
            {
                var comp = Programs.getComputer(CurrentOS, id);
                if (comp == null) continue;
                int idx = CurrentOS.netMap.nodes.IndexOf(comp);
                if (!CurrentOS.netMap.visibleNodes.Contains(idx))
                    CurrentOS.netMap.visibleNodes.Add(idx);
                if (idx >= 0 && idx < CurrentOS.netMap.nodes.Count)
                {
                    var node = CurrentOS.netMap.nodes[idx];
                    node.highlightFlashTime = 1f;
                    SFX.addCircle(node.getScreenSpacePosition(), Utils.AddativeWhite * 0.4f, 70f);
                }
            }
            _currentVisibleNodeIds = _sceneStartIds[sceneIdx].ToHashSet();
        }

        public static void OverrideOriginalLinks(Dictionary<string, List<string>> linkIds, OS os)
        {
            _originalLinks.Clear();
            foreach (var kv in linkIds)
            {
                var comp = Programs.getComputer(os, kv.Key);
                if (comp == null) continue;
                var indices = new List<int>();
                foreach (var targetId in kv.Value)
                {
                    var targetComp = Programs.getComputer(os, targetId);
                    if (targetComp == null) continue;
                    int idx = os.netMap.nodes.IndexOf(targetComp);
                    if (idx >= 0) indices.Add(idx);
                }
                _originalLinks[kv.Key] = indices;
            }
        }

        public static void BlockNode(string nodeId, int sceneIndex = -1)
        {
            int idx = sceneIndex >= 0 ? sceneIndex : CurrentScene;
            if (Config == null || idx < 0) return;
            if (!_runtimeBlockedNodeIds.TryGetValue(idx, out var set))
            {
                set = new HashSet<string>();
                _runtimeBlockedNodeIds[idx] = set;
            }
            set.Add(nodeId);
            // 若节点在当前场景，立即从地图隐藏（无需等下次切场景 UpdateVisibility）
            if (idx == CurrentScene)
                HideNodeIfCurrentScene(nodeId);
        }

        /// <summary>
        /// 若目标节点属于当前场景，立即从地图上隐藏。
        /// </summary>
        private static void HideNodeIfCurrentScene(string nodeId)
        {
            if (CurrentOS?.netMap == null) return;
            var comp = Programs.getComputer(CurrentOS, nodeId);
            if (comp == null) return;
            int idx = CurrentOS.netMap.nodes.IndexOf(comp);
            if (idx >= 0 && CurrentOS.netMap.visibleNodes.Contains(idx))
                CurrentOS.netMap.visibleNodes.Remove(idx);
        }

        public static void UnblockNode(string nodeId, int sceneIndex = -1)
        {
            int idx = sceneIndex >= 0 ? sceneIndex : CurrentScene;
            if (_runtimeBlockedNodeIds.TryGetValue(idx, out var set))
                set.Remove(nodeId);
        }

        /// <summary>
        /// 诊断（D3 压测）：反射读 FNA 的动态音频实例池大小。
        /// 池里只增不减就是泄漏的直接证据——`CleanupAudio` 只丢掉引用、不调 Stop，
        /// 而 FNA 仅靠 `Stop()` 才会把实例移出池（其自带的“状态==Stopped”清理对 DSEI 永不成立）。
        /// 拿不到字段时返回 -1（不同 FNA 版本/裁剪），不影响功能。
        /// </summary>
        private static int GetDynamicPoolCount()
        {
            try
            {
                if (_dynamicPoolField == null)
                {
                    var audioDevice = typeof(DynamicSoundEffectInstance).Assembly
                        .GetType("Microsoft.Xna.Framework.Audio.AudioDevice");
                    _dynamicPoolField = audioDevice?.GetField("DynamicInstancePool",
                        BindingFlags.Public | BindingFlags.Static);
                    if (_dynamicPoolField == null) return -1;
                }
                return (_dynamicPoolField.GetValue(null) as System.Collections.ICollection)?.Count ?? -1;
            }
            catch { return -1; }
        }
        private static FieldInfo _dynamicPoolField;
        /// <summary>诊断（D3）：最近一次记录到的 UpdateAudioBuffers 线程 ID（-1 = 尚未记录）。</summary>
        private static int _audioUpdateThreadId = -1;

        // ——————————————————————————————————————————————————————————————
        // 主线程调度（D3 修复）
        //
        // 实测根因：Hacknet 的 `loadactions` 会为命令**另起线程**，于是
        //   Action 线程（如 4）→ 本类 → FNA 音频 API（new DSEI / Play→GenSource /
        //   SubmitBuffer→GenBuffer+QueueSourceBuffer）
        // 与
        //   主线程（如 1）→ AudioDevice.Update() → 遍历 DynamicInstancePool / DSEI.Update()
        //   → 主线程还有 UpdateAudioBuffers 在读写同一批 KE 数组
        // 并发。FNA 的 DynamicInstancePool（List）与 DSEI 的 queuedBuffers（Queue）
        // 都不是线程安全的 → 内部状态损坏 → 无异常无堆栈地卡死（伴随一声“刺啦”）。
        //
        // 锁挡不住 FNA 内部状态，所以改为：碰音频 API 的入口一律调到主线程执行。
        // 采用**同步等待**（而非异步排队），以保持 Action 的原有语义：
        // Action 返回时效果已生效。
        // ——————————————————————————————————————————————————————————————

        /// <summary>主线程 ID（由 UpdateAudioBuffers 在 OS.Update 中首次运行时记录；-1 = 未知）。</summary>
        private static int _mainThreadId = -1;
        private static readonly ConcurrentQueue<Action> _mainThreadQueue = new();
        private const int MainThreadDispatchTimeoutMs = 5000;

        /// <summary>当前是否处在主线程（主线程未知时视为 true，避免初始化极早期死锁）。</summary>
        private static bool IsOnMainThread()
            => _mainThreadId < 0 || Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        /// <summary>
        /// 确保工作在**主线程**执行：已在主线程则直接执行；否则入队并同步等待。
        /// 入口方法统一用「<c>if (!IsOnMainThread()) { RunOnMainThread(() =&gt; 方法(参数)); return; }</c>」
        /// 的递归写法包装（第二次进入时已在主线程，直接跑原件）。
        /// </summary>
        private static void RunOnMainThread(Action work)
        {
            if (work == null) return;
            if (IsOnMainThread()) { work(); return; }

            Exception captured = null;
            using var done = new ManualResetEventSlim(false);
            _mainThreadQueue.Enqueue(() =>
            {
                try { work(); }
                catch (Exception ex) { captured = ex; }
                finally { done.Set(); }
            });

            if (!done.Wait(MainThreadDispatchTimeoutMs))
            {
                KELog.Warn($"[PhaseSwift] 主线程调度超时（{MainThreadDispatchTimeoutMs}ms），操作可能未执行");
                return;
            }
            if (captured != null)
                KELog.Error($"[PhaseSwift] 主线程执行出错: {captured}");
        }

        /// <summary>主线程侧执行排队的工作（由 OS.Update Postfix 每帧调用，在 UpdateAudioBuffers 之前）。</summary>
        internal static void DrainMainThreadQueue()
        {
            while (_mainThreadQueue.TryDequeue(out var work))
            {
                try { work(); }
                catch (Exception ex) { KELog.Error($"[PhaseSwift] 排队操作执行出错: {ex}"); }
            }
        }

        private static void SyncVolume()
        {
            if (_dseInstances == null || _dseInstances.Length == 0) return;
            float volMul = MusicManager.getVolume();
            for (int i = 0; i < _dseInstances.Length; i++)
            {
                if (_dseInstances[i] != null)
                    _dseInstances[i].Volume = _targetVolumes[i] * _trackVolumeMul[i] * volMul;
            }
        }

        private static void SaveCurrentSceneDiscovery()
        {
            if (Config == null || CurrentScene < 0 || CurrentScene >= Config.Scenes.Count) return;
            var discovered = new HashSet<string>();
            foreach (var id in _controlledNodeIds)
            {
                if (_sceneStartIds.Count > CurrentScene && _sceneStartIds[CurrentScene].Contains(id))
                    continue;
                var comp = Programs.getComputer(CurrentOS, id);
                if (comp == null) continue;
                int idx = CurrentOS.netMap.nodes.IndexOf(comp);
                if (CurrentOS.netMap.visibleNodes.Contains(idx))
                    discovered.Add(id);
            }
            _sceneDiscoveredNodeIds[CurrentScene] = discovered;
        }

        /// <summary>
        /// 保存前刷新内存中的发现状态与 admin 记录。
        /// 由 OnSaveGame 在写 PhaseSwiftData 前调用。
        /// </summary>
        public static void RefreshPersistentState()
        {
            SaveCurrentSceneDiscovery();
        }

        private static Color ParseColor(string colorStr)
        {
            if (string.IsNullOrEmpty(colorStr)) return Color.Transparent;
            // 优先用 CustomColorPatch 解析动态色（LDTchara/Rainbow/预设名）
            var dynConfig = CustomColorManager.ParseColorString(colorStr);
            if (dynConfig != null)
                return CustomColorManager.CalcColor(dynConfig, OS.currentElapsedTime);
            try { return new Microsoft.Xna.Framework.Design.ColorConverter().ConvertFromString(colorStr) as Color? ?? Color.Transparent; }
            catch { return Color.Transparent; }
        }

        public static void RestorePersistentState(PhaseSwiftPersistentState state)
        {
            if (state == null) return;
            _sceneDiscoveredNodeIds = state.DiscoveredNodes ?? new Dictionary<int, HashSet<string>>();
            _runtimeBlockedNodeIds = state.RuntimeBlocked ?? new Dictionary<int, HashSet<string>>();
            // 恢复音乐组：Start() 里 LoadMusicPhase 用 CurrentMusicPhase 加载对应组
            if (Config != null && state.MusicPhase >= 0 && state.MusicPhase < Config.MusicPhases.Count)
                CurrentMusicPhase = state.MusicPhase;
            if (!string.IsNullOrEmpty(state.Theme))
                DefaultTheme = state.Theme;
        }

        public static Dictionary<int, HashSet<string>> GetSceneDiscoveredNodes() => new(_sceneDiscoveredNodeIds);
        public static Dictionary<string, List<int>> GetOriginalLinks() => new(_originalLinks);
        public static Dictionary<int, HashSet<string>> GetRuntimeBlockedNodes() => new(_runtimeBlockedNodeIds);
    }
}