using UnityEngine;

namespace Game.Voxels.Demo
{
    /// <summary>
    /// Controls and the voxel world's numbers in the corner. F1 hides it.
    /// The text is rebuilt a few times a second, not on every GUI event: counting the bodies and
    /// laying out fresh rich text each event cost about a millisecond a frame.
    /// </summary>
    public sealed class VoxelDemoHud : MonoBehaviour
    {
        private const float REFRESH = 0.25f;

        private float _frameMs = 16f;
        private bool _visible = true;
        private GUIStyle _style;
        private readonly GUIContent _content = new();
        private float _nextRefresh;
        private string _sizedText;
        private Vector2 _size;

        private void Update()
        {
            _frameMs = Mathf.Lerp(_frameMs, Time.unscaledDeltaTime * 1000f, 0.05f);

            if (Input.GetKeyDown(KeyCode.F1))
                _visible = !_visible;

            if (_visible && Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + REFRESH;
                _content.text = BuildText();
            }
        }

        private string BuildText()
        {
            int solid = 0;
            int pieces = 0;
            foreach (VoxelBody body in VoxelWorld.Bodies)
            {
                if (body == null)
                    continue;
                solid += body.SolidCount;
                if (body.TryGetComponent(out Rigidbody _))
                    pieces++;
            }

            VoxelWorld world = VoxelWorld.Instance;
            return
                "<b>Voxel sandbox</b> — the game's own tank and controls\n" +
                "W/S/A/D — drive    Mouse — turn the camera, the crosshair aims    E — get out / in\n" +
                "Esc — free the cursor, click — take it back    Middle double-click — reset the camera\n" +
                (VoxelWeatherDemo.SmokeKey ? "B — smoke at the crosshair (shoot it to clear)    " : "") +
                (VoxelWeatherDemo.RainKey ? "N — rain on / off" : "") +
                (VoxelWeatherDemo.SmokeKey || VoxelWeatherDemo.RainKey ? "\n" : "") +
                ExtraLines() +
                "F1 — hide this\n\n" +
                $"Frame: {_frameMs:F1} ms ({1000f / Mathf.Max(0.1f, _frameMs):F0} fps)\n" +
                $"Voxels: {solid:N0} in {VoxelWorld.Bodies.Count} bodies ({pieces} loose pieces)\n" +
                $"Debris cubes: {(world != null && world.Debris != null ? world.Debris.Count : 0):N0}\n" +
                $"Last remesh: {(world != null ? world.LastRebuildChunks : 0)} chunks in {(world != null ? world.LastRebuildMs : 0f):F1} ms" +
                $" ({(world != null ? world.AsyncInFlight : 0)} ground in flight)\n" +
                $"Smoke: {VoxelSmoke.Active.Count} clouds, {SmokeCubes():N0} cubes";
        }

        // Keys from pieces outside this assembly (the air strike), one line each.
        private static string ExtraLines()
        {
            string text = "";
            foreach (System.Func<string> line in VoxelHudLines.All)
                text += line() + "\n";
            return text;
        }

        private void OnGUI()
        {
            if (!_visible || Event.current.type != EventType.Repaint)
                return;

            _style ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft, fontSize = 14, richText = true, padding = new RectOffset(8, 8, 6, 6)
            };

            // Sized to the text, measured only when the text changes: a fixed box cut the longer
            // lines of keys off at its right edge.
            if (_sizedText != _content.text)
            {
                _sizedText = _content.text;
                _size = _style.CalcSize(_content);
            }

            GUI.Box(new Rect(12, 12, _size.x, _size.y), _content, _style);
        }

        private static int SmokeCubes()
        {
            int cubes = 0;
            foreach (VoxelSmoke smoke in VoxelSmoke.Active)
                cubes += smoke.DrawnCubes;
            return cubes;
        }
    }
}
