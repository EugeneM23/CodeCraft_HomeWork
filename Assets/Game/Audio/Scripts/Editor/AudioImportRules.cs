using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.AudioEngine.Adapters.EditorTools
{
    /// <summary>
    /// Import settings of every clip under Assets/Game/Audio (default and Standalone):
    /// up to 5 s — Decompress On Load, Vorbis 0.7, preload, load in background (mono when it is a key clip);
    /// 5–30 s — Compressed In Memory, Vorbis, preload; longer than 30 s or music — Streaming, no preload.
    /// The length is known only after an import, so a clip whose settings are off is fixed and reimported once
    /// from a delayCall. A clip imported under Clips/ schedules Sync Banks once per import batch.
    /// </summary>
    public sealed class AudioImportRules : AssetPostprocessor
    {
        private const string MENU = "Tools/Audio/Apply Import Rules";
        private const string STANDALONE = "Standalone";
        private const float SHORT_SECONDS = 5f;
        private const float LONG_SECONDS = 30f;
        private const float VORBIS_QUALITY = 0.7f;

        private static readonly string[] Extensions = { ".wav", ".ogg", ".mp3", ".aif", ".aiff" };

        private static readonly HashSet<string> s_pendingFixes = new HashSet<string>();
        private static readonly HashSet<string> s_beingFixed = new HashSet<string>();
        private static bool s_fixScheduled;
        private static bool s_syncScheduled;

        [MenuItem(MENU, priority = 1)]
        public static void ApplyToAllFromMenu() => ApplyToAll();

        /// <summary>Applies the rules to every clip under Assets/Game/Audio. Returns how many were reimported.</summary>
        public static int ApplyToAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioAssetPaths.ROOT });
            List<string> changed = new List<string>();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (TryApply(path))
                        changed.Add(path);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            Debug.Log($"Audio import rules: {guids.Length} clips checked, {changed.Count} reimported");
            return changed.Count;
        }

        public static bool IsRuledPath(string path)
        {
            if (!path.StartsWith(AudioAssetPaths.ROOT + "/", StringComparison.Ordinal))
                return false;

            foreach (string extension in Extensions)
                if (path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        private void OnPostprocessAudio(AudioClip clip)
        {
            if (!IsRuledPath(assetPath) || !(assetImporter is AudioImporter importer))
                return;

            if (Matches(importer, RuleFor(assetPath, clip.length)))
                return;

            // Cannot reimport from inside an import: fix it once, right after this batch.
            if (s_beingFixed.Contains(assetPath))
            {
                Debug.LogWarning($"Audio import rules: {assetPath} still differs after a fix, left as is");
                return;
            }

            s_pendingFixes.Add(assetPath);
            if (!s_fixScheduled)
            {
                s_fixScheduled = true;
                EditorApplication.delayCall += FixPending;
            }
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (s_syncScheduled)
                return;

            if (TouchesKeyClips(imported) || TouchesKeyClips(deleted) || TouchesKeyClips(moved) || TouchesKeyClips(movedFrom))
            {
                s_syncScheduled = true;
                EditorApplication.delayCall += SyncAfterImport;
            }
        }

        private static bool TouchesKeyClips(string[] paths)
        {
            foreach (string path in paths)
                if (path.StartsWith(AudioAssetPaths.CLIPS + "/", StringComparison.Ordinal) && IsRuledPath(path))
                    return true;

            return false;
        }

        /// <summary>
        /// For tests: runs the import-rule fixes queued for the next editor tick now and drops the queued Sync Banks
        /// (the test syncs itself), so nothing a test import scheduled lands after the test.
        /// </summary>
        internal static void FlushScheduledForTests()
        {
            if (s_fixScheduled)
            {
                EditorApplication.delayCall -= FixPending;
                FixPending();
            }

            if (s_syncScheduled)
            {
                EditorApplication.delayCall -= SyncAfterImport;
                s_syncScheduled = false;
            }
        }

        private static void SyncAfterImport()
        {
            s_syncScheduled = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return;

            AudioBankSync.Sync();
        }

        private static void FixPending()
        {
            s_fixScheduled = false;
            List<string> paths = new List<string>(s_pendingFixes);
            s_pendingFixes.Clear();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string path in paths)
                {
                    s_beingFixed.Add(path);
                    TryApply(path);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                s_beingFixed.Clear();
            }
        }

        private static bool TryApply(string path)
        {
            if (!IsRuledPath(path) || !(AssetImporter.GetAtPath(path) is AudioImporter importer))
                return false;

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
                return false;

            AudioImportRule rule = RuleFor(path, clip.length);
            if (Matches(importer, rule))
                return false;

            importer.forceToMono = rule.ForceToMono;
            importer.loadInBackground = rule.LoadInBackground;
            AudioImporterSampleSettings settings = Apply(importer.defaultSampleSettings, rule);
            importer.defaultSampleSettings = settings;
            importer.SetOverrideSampleSettings(STANDALONE, Apply(importer.GetOverrideSampleSettings(STANDALONE), rule));
            importer.SaveAndReimport();
            return true;
        }

        /// <summary>The settings a clip at this path with this length (seconds) gets. Pure, for tests.</summary>
        public static AudioImportRule RuleFor(string path, float length)
        {
            bool music = path.StartsWith(AudioAssetPaths.MUSIC + "/", StringComparison.Ordinal) ||
                path.StartsWith(AudioAssetPaths.CLIPS + "/music/", StringComparison.Ordinal);

            if (music || length > LONG_SECONDS)
                return new AudioImportRule(AudioClipLoadType.Streaming, false, false, false);
            if (length > SHORT_SECONDS)
                return new AudioImportRule(AudioClipLoadType.CompressedInMemory, true, true, false);

            bool keyClip = path.StartsWith(AudioAssetPaths.CLIPS + "/", StringComparison.Ordinal);
            return new AudioImportRule(AudioClipLoadType.DecompressOnLoad, true, true, keyClip);
        }

        private static AudioImporterSampleSettings Apply(AudioImporterSampleSettings settings, AudioImportRule rule)
        {
            settings.loadType = rule.LoadType;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = VORBIS_QUALITY;
            settings.preloadAudioData = rule.Preload;
            return settings;
        }

        private static bool Matches(AudioImporter importer, AudioImportRule rule)
        {
            if (importer.forceToMono != rule.ForceToMono || importer.loadInBackground != rule.LoadInBackground)
                return false;
            if (!Matches(importer.defaultSampleSettings, rule))
                return false;

            return importer.ContainsSampleSettingsOverride(STANDALONE) &&
                Matches(importer.GetOverrideSampleSettings(STANDALONE), rule);
        }

        private static bool Matches(AudioImporterSampleSettings settings, AudioImportRule rule) =>
            settings.loadType == rule.LoadType &&
            settings.compressionFormat == AudioCompressionFormat.Vorbis &&
            Mathf.Approximately(settings.quality, VORBIS_QUALITY) &&
            settings.preloadAudioData == rule.Preload;
    }
}
