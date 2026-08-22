using Hacknet;
using Hacknet.Extensions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using KernelExtensions.Configs;
using KernelExtensions.Utility;
using KernelExtensions.Saving;
using KernelExtensions.Patches;
using NVorbis;
using System.Xml.Serialization;

namespace KernelExtensions.Modules
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
        /// <summary>可视化滚动缓冲：~500ms mono PCM，初始化时按采样率自动计算大小</summary>
        private static float[] _rollingBuf;
        private static int _rollingBufPos = 0;
        private static int _rollingBufCount = 0;
        private static int _visOffset = 0;

        private static DynamicSoundEffectInstance[] _dseInstances = Array.Empty<DynamicSoundEffectInstance>();
        private static FileStream[] _trackStreams = Array.Empty<FileStream>();
        private static VorbisReader[] _trackReaders = Array.Empty<VorbisReader>();
        private static int[] _trackChannels = Array.Empty<int>();
        private static volatile bool _stopped;
        private static bool _isFading;
        private static float _fadeProgress, _targetFadeDuration;
        private static float[] _startVolumes = Array.Empty<float>();
        private static float[] _targetVolumes = Array.Empty<float>();

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
            CurrentOS = os;
            if (ExtensionLoader.ActiveExtensionInfo != null)
                ExtensionRoot = ExtensionLoader.ActiveExtensionInfo.FolderPath.Replace("\\", "/");

            string configPath = Path.Combine(ExtensionRoot, "PhaseSwift", configName + ".xml").Replace("\\", "/");
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

        public static void Start(int? overrideScene = null)
        {
            if (!IsInitialized || Config == null || IsRunning) return;

            if (UseDualTrack)
            {
                // 停止 MusicManager 的正在播放，避免与 DSEI 叠加
                if (MusicManager.isPlaying)
                    MusicManager.stop();
                if (Config.MusicPhases.Count > 0)
                    LoadMusicPhase(Config.MusicPhases[CurrentMusicPhase]);
            }
            else
            {
                // 单曲模式：MusicManager 播放指定音乐
                string singleTrack = Config.SingleTrack;
                if (ConfigValue.IsNone(singleTrack) && Config.MusicPhases.Count > 0 && Config.MusicPhases[0].Tracks.Count > 0)
                    singleTrack = Config.MusicPhases[0].Tracks[0];
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

        public static void Stop(string finishMode = "none")
        {
            if (!IsInitialized) return;

            CleanupAudio();
            RestoreOriginalLinks();

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
            _rollingBuf = null;
            _rollingBufCount = 0;
            _rollingBufPos = 0;
            _visOffset = 0;
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
            if (!UseDualTrack) return;
            if (!IsRunning) return;
            SyncVolume();
            for (int i = 0; i < _dseInstances.Length; i++)
            {
                if (_dseInstances[i] != null)
                {
                    try
                    {
                        int pending = _dseInstances[i].PendingBufferCount;
                        int needed = 3 - pending;
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
                    _dseInstances[i].Volume = MathHelper.Lerp(_startVolumes[i], _targetVolumes[i], t) * volMul;
            if (_fadeProgress >= _targetFadeDuration)
            {
                _isFading = false;
                for (int i = 0; i < _dseInstances.Length; i++)
                    if (_dseInstances[i] != null)
                        _dseInstances[i].Volume = _targetVolumes[i] * volMul;
            }
        }

        /// 淡出所有音轨：目标音量 0，时长 duration 秒。
        public static void StartFadeOut(float duration)
        {
            if (!IsRunning || _dseInstances.Length == 0) return;
            for (int i = 0; i < _dseInstances.Length; i++)
            {
                _startVolumes[i] = _dseInstances[i] != null ? _dseInstances[i].Volume : 0f;
                _targetVolumes[i] = 0f;
            }
            _targetFadeDuration = duration;
            _fadeProgress = 0f;
            _isFading = true;
        }

        public static void SwitchToScene(int targetScene, float? fadeDurationOverride = null, string overrideTheme = null, bool immediate = false)
        {
            if (Config == null || targetScene < 0 || targetScene >= Config.Scenes.Count || targetScene == CurrentScene) return;
            SaveCurrentSceneDiscovery();
            if (CurrentOS.terminal != null && CurrentOS.connectedComp != null && CurrentOS.connectedComp != CurrentOS.thisComputer
                && _controlledNodeIds.Contains(CurrentOS.connectedComp.idName))
            {
                CurrentOS.display.command = "dc";
                CurrentOS.connectedComp = null;
                CurrentOS.terminal.writeLine("Connection Lost: Network Changed");
            }

            if (UseDualTrack && _dseInstances.Length > 0)
            {
                if (immediate)
                {
                    // 无过渡直切：直接设音量，不启动交叉淡化
                    for (int i = 0; i < _dseInstances.Length; i++)
                    {
                        if (_dseInstances[i] != null)
                            _dseInstances[i].Volume = (i == targetScene) ? 1f : 0f;
                        // 必须同步 _targetVolumes：SyncVolume() 每帧用它覆盖 DSEI 音量，
                        // 只设 Volume 会在下一帧被覆盖回 0（LoadMusicPhase 初始化时全 0）→ 静音
                        _targetVolumes[i] = (i == targetScene) ? 1f : 0f;
                        _startVolumes[i] = (i == targetScene) ? 1f : 0f;
                    }
                    _isFading = false;
                }
                else
                {
                for (int i = 0; i < _dseInstances.Length; i++)
                {
                    _startVolumes[i] = _dseInstances[i] != null ? _dseInstances[i].Volume : 0f;
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
            // 从滚动缓冲取 256 连续样本，偏移 +1 每帧，模拟播放头推进
            if (_rollingBuf == null || _rollingBufCount < 256 || _visSampList == null)
            {
                if (_visSampList == null) _visSampList = new List<float>(new float[256]);
                return;
            }

            int bufSize = _rollingBuf.Length;
            int basePos = (_rollingBufPos - 256 - _visOffset + bufSize) % bufSize;
            _visOffset = (_visOffset + 1) % 256;

            // 写入 _visSampList 和 CurrentVisBands（供注入器读取）
            // 获取当前场景音轨音量，FadeOut/交叉淡化时可视化同步衰减
            float visVolume = 1f;
            if (UseDualTrack && _dseInstances != null && CurrentScene >= 0 && CurrentScene < _dseInstances.Length)
            {
                var dsei = _dseInstances[CurrentScene];
                if (dsei != null) visVolume = dsei.Volume;
            }

            // 写入 _visSampList 和 CurrentVisBands（供注入器读取）
            if (CurrentVisBands.Length != 256) CurrentVisBands = new float[256];
            for (int i = 0; i < 256; i++)
            {
                int srcIdx = (basePos + i + bufSize) % bufSize;
                float val = Math.Abs(_rollingBuf[srcIdx]) * visVolume;
                _visSampList[i] = Math.Min(1f, Math.Min(1f, val));
                CurrentVisBands[i] = _visSampList[i];
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
                // 9.8: 运行时黑名单同样拦截连接
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
            for (int i = 0; i < trackCount; i++)
            {
                try
                {
                    // 解析文件路径：先按配置中的相对路径，再回退到文件名直接查找
                    string relPath = phase.Tracks[i].Replace('\\', '/');
                    string filePath = Path.Combine(root, relPath);
                    if (!File.Exists(filePath))
                    {
                        // 回退：只取文件名，在 root/Music 下搜索
                        string fileName = Path.GetFileName(relPath);
                        string altPath = Path.Combine(root, "Music", fileName);
                        if (File.Exists(altPath))
                            filePath = altPath;
                        else
                        {
                            KELog.Warn($"[PhaseSwift] 找不到音轨 {i}: {filePath} (已尝试 {altPath})");
                            continue;
                        }
                    }
                    _trackStreams[i] = File.OpenRead(filePath);
                    _trackReaders[i] = new VorbisReader(_trackStreams[i], false);
                    int sr = _trackReaders[i].SampleRate;
                    int ch = _trackReaders[i].Channels;
                    _trackChannels[i] = ch;
                    // 按首轨采样率初始化滚动缓冲 (~500ms mono)
                    if (i == 0 && (_rollingBuf == null || _rollingBuf.Length != sr / 2))
                        _rollingBuf = new float[sr / 2];
                    AudioChannels audioCh = (ch >= 2) ? AudioChannels.Stereo : AudioChannels.Mono;
                    _dseInstances[i] = new DynamicSoundEffectInstance(sr, audioCh);
                    _dseInstances[i].BufferNeeded += OnBufferNeeded;
                    _dseInstances[i].Volume = (i == CurrentScene) ? 1f : 0f;
                    _dseInstances[i].Play();
                    _stopped = false;
                    for (int b = 0; b < 6; b++) SubmitNextChunk(i);
                }
                catch (Exception ex)
                {
                    KELog.Error($"[PhaseSwift] 加载音轨 {i} 失败: {ex.Message}");
                    if (_trackStreams[i] != null) { _trackStreams[i].Dispose(); _trackStreams[i] = null; }
                }
            }
            _isFading = false;
            _visOffset = 0;
        }


        private static void SubmitNextChunk(int trackIdx)
        {
            if (_stopped) return;
            if (trackIdx < 0 || trackIdx >= _trackReaders.Length) return;
            var reader = _trackReaders[trackIdx];
            if (reader == null) return;
            int bufSamples = (reader.SampleRate * _trackChannels[trackIdx]) / 24;
            // 确保帧对齐：立体声必须是偶数个样本
            if (bufSamples % _trackChannels[trackIdx] != 0)
                bufSamples -= bufSamples % _trackChannels[trackIdx];
            float[] floatBuf = new float[bufSamples];
            int read = reader.ReadSamples(floatBuf, 0, bufSamples);
            if (read < bufSamples)
            {
                // 独立循环：音轨播完后重置到文件开头
                // 不清零剩余缓冲，避免不同长度音轨的 seek 偏移积累
                _trackStreams[trackIdx].Position = 0;
                _trackReaders[trackIdx].Dispose();
                _trackReaders[trackIdx] = new VorbisReader(_trackStreams[trackIdx], false);
                // 读取更多样本补足当前帧（从文件开头继续读）
                int more = _trackReaders[trackIdx].ReadSamples(floatBuf, read, bufSamples - read);
                read += more;
            }
            // 写入滚动缓冲（取第 0 声道）
            int ch = _trackChannels[trackIdx];
            for (int j = 0; j < read; j += ch)
            {
                _rollingBuf[_rollingBufPos] = floatBuf[j];
                _rollingBufPos = (_rollingBufPos + 1) % _rollingBuf.Length;
            }
            _rollingBufCount = Math.Min(_rollingBuf.Length, _rollingBufCount + read / ch);

                        // 取最近 256 个连续样本（~5.8ms @ 44.1kHz），还原原版波形
                        // 从滚动缓冲的实时采样交给 UpdateVisualization (注入器触发)
            // 这里只更新 LastBandUpdateTime 标记，用于检测是否有新数据
            CurrentVisBands = Array.Empty<float>();
            LastBandUpdateTime = DateTime.UtcNow;
            // SubmitBuffer(byte[]) 是 XNA 标准方法，全采样率兼容
            // 将 float PCM 转为 16-bit PCM 字节
            try
            {
                byte[] pcm16 = new byte[read * 2];
                for (int s = 0; s < read; s++)
                {
                    float clamped = Math.Max(-1f, Math.Min(1f, floatBuf[s]));
                    short val = (short)(clamped * short.MaxValue);
                    pcm16[s * 2] = (byte)(val & 0xFF);
                    pcm16[s * 2 + 1] = (byte)((val >> 8) & 0xFF);
                }
                _dseInstances[trackIdx].SubmitBuffer(pcm16);
            }
            catch (Exception ex_)
            {
                KELog.Error($"[PhaseSwift] SubmitBuffer error: {ex_.Message}");
            }
        }

        private static void OnBufferNeeded(object sender, EventArgs e)
        {
            var dsei = sender as DynamicSoundEffectInstance;
            for (int i = 0; i < _dseInstances.Length; i++)
                if (_dseInstances[i] == dsei) { SubmitNextChunk(i); return; }
        }

        private static void CleanupAudio()
        {
            _stopped = true;
            for (int i = 0; i < _dseInstances.Length; i++)
            {
                if (_dseInstances[i] != null)
                {
                    _dseInstances[i].BufferNeeded -= OnBufferNeeded;
                    // 不调 Stop/Dispose，避免 OpenAL 驱动内部锁死
                    // 仅静音 + 丢引用，旧 DSEI 缓冲耗尽后自然静默，GC 回收
                    _dseInstances[i].Volume = 0f;
                    _dseInstances[i] = null;
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
            _isFading = false;
            _visOffset = 0;
        }


        private static void ApplyScene(int sceneIdx, bool force = false)
        {
            ApplyTopology(sceneIdx);
            UpdateVisibility(sceneIdx);
        }

        private static void ApplyTopology(int sceneIdx)
        {
            var scene = Config.Scenes[sceneIdx];
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
            foreach (var link in scene.Topology)
            {
                var fromComp = Programs.getComputer(CurrentOS, link.From);
                var toComp = Programs.getComputer(CurrentOS, link.To);
                if (fromComp != null && toComp != null)
                {
                    int toIndex = CurrentOS.netMap.nodes.IndexOf(toComp);
                    if (toIndex >= 0 && !fromComp.links.Contains(toIndex))
                        fromComp.links.Add(toIndex);
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
            // 9.28: GlobalDiscovery — 其他场景发现的节点如果本场景 VisibleNodes 也含有，一并显示
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
            // 9.8: 从 nodesToShow 中排除运行时黑名单中的节点
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

        public static void OverrideOriginalLinks(Dictionary<string, System.Collections.Generic.List<string>> linkIds, OS os)
        {
            _originalLinks.Clear();
            foreach (var kv in linkIds)
            {
                var comp = Programs.getComputer(os, kv.Key);
                if (comp == null) continue;
                var indices = new System.Collections.Generic.List<int>();
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

        private static void SyncVolume()
        {
            if (_dseInstances == null || _dseInstances.Length == 0) return;
            float volMul = MusicManager.getVolume();
            for (int i = 0; i < _dseInstances.Length; i++)
            {
                if (_dseInstances[i] != null)
                    _dseInstances[i].Volume = _targetVolumes[i] * volMul;
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
        /// 保存前刷新内存中的发现状态与 admin 记录（9.6b）。
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
            var dynConfig = CustomColorPatch.ParseColorString(colorStr);
            if (dynConfig != null)
                return CustomColorPatch.CalcColor(dynConfig, OS.currentElapsedTime);
            try { return new Microsoft.Xna.Framework.Design.ColorConverter().ConvertFromString(colorStr) as Color? ?? Color.Transparent; }
            catch { return Color.Transparent; }
        }

        public static Color GetDynamicColor(string colorStr, Color defaultColor)
        {
            if (string.IsNullOrEmpty(colorStr)) return defaultColor;
            // 每帧用 CustomColorPatch 计算动态色
            var dynConfig = CustomColorPatch.ParseColorString(colorStr);
            if (dynConfig != null)
                return CustomColorPatch.CalcColor(dynConfig, OS.currentElapsedTime);
            try { return new Microsoft.Xna.Framework.Design.ColorConverter().ConvertFromString(colorStr) as Color? ?? defaultColor; }
            catch { return defaultColor; }
        }

        public static void RestorePersistentState(PhaseSwiftPersistentState state)
        {
            if (state == null) return;
            _sceneDiscoveredNodeIds = state.DiscoveredNodes ?? new System.Collections.Generic.Dictionary<int, System.Collections.Generic.HashSet<string>>();
            _runtimeBlockedNodeIds = state.RuntimeBlocked ?? new System.Collections.Generic.Dictionary<int, System.Collections.Generic.HashSet<string>>();
            // 恢复音乐组：Start() 里 LoadMusicPhase 用 CurrentMusicPhase 加载对应组
            if (Config != null && state.MusicPhase >= 0 && state.MusicPhase < Config.MusicPhases.Count)
                CurrentMusicPhase = state.MusicPhase;
            if (!string.IsNullOrEmpty(state.Theme))
                DefaultTheme = state.Theme;
        }

        public static System.Collections.Generic.Dictionary<int, System.Collections.Generic.HashSet<string>> GetSceneDiscoveredNodes() => new(_sceneDiscoveredNodeIds);
        public static System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<int>> GetOriginalLinks() => new(_originalLinks);
        public static System.Collections.Generic.Dictionary<int, System.Collections.Generic.HashSet<string>> GetRuntimeBlockedNodes() => new(_runtimeBlockedNodeIds);
    }
}