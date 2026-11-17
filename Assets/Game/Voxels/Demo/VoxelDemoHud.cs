using System.Text;
using UnityEngine;

namespace Game.Voxels.Demo
{
    /// <summary>
    /// Controls and the voxel world's numbers in the corner. F1 hides it.
    /// </summary>
    /// <remarks>
    /// Every new text costs IMGUI a fresh layout of it, and that layout makes garbage by the length
    /// of the text: rebuilt four times a second, the whole box made 50–200 KB of it a second. So the
    /// keys sit in a box of their own, laid out again only when they change (an air strike switched
    /// on or off), and the numbers, a few short lines, are rebuilt twice a second. Both are built in
    /// reused buffers, and the GUI skips its layout pass: only GUI.Box is drawn.
    /// </remarks>
    public sealed class VoxelDemoHud : MonoBehaviour
    {
        private const float REFRESH = 0.5f;
        private const float MARGIN = 12f;
        private const float GAP = 4f;

        private float _frameMs = 16f;
        private bool _visible = true;
        private float _nextRefresh;

        private readonly Box _keys = new();
        private readonly Box _numbers = new();
        private readonly StringBuilder _keysNow = new(512);

        /// <summary>One box of text, measured only when its text changes.</summary>
        private sealed class Box
        {
            public readonly GUIContent Content = new();
            public readonly StringBuilder Text = new(512);
            // A style of its own: IMGUI keeps the layout of the last text drawn with a style, and two
            // texts taking turns on one style would be laid out again on every repaint.
            public GUIStyle Style;
            private string _sized;
            private Vector2 _size;

            public void Draw(float x, float y)
            {
                Style ??= new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft, fontSize = 14, richText = true, padding = new RectOffset(8, 8, 6, 6)
                };
                GUI.Box(new Rect(x, y, Size.x, Size.y), Content, Style);
            }

            public Vector2 Size
            {
                get
                {
                    if (!ReferenceEquals(_sized, Content.text))
                    {
                        _sized = Content.text;
                        _size = Style.CalcSize(Content);
                    }

                    return _size;
                }
            }
        }

        // Nothing here uses GUILayout: without the layout pass IMGUI does not allocate for it every frame.
        private void Awake() => useGUILayout = false;

        private void Update()
        {
            _frameMs = Mathf.Lerp(_frameMs, Time.unscaledDeltaTime * 1000f, 0.05f);

            if (Input.GetKeyDown(KeyCode.F1))
                _visible = !_visible;

            if (_visible && Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + REFRESH;
                RefreshKeys();
                RefreshNumbers();
            }
        }

        /// <summary>The keys change only when a piece adds or changes its line: the text is replaced then only.</summary>
        private void RefreshKeys()
        {
            StringBuilder text = _keysNow.Clear();
            text.Append("<b>Voxel sandbox</b> — the game's own tank and controls\n")
                .Append("W/S/A/D — drive    Shift — full power    Mouse — turn the camera, the crosshair aims\n")
                .Append("LMB — cannon    RMB — machine gun    Esc — free the cursor, click — take it back\n")
                .Append("Middle double-click — reset the camera\n");
            if (VoxelWeatherDemo.SmokeKey)
                text.Append("B — smoke at the crosshair (shoot it to clear)    ");
            if (VoxelWeatherDemo.RainKey)
                text.Append("N — rain on / off");
            if (VoxelWeatherDemo.SmokeKey || VoxelWeatherDemo.RainKey)
                text.Append('\n');
            // Keys from pieces outside this assembly (the air strike), one line each.
            // Indexed loops here and below: a foreach over an IReadOnlyList boxes its enumerator.
            for (int i = 0; i < VoxelHudLines.All.Count; i++)
                text.Append(VoxelHudLines.All[i]()).Append('\n');
            text.Append("F1 — hide this");

            if (_keys.Content.text != null && Same(text, _keys.Text))
                return;

            _keys.Text.Clear().Append(text);
            _keys.Content.text = text.ToString();
        }

        private void RefreshNumbers()
        {
            int solid = 0;
            int pieces = 0;
            for (int i = 0; i < VoxelWorld.Bodies.Count; i++)
            {
                VoxelBody body = VoxelWorld.Bodies[i];
                if (body == null)
                    continue;
                solid += body.SolidCount;
                if (body.TryGetComponent(out Rigidbody _))
                    pieces++;
            }

            VoxelWorld world = VoxelWorld.Instance;
            int debris = world != null && world.Debris != null ? world.Debris.Count : 0;
            StringBuilder text = _numbers.Text.Clear();
            text.Append("Frame: ").Append(_frameMs.ToString("F1")).Append(" ms (")
                .Append(Mathf.RoundToInt(1000f / Mathf.Max(0.1f, _frameMs))).Append(" fps)\n");
            text.Append("Voxels: ").Append(solid.ToString("N0")).Append(" in ").Append(VoxelWorld.Bodies.Count)
                .Append(" bodies (").Append(pieces).Append(" loose pieces)\n");
            text.Append("Debris cubes: ").Append(debris.ToString("N0")).Append('\n');
            text.Append("Last remesh: ").Append(world != null ? world.LastRebuildChunks : 0).Append(" chunks in ")
                .Append((world != null ? world.LastRebuildMs : 0f).ToString("F1")).Append(" ms (")
                .Append(world != null ? world.AsyncInFlight : 0).Append(" ground in flight)\n");
            text.Append("Smoke: ").Append(VoxelSmoke.Active.Count).Append(" clouds, ")
                .Append(SmokeCubes().ToString("N0")).Append(" cubes");
            _numbers.Content.text = text.ToString();
        }

        private void OnGUI()
        {
            if (!_visible || Event.current.type != EventType.Repaint || _keys.Content.text == null)
                return;

            // Each box sized to its text: a fixed box cut the longer lines of keys off at its right edge.
            _keys.Draw(MARGIN, MARGIN);
            _numbers.Draw(MARGIN, MARGIN + _keys.Size.y + GAP);
        }

        /// <summary>The same characters (StringBuilder.Equals may also compare the capacity).</summary>
        private static bool Same(StringBuilder a, StringBuilder b)
        {
            if (a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i])
                    return false;
            return true;
        }

        private static int SmokeCubes()
        {
            int cubes = 0;
            for (int i = 0; i < VoxelSmoke.Active.Count; i++)
                cubes += VoxelSmoke.Active[i].DrawnCubes;
            return cubes;
        }
    }
}
