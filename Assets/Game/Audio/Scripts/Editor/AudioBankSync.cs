using System;
using System.Collections.Generic;
using System.Text;
using AudioEngine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Game.AudioEngine.Adapters.EditorTools
{
    /// <summary>
    /// Builds the audio assets from <see cref="AudioLayout"/>: a clip folder per key, an AudioSourceEvent per
    /// key, an AudioBank per bank, the UI catalog, the music catalogs and the preload list of GameAudio.
    /// Idempotent. On an existing event only the clip provider changes, everything else is the designer's.
    /// </summary>
    public static class AudioBankSync
    {
        private const string MENU = "Tools/Audio/Sync Banks";
        private const string PRELOAD_FIELD = "_preloadClips";
        private const string GAME_AUDIO_TYPE = "Game.AudioEngine.Adapters.GameAudio";
        private const float MIN_DISTANCE = 2f;
        private const float MAX_DISTANCE = 80f;
        private const float ONE_SHOT_PITCH_MIN = 0.95f;
        private const float ONE_SHOT_PITCH_MAX = 1.05f;

        private static bool s_running;

        [MenuItem(MENU, priority = 0)]
        public static void SyncFromMenu() => Sync();

        /// <summary>Runs the whole sync and returns the summary it logs.</summary>
        public static string Sync()
        {
            if (s_running)
                return "Audio sync already running";
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Audio: Sync Banks skipped in Play mode");
                return "skipped in Play mode";
            }

            s_running = true;
            try
            {
                return Run();
            }
            finally
            {
                s_running = false;
            }
        }

        /// <summary>Ids ("bank.key") of the sounds whose event has no clip (or no event at all).</summary>
        public static List<string> FindSoundsWithoutClips()
        {
            List<string> missing = new List<string>();
            foreach (AudioLayout.Sound sound in AudioLayout.Sounds)
            {
                AudioSourceEvent audioEvent = LoadEvent(sound);
                if (AudioEventClips.Read(audioEvent).Count == 0)
                    missing.Add(sound.Id);
            }

            return missing;
        }

        public static AudioSourceEvent LoadEvent(AudioLayout.Sound sound) =>
            AssetDatabase.LoadAssetAtPath<AudioSourceEvent>(AudioAssetPaths.EventAsset(sound.Bank, sound.Key));

        private static string Run()
        {
            CreateFolders();

            AudioMixer mixer = AudioMixerGroups.LoadMixer();
            if (mixer == null)
                Debug.LogWarning($"Audio: mixer {AudioAssetPaths.MIXER} not found, events get no output group");

            int created = 0;
            int clipsChanged = 0;
            List<string> withoutClips = new List<string>();
            List<AudioClip> preload = new List<AudioClip>();

            foreach (string bank in AudioLayout.Banks)
                LoadOrCreate<AudioBank>(AudioAssetPaths.BankAsset(bank));

            foreach (AudioLayout.Sound sound in AudioLayout.Sounds)
            {
                AudioSourceEvent audioEvent = LoadEvent(sound);
                if (audioEvent == null)
                {
                    audioEvent = CreateEvent(sound, mixer);
                    created++;
                }

                List<AudioClip> clips = ClipsOf(sound);
                if (AudioEventClips.Write(audioEvent, clips))
                    clipsChanged++;

                if (clips.Count == 0)
                    withoutClips.Add(sound.Id);
                foreach (AudioClip clip in clips)
                    if (!preload.Contains(clip))
                        preload.Add(clip);
            }

            foreach (string bank in AudioLayout.Banks)
                WriteBank(bank);

            List<string> uiWithoutClips = WriteUICatalog();
            WriteMusicCatalog(AudioAssetPaths.MENU_MUSIC, AudioAssetPaths.MENU_MUSIC_CLIPS);
            WriteMusicCatalog(AudioAssetPaths.BATTLE_MUSIC, AudioAssetPaths.BATTLE_MUSIC_CLIPS);
            string preloadResult = WritePreload(preload);

            AssetDatabase.SaveAssets();

            StringBuilder summary = new StringBuilder();
            summary.Append($"Audio sync: {AudioLayout.Banks.Length} banks, {AudioLayout.Sounds.Length} events ");
            summary.Append($"({created} created, {clipsChanged} clip sets changed), {preloadResult}, ");
            summary.Append($"{withoutClips.Count} keys without clips");
            if (withoutClips.Count > 0)
                summary.Append(": ").Append(string.Join(", ", withoutClips));
            if (uiWithoutClips.Count > 0)
                summary.Append($"; UI without clips: {string.Join(", ", uiWithoutClips)}");

            string text = summary.ToString();
            Debug.Log(text);
            return text;
        }

        private static void CreateFolders()
        {
            bool written = false;
            written |= AudioClipFolders.EnsureOnDisk(AudioAssetPaths.BANKS, null);
            foreach (string bank in AudioLayout.Banks)
                written |= AudioClipFolders.EnsureOnDisk(AudioAssetPaths.EventFolder(bank), null);

            foreach (AudioLayout.Sound sound in AudioLayout.Sounds)
                written |= AudioClipFolders.EnsureOnDisk(AudioAssetPaths.KeyFolder(sound.Bank, sound.Key),
                    $"Drop clips for {sound.Id} here.");

            foreach (string id in AudioLayout.UISounds)
                written |= AudioClipFolders.EnsureOnDisk($"{AudioAssetPaths.UI_CLIPS}/{id}",
                    $"Drop the clip for the UI sound {id} here.");

            written |= AudioClipFolders.EnsureOnDisk(AudioAssetPaths.MENU_MUSIC_CLIPS, "Drop menu and hangar music here.");
            written |= AudioClipFolders.EnsureOnDisk(AudioAssetPaths.BATTLE_MUSIC_CLIPS, "Drop battle music here.");
            written |= AudioClipFolders.EnsureOnDisk(AudioAssetPaths.UI, null);
            written |= AudioClipFolders.EnsureOnDisk(AudioAssetPaths.MUSIC, null);

            if (written)
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static List<AudioClip> ClipsOf(AudioLayout.Sound sound)
        {
            List<AudioClip> clips = AudioClipFolders.FindClips(AudioAssetPaths.KeyFolder(sound.Bank, sound.Key));
            return clips.Count > 0 ? clips : AudioClipFolders.LoadDefaultClips(sound.Id, sound.DefaultClips);
        }

        private static AudioSourceEvent CreateEvent(AudioLayout.Sound sound, AudioMixer mixer)
        {
            AudioSourceEvent audioEvent = ScriptableObject.CreateInstance<AudioSourceEvent>();
            AssetDatabase.CreateAsset(audioEvent, AudioAssetPaths.EventAsset(sound.Bank, sound.Key));

            SerializedObject serialized = new SerializedObject(audioEvent);
            serialized.FindProperty("duration").floatValue = 0f;
            serialized.FindProperty("loop").boolValue = sound.Loop;
            serialized.FindProperty("spartialBlend").floatValue = sound.Spatial ? 1f : 0f;
            serialized.FindProperty("output").objectReferenceValue = AudioMixerGroups.Find(mixer, sound.Channel);
            serialized.FindProperty("volume").managedReferenceValue = new FloatConst(sound.Volume);

            SerializedProperty pitch = serialized.FindProperty("pitch");
            if (sound.Loop)
            {
                pitch.managedReferenceValue = new FloatConst(1f);
            }
            else
            {
                pitch.managedReferenceValue = new FloatRandom();
                pitch.FindPropertyRelative("minValue").floatValue = ONE_SHOT_PITCH_MIN;
                pitch.FindPropertyRelative("maxValue").floatValue = ONE_SHOT_PITCH_MAX;
            }

            serialized.FindProperty("minDistance").managedReferenceValue = new FloatConst(MIN_DISTANCE);
            serialized.FindProperty("maxDistance").managedReferenceValue = new FloatConst(MAX_DISTANCE);
            serialized.FindProperty("rolloffMode").intValue = (int)AudioRolloffMode.Logarithmic;
            if (sound.Loop)
                serialized.FindProperty("dopplerLevel").managedReferenceValue = new FloatConst(0f);

            serialized.FindProperty("behaviours").arraySize = 0;
            serialized.FindProperty("actions").arraySize = 0;
            if (AudioParameterCurves.TryGet(sound.Id, out AudioParameterCurves.Row row))
                AudioParameterCurves.AddTo(serialized, row);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(audioEvent);
            return audioEvent;
        }

        private static void WriteBank(string bank)
        {
            AudioBank asset = LoadOrCreate<AudioBank>(AudioAssetPaths.BankAsset(bank));
            SerializedObject serialized = new SerializedObject(asset);
            serialized.FindProperty("identifier").stringValue = bank;
            serialized.FindProperty("codeGenPath").stringValue = AudioAssetPaths.BANKS;

            SerializedProperty events = serialized.FindProperty("eventParameters");
            List<AudioLayout.Sound> sounds = new List<AudioLayout.Sound>();
            foreach (AudioLayout.Sound sound in AudioLayout.Sounds)
                if (sound.Bank == bank)
                    sounds.Add(sound);

            events.arraySize = sounds.Count;
            for (int i = 0; i < sounds.Count; i++)
            {
                SerializedProperty element = events.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("identifier").stringValue = sounds[i].Key;
                element.FindPropertyRelative("value").objectReferenceValue = LoadEvent(sounds[i]);
            }

            WriteFloatParameters(serialized.FindProperty("floatParameters"), bank);

            // The module iterates these without null checks: keep them as (possibly empty) arrays.
            EnsureArray(serialized.FindProperty("boolParameters"));
            EnsureArray(serialized.FindProperty("intParameters"));
            EnsureArray(serialized.FindProperty("callbacks"));

            if (serialized.ApplyModifiedPropertiesWithoutUndo())
                EditorUtility.SetDirty(asset);
        }

        private static void WriteFloatParameters(SerializedProperty array, string bank)
        {
            // Keep values tuned by hand for parameters that already exist.
            Dictionary<string, float> existing = new Dictionary<string, float>();
            for (int i = 0; i < array.arraySize; i++)
            {
                SerializedProperty element = array.GetArrayElementAtIndex(i);
                existing[element.FindPropertyRelative("identifier").stringValue] =
                    element.FindPropertyRelative("value").floatValue;
            }

            List<AudioLayout.Parameter> parameters = new List<AudioLayout.Parameter>();
            foreach (AudioLayout.Parameter parameter in AudioLayout.Parameters)
                if (parameter.Bank == bank)
                    parameters.Add(parameter);

            array.arraySize = parameters.Count;
            for (int i = 0; i < parameters.Count; i++)
            {
                SerializedProperty element = array.GetArrayElementAtIndex(i);
                string key = parameters[i].Key;
                element.FindPropertyRelative("identifier").stringValue = key;
                element.FindPropertyRelative("value").floatValue =
                    existing.TryGetValue(key, out float value) ? value : parameters[i].Default;
            }
        }

        private static void EnsureArray(SerializedProperty array)
        {
            // Touching arraySize materialises a null array as an empty one.
            array.arraySize = array.arraySize;
        }

        private static List<string> WriteUICatalog()
        {
            UISoundCatalog catalog = LoadOrCreate<UISoundCatalog>(AudioAssetPaths.UI_CATALOG);
            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty sounds = serialized.FindProperty("sounds");

            Dictionary<string, AudioClip> existing = new Dictionary<string, AudioClip>();
            for (int i = 0; i < sounds.arraySize; i++)
            {
                SerializedProperty element = sounds.GetArrayElementAtIndex(i);
                existing[element.FindPropertyRelative("id").stringValue] =
                    element.FindPropertyRelative("clip").objectReferenceValue as AudioClip;
            }

            List<string> withoutClips = new List<string>();
            sounds.arraySize = AudioLayout.UISounds.Length;
            for (int i = 0; i < AudioLayout.UISounds.Length; i++)
            {
                string id = AudioLayout.UISounds[i];
                List<AudioClip> clips = AudioClipFolders.FindClips($"{AudioAssetPaths.UI_CLIPS}/{id}");
                // An empty folder keeps a clip assigned by hand.
                AudioClip clip = clips.Count > 0 ? clips[0] : existing.TryGetValue(id, out AudioClip old) ? old : null;
                if (clip == null)
                    withoutClips.Add(id);

                SerializedProperty element = sounds.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("id").stringValue = id;
                element.FindPropertyRelative("clip").objectReferenceValue = clip;
            }

            if (serialized.ApplyModifiedPropertiesWithoutUndo())
                EditorUtility.SetDirty(catalog);
            return withoutClips;
        }

        private static void WriteMusicCatalog(string assetPath, string clipFolder)
        {
            MusicCatalog catalog = LoadOrCreate<MusicCatalog>(assetPath);
            List<AudioClip> clips = AudioClipFolders.FindClips(clipFolder);
            if (clips.Count == 0)
            {
                AudioClip fallback = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioAssetPaths.FALLBACK_MUSIC);
                if (fallback != null)
                    clips.Add(fallback);
            }

            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty array = serialized.FindProperty("clips");
            array.arraySize = clips.Count;
            for (int i = 0; i < clips.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];

            if (serialized.ApplyModifiedPropertiesWithoutUndo())
                EditorUtility.SetDirty(catalog);
        }

        private static string WritePreload(List<AudioClip> clips)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AudioAssetPaths.GAME_AUDIO_PREFAB);
            if (prefab == null)
                return "preload skipped (no GameAudio prefab)";

            Type type = null;
            foreach (Type candidate in TypeCache.GetTypesDerivedFrom<Component>())
                if (candidate.FullName == GAME_AUDIO_TYPE)
                    type = candidate;
            if (type == null)
                return "preload skipped (no GameAudio type)";

            Component component = prefab.GetComponentInChildren(type, true);
            if (component == null)
                return "preload skipped (GameAudio prefab has no GameAudio component)";

            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty array = serialized.FindProperty(PRELOAD_FIELD);
            if (array == null || !array.isArray)
                return $"preload skipped (GameAudio has no {PRELOAD_FIELD} array)";

            array.arraySize = clips.Count;
            for (int i = 0; i < clips.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];

            if (serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                EditorUtility.SetDirty(component);
                PrefabUtility.SavePrefabAsset(prefab);
            }

            return $"{clips.Count} clips to preload";
        }

        private static T LoadOrCreate<T>(string assetPath) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset != null)
                return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }
    }
}
