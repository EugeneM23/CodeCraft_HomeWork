using System.Collections.Generic;
using AudioEngine;
using UnityEditor;
using UnityEngine;

namespace Game.AudioEngine.Adapters.EditorTools
{
    /// <summary>
    /// Tools/Audio/Sound Board: every bank and key of <see cref="AudioLayout"/> with its clips, a play button
    /// that works without Play mode, a red "no clip" marker and a button to the key's clip folder; then the UI
    /// catalog and the music catalogs.
    /// </summary>
    public sealed class SoundBoardWindow : EditorWindow
    {
        private const string MENU = "Tools/Audio/Sound Board";
        private const float KEY_WIDTH = 170f;
        private const float FLAGS_WIDTH = 70f;
        private const float BUTTON_WIDTH = 24f;
        private const float SMALL_BUTTON_WIDTH = 52f;

        private static readonly Color MissingColor = new Color(1f, 0.35f, 0.35f);

        private readonly Dictionary<string, List<AudioClip>> _clipsById = new Dictionary<string, List<AudioClip>>();
        private readonly Dictionary<string, bool> _foldouts = new Dictionary<string, bool>();
        private Vector2 _scroll;
        private bool _dirty = true;
        private bool _wasPlaying;

        [MenuItem(MENU, priority = 20)]
        public static void Open()
        {
            SoundBoardWindow window = GetWindow<SoundBoardWindow>("Sound Board");
            window.minSize = new Vector2(640f, 300f);
            window.Show();
        }

        private void OnEnable() => _dirty = true;

        private void OnFocus() => _dirty = true;

        private void OnProjectChange()
        {
            _dirty = true;
            Repaint();
        }

        private void OnDisable() => AudioClipPreview.Dispose();

        private void OnGUI()
        {
            if (_dirty)
                Reload();

            DrawToolbar();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (string bank in AudioLayout.Banks)
                DrawBank(bank);

            DrawUICatalog();
            DrawMusicCatalog("Menu music", AudioAssetPaths.MENU_MUSIC, AudioAssetPaths.MENU_MUSIC_CLIPS);
            DrawMusicCatalog("Hangar music", AudioAssetPaths.HANGAR_MUSIC, AudioAssetPaths.HANGAR_MUSIC_CLIPS);
            DrawMusicCatalog("Battle music", AudioAssetPaths.BATTLE_MUSIC, AudioAssetPaths.BATTLE_MUSIC_CLIPS);
            DrawMusicCatalog("Battle: calm", AudioAssetPaths.BATTLE_CALM, AudioAssetPaths.BATTLE_CALM_CLIPS);
            DrawMusicCatalog("Battle: horde", AudioAssetPaths.BATTLE_HORDE, AudioAssetPaths.BATTLE_HORDE_CLIPS);
            DrawMusicCatalog("Battle: peak", AudioAssetPaths.BATTLE_PEAK, AudioAssetPaths.BATTLE_PEAK_CLIPS);
            DrawMusicCatalog("Elite music", AudioAssetPaths.ELITE_MUSIC, AudioAssetPaths.ELITE_MUSIC_CLIPS);

            EditorGUILayout.EndScrollView();
        }

        private void Reload()
        {
            _dirty = false;
            _clipsById.Clear();
            foreach (AudioLayout.Sound sound in AudioLayout.Sounds)
                _clipsById[sound.Id] = AudioEventClips.Read(AudioBankSync.LoadEvent(sound));
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Sync Banks", EditorStyles.toolbarButton))
                {
                    AudioBankSync.Sync();
                    _dirty = true;
                }

                if (GUILayout.Button("Apply Import Rules", EditorStyles.toolbarButton))
                    AudioImportRules.ApplyToAll();

                if (GUILayout.Button("Stop", EditorStyles.toolbarButton))
                    AudioClipPreview.Stop();

                GUILayout.FlexibleSpace();
                GUILayout.Label($"{CountMissing()} keys without clips", EditorStyles.miniLabel);
            }
        }

        private void DrawBank(string bank)
        {
            bool open = !_foldouts.TryGetValue(bank, out bool stored) || stored;
            open = EditorGUILayout.BeginFoldoutHeaderGroup(open, bank);
            _foldouts[bank] = open;
            if (open)
            {
                foreach (AudioLayout.Sound sound in AudioLayout.Sounds)
                    if (sound.Bank == bank)
                        DrawSound(sound);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawSound(AudioLayout.Sound sound)
        {
            List<AudioClip> clips = _clipsById.TryGetValue(sound.Id, out List<AudioClip> found) ? found : new List<AudioClip>();
            AudioSourceEvent audioEvent = AudioBankSync.LoadEvent(sound);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(sound.Key, GUILayout.Width(KEY_WIDTH));
                string flags = (sound.Spatial ? "3D" : "2D") + (sound.Loop ? " loop" : string.Empty);
                GUILayout.Label(flags, EditorStyles.miniLabel, GUILayout.Width(FLAGS_WIDTH));

                if (GUILayout.Button("Folder", EditorStyles.miniButton, GUILayout.Width(SMALL_BUTTON_WIDTH)))
                    AudioClipFolders.Ping(AudioAssetPaths.KeyFolder(sound.Bank, sound.Key));

                using (new EditorGUI.DisabledScope(audioEvent == null))
                {
                    if (GUILayout.Button("Event", EditorStyles.miniButton, GUILayout.Width(SMALL_BUTTON_WIDTH)))
                    {
                        Selection.activeObject = audioEvent;
                        EditorGUIUtility.PingObject(audioEvent);
                    }
                }

                if (clips.Count == 0)
                    DrawMissing(audioEvent == null ? "no event, run Sync Banks" : "no clip");
                else
                    DrawClips(clips);

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawUICatalog()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("UI", EditorStyles.boldLabel);
            UISoundCatalog catalog = AssetDatabase.LoadAssetAtPath<UISoundCatalog>(AudioAssetPaths.UI_CATALOG);
            if (catalog == null || catalog.sounds == null)
            {
                DrawMissing("no UI catalog, run Sync Banks");
                return;
            }

            foreach (UISoundCatalog.SoundInfo sound in catalog.sounds)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(sound.id, GUILayout.Width(KEY_WIDTH + FLAGS_WIDTH));
                    if (GUILayout.Button("Folder", EditorStyles.miniButton, GUILayout.Width(SMALL_BUTTON_WIDTH)))
                        AudioClipFolders.Ping($"{AudioAssetPaths.UI_CLIPS}/{sound.id}");

                    if (sound.clip == null)
                        DrawMissing("no clip");
                    else
                        DrawClips(new List<AudioClip> { sound.clip });

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawMusicCatalog(string title, string assetPath, string clipFolder)
        {
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(title, EditorStyles.boldLabel, GUILayout.Width(KEY_WIDTH + FLAGS_WIDTH));
                if (GUILayout.Button("Folder", EditorStyles.miniButton, GUILayout.Width(SMALL_BUTTON_WIDTH)))
                    AudioClipFolders.Ping(clipFolder);
                GUILayout.FlexibleSpace();
            }

            MusicCatalog catalog = AssetDatabase.LoadAssetAtPath<MusicCatalog>(assetPath);
            if (catalog == null)
            {
                DrawMissing("no catalog, run Sync Banks");
                return;
            }

            List<AudioClip> clips = new List<AudioClip>();
            foreach (AudioClip clip in catalog)
                if (clip != null)
                    clips.Add(clip);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(KEY_WIDTH + FLAGS_WIDTH + SMALL_BUTTON_WIDTH + 8f);
                if (clips.Count == 0)
                    DrawMissing("no clip");
                else
                    DrawClips(clips);
                GUILayout.FlexibleSpace();
            }
        }

        private static void DrawClips(List<AudioClip> clips)
        {
            foreach (AudioClip clip in clips)
            {
                bool playing = AudioClipPreview.Playing == clip;
                if (GUILayout.Button(playing ? "■" : "▶", EditorStyles.miniButton, GUILayout.Width(BUTTON_WIDTH)))
                {
                    if (playing)
                        AudioClipPreview.Stop();
                    else
                        AudioClipPreview.Play(clip);
                }

                if (GUILayout.Button($"{clip.name} ({clip.length:0.0}s)", EditorStyles.label, GUILayout.ExpandWidth(false)))
                    EditorGUIUtility.PingObject(clip);
            }
        }

        private static void DrawMissing(string text)
        {
            Color previous = GUI.color;
            GUI.color = MissingColor;
            GUILayout.Label(text, EditorStyles.boldLabel, GUILayout.ExpandWidth(false));
            GUI.color = previous;
        }

        private int CountMissing()
        {
            int count = 0;
            foreach (List<AudioClip> clips in _clipsById.Values)
                if (clips.Count == 0)
                    count++;

            return count;
        }

        private void Update()
        {
            // Keep the play/stop glyph in step with the preview while something plays.
            bool playing = AudioClipPreview.Playing != null;
            if (playing || _wasPlaying)
                Repaint();
            _wasPlaying = playing;
        }
    }
}
