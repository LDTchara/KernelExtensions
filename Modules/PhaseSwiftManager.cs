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

using Hacknet;
using Hacknet.Extensions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using KernelExtensions.Config;
using KernelExtensions.Utility;
using KernelExtensions.Patches;
using NVorbis;
using Pathfinder.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace KernelExtensions.Modules
{
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

        public static void Start()
        {
            if (!IsInitialized || Config == null || IsRunning) return;

            if (UseDualTrack)
            {
                if (Config.MusicPhases.Count > 0)
                    LoadMusicPhase(Config.MusicPhases[CurrentMusicPhase]);
            }
            else
            {
                // 单曲模式：MusicManager 播放指定音乐
                string singleTrack = Config.SingleTrack;
                if (string.IsNullOrEmpty(singleTrack) && Config.MusicPhases.Count > 0 && Config.MusicPhases[0].Tracks.Count > 0)
                    singleTrack = Config.MusicPhases[0].Tracks[0];
                if (!string.IsNullOrEmpty(singleTrack))
                {
                    string resolved = MusicPathResolver.ResolveMusicPath(singleTrack, ExtensionRoot);
                    MusicManager.playSongImmediatley(resolved);
                }
                }
            int firstScene = Config.InitialScene;
            CurrentScene = -1;
            SwitchToScene(firstScene);
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

            // 恢复主题
            if (!string.IsNullOrEmpty(DefaultTheme))
            {
                if (Enum.TryParse<OSTheme>(DefaultTheme, true, out OSTheme t))
                    CurrentOS.EffectsUpdater.StartThemeSwitch(0.15f, t, CurrentOS, null);
                else if (CurrentOS.EffectsUpdater != null)
                    CurrentOS.EffectsUpdater.StartThemeSwitch(0.15f, OSTheme.Custom, CurrentOS, DefaultTheme);
            }

            PhaseSwiftConnectionPatch.CurrentExe = null;
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
            _fadeProgress += dt;
            float t = Math.Min(_fadeProgress / _targetFadeDuration, 1f);
            for (int i = 0; i < _dseInstances.Length; i++)
                if (_dseInstances[i] != null)
                    _dseInstances[i].Volume = MathHelper.Lerp(_startVolumes[i], _targetVolumes[i], t);
            if (_fadeProgress >= _targetFadeDuration)
            {
                _isFading = false;
                for (int i = 0; i < _dseInstances.Length; i++)
                    if (_dseInstances[i] != null)
                        _dseInstances[i].Volume = _targetVolumes[i];
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

        public static void SwitchToScene(int targetScene, float? fadeDurationOverride = null, string overrideTheme = null)
        {
            if (Config == null || targetScene < 0 || targetScene >= Config.Scenes.Count || targetScene == CurrentScene) return;
            SaveCurrentSceneDiscovery();
            if (CurrentOS.terminal != null && CurrentOS.connectedComp != null && CurrentOS.connectedComp != CurrentOS.thisComputer)
                CurrentOS.runCommand("dc");

            if (UseDualTrack && _dseInstances.Length > 0)
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

            string theme = overrideTheme ?? Config.Scenes[targetScene].Theme;
            if (string.IsNullOrEmpty(theme)) theme = DefaultTheme;
            if (!string.IsNullOrEmpty(theme))
            {
                if (!Config.ChangeLayout) PhaseSwiftLayoutPatch.SkipLayoutChange = true;
                if (Enum.TryParse<OSTheme>(theme, true, out OSTheme themeEnum))
                    CurrentOS.EffectsUpdater.StartThemeSwitch(Config.ThemeFlickerDuration, themeEnum, CurrentOS, null);
                else
                {
                    string fullPath = theme;
                    if (!string.IsNullOrEmpty(ExtensionRoot) && !Path.IsPathRooted(theme))
                        fullPath = ExtensionRoot + "/" + theme;
                    CurrentOS.EffectsUpdater.StartThemeSwitch(Config.ThemeFlickerDuration, OSTheme.Custom, CurrentOS, fullPath);
                }
                if (!Config.ChangeLayout)
                {
                    CurrentOS.delayer.Post(ActionDelayer.Wait(Config.ThemeFlickerDuration + 0.15f), () => { PhaseSwiftLayoutPatch.SkipLayoutChange = false; });
                }
            }

            ApplyTopology(targetScene);
            UpdateVisibility(targetScene);

            var onSwitch = Config.Scenes[targetScene].OnSwitch;
            if (onSwitch != null && !string.IsNullOrEmpty(onSwitch.FilePath))
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
            CurrentMusicPhase = phaseId;
            LoadMusicPhase(phase);
        }

        public static HashSet<string> GetControlledNodeIds() { return _controlledNodeIds; }

        public static bool IsNodeAllowed(string id)
        {
            if (_controlledNodeIds.Contains(id))
            {
                bool inScene = _sceneVisibleIds[CurrentScene].Contains(id);
                bool notBlocked = !_sceneBlockedIds[CurrentScene].Contains(id);
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
                string path = Path.Combine(root, phase.Tracks[i]);
                _trackStreams[i] = File.OpenRead(path);
                _trackReaders[i] = new VorbisReader(_trackStreams[i], false);
                int sr = _trackReaders[i].SampleRate;
                int ch = _trackReaders[i].Channels;
                _trackChannels[i] = ch;
                AudioChannels audioCh = (ch >= 2) ? AudioChannels.Stereo : AudioChannels.Mono;
                _dseInstances[i] = new DynamicSoundEffectInstance(sr, audioCh);
                _dseInstances[i].BufferNeeded += OnBufferNeeded;
                _dseInstances[i].Volume = (i == CurrentScene) ? 1f : 0f;
                _dseInstances[i].Play();
                _stopped = false;
                for (int b = 0; b < 6; b++) SubmitNextChunk(i);
            }
            _isFading = false;
        }

        private static void SubmitNextChunk(int trackIdx)
        {
            if (_stopped) return;
            if (trackIdx < 0 || trackIdx >= _trackReaders.Length) return;
            var reader = _trackReaders[trackIdx];
            if (reader == null) return;
            int bufSamples = (reader.SampleRate * _trackChannels[trackIdx]) / 20;
            float[] floatBuf = new float[bufSamples];
            int read = reader.ReadSamples(floatBuf, 0, bufSamples);
            if (read < bufSamples)
            {
                _trackStreams[trackIdx].Position = 0;
                _trackReaders[trackIdx].Dispose();
                _trackReaders[trackIdx] = new VorbisReader(_trackStreams[trackIdx], false);
                if (read > 0) _trackReaders[trackIdx].ReadSamples(floatBuf, read, bufSamples - read);
            }
            PreviousVisBands = CurrentVisBands.Length > 0 ? CurrentVisBands : Array.Empty<float>();
            int binCount = 256;
            float[] rawSamples = new float[binCount];
            int step = Math.Max(1, read / binCount);
            for (int i = 0; i < binCount; i++)
            {
                int idx = i * step;
                if (idx >= read) idx = read - 1;
                rawSamples[i] = floatBuf[idx];
            }
            CurrentVisBands = rawSamples;
            LastBandUpdateTime = DateTime.UtcNow;
            try { _dseInstances[trackIdx].SubmitFloatBufferEXT(floatBuf, 0, read); }
            catch { }
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
                    _dseInstances[i].Stop();
                    _dseInstances[i].Dispose();
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
    }
}