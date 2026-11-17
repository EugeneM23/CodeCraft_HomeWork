using System;
using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Game.Voxels.EditorTools
{
    /// <summary>
    /// Tools/Voxels/Voxelizer: pick a config, see the model and its voxel version side by side without
    /// Play, bake VX_&lt;Name&gt; next to the config. UI only; the panels are VoxelizerPreview's.
    /// </summary>
    public sealed class VoxelizerWindow : EditorWindow
    {
        private const float SIDE_WIDTH = 380f;
        private const float GAP = 4f;

        [SerializeField] private VoxelizerConfig _config;
        [SerializeField] private GameObject _model;
        private Editor _configEditor;
        private VoxelizerPreview _preview;
        private Vector2 _scroll;
        private string _error = "";
        private int _part;

        [MenuItem("Tools/Voxels/Voxelizer")]
        public static void Open()
        {
            var window = GetWindow<VoxelizerWindow>("Voxelizer");
            window.minSize = new Vector2(900f, 480f);
        }

        private void OnDisable()
        {
            _preview?.Dispose();
            _preview = null;
            if (_configEditor != null) DestroyImmediate(_configEditor);
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(SIDE_WIDTH)))
                    Side();
                using (new EditorGUILayout.VerticalScope())
                    Panels();
            }
        }

        private void Side()
        {
            EditorGUI.BeginChangeCheck();
            _config = (VoxelizerConfig)EditorGUILayout.ObjectField("Config", _config, typeof(VoxelizerConfig), false);
            if (EditorGUI.EndChangeCheck())
            {
                _preview?.Dispose();
                _preview = null;
                _error = "";
            }

            if (_config == null)
            {
                _model = (GameObject)EditorGUILayout.ObjectField("Model", _model, typeof(GameObject), false);
                using (new EditorGUI.DisabledScope(_model == null))
                    if (GUILayout.Button("New config"))
                    {
                        _config = CreateConfig(_model);
                        GUIUtility.ExitGUI();
                    }
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            Editor.CreateCachedEditor(_config, null, ref _configEditor);
            _configEditor.OnInspectorGUI();
            EditorGUILayout.Space();

            bool hasModel = _config.model != null;
            using (new EditorGUI.DisabledScope(!hasModel))
            {
                if (GUILayout.Button("Recompute", GUILayout.Height(26f)))
                    Recompute();
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                    if (GUILayout.Button($"Bake to {Folder()}", GUILayout.Height(26f)))
                        Bake();
            }

            VoxFileButtons();
            if (EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("Baking is off in Play mode.", MessageType.Info);
            if (_preview != null && _preview.PartCounts != "")
                EditorGUILayout.HelpBox(_preview.PartCounts.TrimEnd(), MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        /// <summary>The baked VX_&lt;Name&gt;_Data.asset next to the config, or null.</summary>
        private VoxelModelAsset BakedAsset() =>
            _config.model == null ? null : AssetDatabase.LoadAssetAtPath<VoxelModelAsset>($"{Folder()}/VX_{_config.model.name}_Data.asset");

        private void VoxFileButtons()
        {
            VoxelModelAsset asset = BakedAsset();
            bool ready = asset != null && asset.parts.Length > 0;
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!ready || EditorApplication.isPlaying))
            {
                string[] names = ready ? PartNames(asset) : new[] { "(bake first)" };
                _part = EditorGUILayout.Popup("MagicaVoxel part", math.clamp(_part, 0, names.Length - 1), names);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Export .vox")) ExportVox(asset);
                    if (GUILayout.Button("Import .vox")) ImportVox(asset);
                }
            }
        }

        private static string[] PartNames(VoxelModelAsset asset)
        {
            var names = new string[asset.parts.Length];
            for (int i = 0; i < names.Length; i++)
                names[i] = asset.parts[i].path == "" ? "(root)" : asset.parts[i].path.Replace('/', '∕'); // '/' would make a submenu
            return names;
        }

        private void ExportVox(VoxelModelAsset asset)
        {
            try
            {
                VoxelModelAsset.Part part = asset.parts[_part];
                if (math.any(new int3(part.size.x, part.size.y, part.size.z) > VoxFile.MAX_SIZE))
                {
                    _error = "The part is over 256 voxels a side; it cannot be exported in pieces.";
                    return;
                }

                string path = EditorUtility.SaveFilePanel("Export .vox", "", $"{_config.model.name}_{_part}.vox", "vox");
                if (path != "") File.WriteAllBytes(path, VoxelizerVoxExchange.Export(asset, _part)); // cancelled: nothing to write, but the GUI still exits below
                _error = "";
            }
            catch (Exception e)
            {
                _error = e.Message;
                Debug.LogException(e);
            }

            GUIUtility.ExitGUI();
        }

        private void ImportVox(VoxelModelAsset asset)
        {
            try
            {
                string path = EditorUtility.OpenFilePanel("Import .vox", "", "vox");
                if (path != "") VoxelizerVoxExchange.Import(asset, _part, File.ReadAllBytes(path)); // cancelled: nothing to read, but the GUI still exits below
                AssetDatabase.SaveAssetIfDirty(asset);
                _error = "";
            }
            catch (Exception e)
            {
                _error = e.Message;
                Debug.LogException(e);
            }

            GUIUtility.ExitGUI();
        }

        private void Panels()
        {
            Rect area = GUILayoutUtility.GetRect(10f, 10f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (_preview == null || !_preview.HasModel)
            {
                EditorGUI.DrawRect(area, new Color(0.18f, 0.18f, 0.18f));
                GUI.Label(area, "Pick a config and press Recompute.", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                float w = (area.width - GAP) * 0.5f;
                var source = new Rect(area.x, area.y, w, area.height);
                var voxels = new Rect(area.x + w + GAP, area.y, w, area.height);
                if (_preview.Render(source, voxels)) Repaint();
                GUI.Label(new Rect(source.x + 6f, source.y + 4f, w, 18f), "Model", EditorStyles.whiteBoldLabel);
                GUI.Label(new Rect(voxels.x + 6f, voxels.y + 4f, w, 18f), "Voxels", EditorStyles.whiteBoldLabel);
            }

            if (_error != "")
                EditorGUILayout.HelpBox(_error, MessageType.Error);
            else
                EditorGUILayout.LabelField(_preview?.Summary ?? "", EditorStyles.miniLabel);
        }

        private void Recompute()
        {
            _preview ??= new VoxelizerPreview();
            try
            {
                _preview.Recompute(_config);
                _error = "";
            }
            catch (Exception e)
            {
                _error = e.Message;
                Debug.LogException(e);
            }

            GUIUtility.ExitGUI();
        }

        private void Bake()
        {
            // A dialog opened from a button: the GUI exits after it either way (as after the file panels).
            if (BakedAsset() != null && !EditorUtility.DisplayDialog("Re-bake",
                    $"VX_{_config.model.name}_Data.asset already exists. Re-baking replaces hand edits imported from .vox.",
                    "Re-bake", "Cancel"))
                GUIUtility.ExitGUI();

            try
            {
                GameObject prefab = VoxelModelBakeUseCase.Bake(_config, Folder());
                EditorGUIUtility.PingObject(prefab);
                _error = "";
            }
            catch (Exception e)
            {
                _error = e.Message;
                Debug.LogException(e);
            }

            GUIUtility.ExitGUI();
        }

        /// <summary>The config's folder: "New config" puts it next to the model, so the bake lands there too.</summary>
        private string Folder() => Path.GetDirectoryName(AssetDatabase.GetAssetPath(_config))?.Replace('\\', '/') ?? "Assets";

        /// <summary>&lt;Model&gt;VoxelizerConfig.asset next to the model; an existing one is opened, not replaced.</summary>
        private static VoxelizerConfig CreateConfig(GameObject model)
        {
            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(model))?.Replace('\\', '/') ?? "Assets";
            string path = $"{folder}/{model.name}VoxelizerConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<VoxelizerConfig>(path);
            if (config != null) return config;

            config = CreateInstance<VoxelizerConfig>();
            config.model = model;
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            return config;
        }
    }
}
