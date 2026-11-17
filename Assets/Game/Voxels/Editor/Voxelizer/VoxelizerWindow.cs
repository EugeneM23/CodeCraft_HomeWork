using System;
using System.IO;
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

            if (EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("Baking is off in Play mode.", MessageType.Info);
            if (_preview != null && _preview.PartCounts != "")
                EditorGUILayout.HelpBox(_preview.PartCounts.TrimEnd(), MessageType.None);
            EditorGUILayout.EndScrollView();
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
