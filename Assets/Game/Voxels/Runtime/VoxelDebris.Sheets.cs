using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace Game.Voxels
{
    /// <summary>
    /// Goo sheets (the user's liquid splash): the middle of a shell's spray (VoxelSpray.ShotGoo) rises out of the wound as
    /// one smooth sheet of small goo cubes packed edge to edge on a twisted ribbon. It climbs, leans along the shot, spirals
    /// and curls over at the top like a wave's crest, all of it moving together, for about half a second. Then it breaks
    /// up: its rim and crest tear off first into strands (goo drops let go with the sheet's own velocity, VoxelDebris.GooDrop,
    /// which stretch and string out), then the rest thins into drops that fall and stick or soak in as any goo.
    /// The sheet phase is drawn here, not simulated: positions come from the ribbon's shape at its age, so it costs a few
    /// hundred matrices a frame. At most <see cref="MAX_SHEETS"/> at once (a burst of sixty kills makes eight), fewer
    /// cubes far from the camera, none past <see cref="SHEET_FAR"/>.
    /// </summary>
    public sealed partial class VoxelDebris
    {
        /// <summary>Sheets at once: a horde burst's further kills get the rest of their spray only.</summary>
        public const int MAX_SHEETS = 8;

        /// <summary>Rows up the sheet and columns across it, near the camera.</summary>
        private const int SHEET_ROWS = 16;
        private const int SHEET_COLUMNS = 9;

        /// <summary>Past this distance from the camera (m) a sheet has half the rows and columns.</summary>
        private const float SHEET_LOD = 35f;

        /// <summary>Past this distance from the camera (m) no sheet is made: it would be a few pixels.</summary>
        public const float SHEET_FAR = 90f;

        /// <summary>The sheet holds together this long (s) before it all lets go; its rim and crest tear off from 55 % of it.</summary>
        private const float SHEET_LIFE = 0.45f;

        /// <summary>The rise eases out over this long (s): at break-up the sheet is still climbing, a little.</summary>
        private const float SHEET_RISE = 0.5f;

        private const int SHEET_CUBES = SHEET_ROWS * SHEET_COLUMNS;

        private static readonly ProfilerMarker s_sheetsMarker = new("VoxelDebris.Sheets");

        private struct Sheet
        {
            public Vector3 Base;
            public Vector3 Forward;
            public Vector3 Side;
            public float Age;
            public float Height;
            public float Width;
            public float Twist;
            public float Phase;
            public float Lean;
            public float Curl;
            public float Cube;
            public int Rows;
            public int Columns;
        }

        private readonly Sheet[] _sheets = new Sheet[MAX_SHEETS];
        private readonly bool[] _sheetLive = new bool[MAX_SHEETS];
        /// <summary>Per sheet slot and cube: still part of the sheet, and when (sheet age) it lets go.</summary>
        private readonly bool[] _sheetHeld = new bool[MAX_SHEETS * SHEET_CUBES];
        private readonly float[] _sheetRelease = new float[MAX_SHEETS * SHEET_CUBES];
        private readonly Vector3[] _sheetJitter = new Vector3[MAX_SHEETS * SHEET_CUBES];
        private readonly Vector3[] _sheetLast = new Vector3[MAX_SHEETS * SHEET_CUBES];
        private readonly Vector4[] _sheetColor = new Vector4[MAX_SHEETS * SHEET_CUBES];
        /// <summary>The rows of the sheet being worked on (SheetRows).</summary>
        private readonly Vector3[] _rowPoint = new Vector3[SHEET_ROWS];
        private readonly Vector3[] _rowAcross = new Vector3[SHEET_ROWS];
        private readonly Vector3[] _rowTangent = new Vector3[SHEET_ROWS];
        private readonly Quaternion[] _rowTurn = new Quaternion[SHEET_ROWS];
        private readonly List<Matrix4x4> _sheetMatrices = new(MAX_SHEETS * SHEET_CUBES);
        private readonly List<Vector4> _sheetColors = new(MAX_SHEETS * SHEET_CUBES);
        private int _sheetCount;

        /// <summary>Sheets in the air now: for tests and the perf readout.</summary>
        public int SheetCount => _sheetCount;

        /// <summary>Cubes held in sheets now (drawn here, not yet debris).</summary>
        public int SheetCubes { get; private set; }

        /// <summary>
        /// Starts a goo sheet at a wound: it rises <paramref name="height"/> m, leaning along <paramref name="forward"/>
        /// (horizontal), twisting and curling over, and breaks up into goo drops of these colours. False — not made (all
        /// <see cref="MAX_SHEETS"/> in the air, too far from the camera, no room in the gore share for its cubes).
        /// </summary>
        /// <param name="scale">How big (the shell's power: «Мощный фугас»): height, width and cubes grow with it.</param>
        public bool GooSheet(Vector3 at, Vector3 forward, float height, IReadOnlyList<Color32> colors, float scale = 1f)
        {
            if (colors == null || colors.Count == 0 || height <= 0f)
                return false;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f)
                return false;
            forward.Normalize();

            float distance = ViewCamera != null ? Vector3.Distance(ViewCamera.transform.position, at) : 0f;
            if (distance > SHEET_FAR)
                return false;

            int slot = -1;
            for (int i = 0; i < MAX_SHEETS; i++)
                if (!_sheetLive[i])
                {
                    slot = i;
                    break;
                }

            bool far = distance > SHEET_LOD;
            int rows = far ? SHEET_ROWS / 2 : SHEET_ROWS;
            int columns = far ? (SHEET_COLUMNS + 1) / 2 : SHEET_COLUMNS;
            if (slot < 0 || !HasGoreRoom(rows * columns))
                return false;

            scale = Mathf.Clamp(scale, 0.5f, 2.5f);
            float grow = Mathf.Sqrt(scale);
            float h = height * grow * Random.Range(0.9f, 1.1f);
            float w = h * 0.6f;
            var sheet = new Sheet
            {
                Base = at,
                Forward = forward,
                Side = Vector3.Cross(Vector3.up, forward),
                Age = 0f,
                Height = h,
                Width = w,
                // A third to half a turn up the sheet, either way round.
                Twist = Random.Range(2.1f, 3.2f) * (Random.value < 0.5f ? -1f : 1f),
                Phase = Random.Range(-0.4f, 0.4f),
                Lean = Random.Range(0.35f, 0.55f),
                Curl = Random.Range(1.6f, 2.2f),
                // Packed edge to edge: a cube as big as the larger gap between rows and columns, a little over.
                Cube = Mathf.Max(h / (rows - 1), w / (columns - 1)) * 1.15f,
                Rows = rows,
                Columns = columns
            };
            _sheets[slot] = sheet;
            _sheetLive[slot] = true;
            _sheetCount++;

            // Blood, not the meat: the darkest-red of the goo's colours.
            Color32 colour = colors[0];
            for (int i = 1; i < colors.Count; i++)
                if ((colors[i].g + colors[i].b) * colour.r < (colour.g + colour.b) * colors[i].r)
                    colour = colors[i];
            SheetRows(sheet, 0f);
            int first = slot * SHEET_CUBES;
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < columns; c++)
            {
                int k = first + r * columns + c;
                float u = r / (rows - 1f);
                float across = Mathf.Abs(c / (columns - 1f) * 2f - 1f);
                // The rim (outer columns) and the crest (top rows) tear off first, the middle holds to the end.
                float rim = Mathf.Max(across, Mathf.InverseLerp(0.7f, 1f, u));
                _sheetRelease[k] = SHEET_LIFE * Mathf.Lerp(1f, 0.55f, rim * rim) * Random.Range(0.92f, 1.05f);
                _sheetHeld[k] = true;
                _sheetJitter[k] = Random.insideUnitSphere * (sheet.Cube * 0.1f);
                // One liquid: the sheet's colour, a shade darker down it and at the rim, a little noise (no meat flecks).
                Color tint = Color32.Lerp(colour, new Color32(20, 0, 0, 255), 0.25f * (1f - u) + 0.15f * across + Random.Range(0f, 0.12f));
                _sheetColor[k] = new Vector4(tint.r, tint.g, tint.b, tint.a);
                _sheetLast[k] = _rowPoint[r] + _rowAcross[r] * (c / (columns - 1f) * 2f - 1f) + _sheetJitter[k];
            }

            return true;
        }

        /// <summary>
        /// A sheet's rows at an age, into the row arrays: where each row's middle is on the spine, the spine's way there,
        /// and the line across the row (its half width long). The spine climbs from the wound leaning along the shot and
        /// bends over at the top (the crest); the ribbon's width turns round the spine as it goes up (the twist); it is
        /// narrow at the base and widens up it. One pass up the spine for all the rows: a cube is then a row's middle plus
        /// its share of the line across.
        /// </summary>
        private void SheetRows(in Sheet s, float age)
        {
            float rise = Mathf.Clamp01(age / SHEET_RISE);
            float grown = 1f - (1f - rise) * (1f - rise);
            // A sheet is born as a short tongue and climbs: its whole length is laid out along the risen part.
            float length = s.Height * Mathf.Lerp(0.25f, 1f, grown);
            float twist = s.Twist * Mathf.Lerp(0.4f, 1f, grown);
            float widen = s.Width * 0.5f * Mathf.Lerp(0.6f, 1f, grown);
            float du = 1f / (s.Rows - 1);

            Vector3 p = s.Base;
            for (int r = 0; r < s.Rows; r++)
            {
                float u = r * du;
                // The spine's slope tips from straight up toward the shot, then over and down at the crest (the curl).
                float mid = math.max(0f, u - du * 0.5f);
                float bend = s.Lean * mid + s.Curl * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, mid)) * grown;
                Vector3 tangent = Vector3.up * math.cos(bend) + s.Forward * math.sin(bend);
                if (r > 0)
                    p += tangent * (length * du);

                // The ribbon turns round its spine as it climbs (and turns on as it rises): the tornado's twist.
                float turn = s.Phase + twist * u;
                Vector3 normal = Vector3.Cross(tangent, s.Side);
                Vector3 across = s.Side * math.cos(turn) + normal * math.sin(turn);
                _rowPoint[r] = p;
                _rowAcross[r] = across * (widen * Mathf.Lerp(0.3f, 1f, math.sqrt(u)));
                _rowTangent[r] = tangent;
                _rowTurn[r] = Quaternion.LookRotation(Vector3.Cross(tangent, across), tangent);
            }
        }

        /// <summary>Ages the sheets, lets go the cubes whose time has come (goo drops), and draws the rest.</summary>
        private void TickSheets(float dt)
        {
            if (_sheetCount == 0)
            {
                SheetCubes = 0;
                return;
            }

            using (s_sheetsMarker.Auto())
            {
                _sheetMatrices.Clear();
                _sheetColors.Clear();
                for (int slot = 0; slot < MAX_SHEETS; slot++)
                {
                    if (!_sheetLive[slot])
                        continue;

                    ref Sheet s = ref _sheets[slot];
                    s.Age += dt;
                    int first = slot * SHEET_CUBES;
                    SheetRows(s, s.Age);
                    Vector3 scale = Vector3.one * s.Cube;
                    int held = 0;
                    for (int r = 0; r < s.Rows; r++)
                    {
                        float u = r / (s.Rows - 1f);
                        for (int c = 0; c < s.Columns; c++)
                        {
                            int k = first + r * s.Columns + c;
                            if (!_sheetHeld[k])
                                continue;

                            Vector3 p = _rowPoint[r] + _rowAcross[r] * (c / (s.Columns - 1f) * 2f - 1f) + _sheetJitter[k];
                            if (s.Age >= _sheetRelease[k] && dt > 0f)
                            {
                                // Lets go with the sheet's own motion, flung a little off its face: a strand at the rim, a drop inside.
                                Vector3 velocity = (p - _sheetLast[k]) / dt + _rowTurn[r] * Vector3.forward * Random.Range(-0.6f, 0.6f) +
                                                   _rowTangent[r] * (u * Random.Range(1.2f, 2.4f)); // the crest flings on along its curl
                                _sheetHeld[k] = false;
                                GooDrop(p, velocity, (Color)_sheetColor[k], s.Cube * Random.Range(0.85f, 1.1f));
                                continue;
                            }

                            _sheetLast[k] = p;
                            held++;
                            _sheetMatrices.Add(Matrix4x4.TRS(p, _rowTurn[r], scale));
                            _sheetColors.Add(_sheetColor[k]);
                        }
                    }

                    if (held == 0)
                    {
                        _sheetLive[slot] = false;
                        _sheetCount--;
                    }
                }

                SheetCubes = _sheetMatrices.Count;
                DrawSheets();
            }
        }

        private void DrawSheets()
        {
            int count = _sheetMatrices.Count;
            for (int start = 0; start < count; start += BATCH)
            {
                int n = Mathf.Min(BATCH, count - start);
                _sheetMatrices.CopyTo(start, _batchMatrices, 0, n);
                _sheetColors.CopyTo(start, _batchColors, 0, n);
                _block.SetVectorArray(InstanceColorId, _batchColors);
                Graphics.DrawMeshInstanced(_mesh, 0, _material, _batchMatrices, n, _block, ShadowCastingMode.On, true);
            }
        }

        /// <summary>Where the cubes held in sheets were drawn last: for tests.</summary>
        public void GetSheetCubes(List<Vector3> into)
        {
            into.Clear();
            for (int i = 0; i < _sheetMatrices.Count; i++)
                into.Add(_sheetMatrices[i].GetPosition());
        }

        /// <summary>Drops every sheet at once (a scene change, a test): its cubes are not handed on.</summary>
        public void ClearSheets()
        {
            for (int i = 0; i < MAX_SHEETS; i++)
                _sheetLive[i] = false;
            _sheetCount = 0;
            SheetCubes = 0;
        }
    }
}
