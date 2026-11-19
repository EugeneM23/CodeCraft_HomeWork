using System.Collections.Generic;
using Game.Voxels;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// The armour stand in the tank destruction lab (next to TankLab): left mouse fires an armour-piercing shell from
    /// the camera at the point under the cursor. The shell's path is traced through the target's armour at once
    /// (ArmourShotUseCase) and played back by a glowing tracer slowed down to be followed: ricochets fly off with
    /// sparks and a ping, non-penetrations leave a scorched dent, penetrations punch the hole and go on inside to the
    /// crew and modules. Every hit gets a line in the readout and a marker on the tank; B shows the armour map
    /// (thin green → thick red, spaced armour blue).
    /// </summary>
    public sealed class TankArmourLab : MonoBehaviour
    {
        [SerializeField] private Camera _camera;

        [Tooltip("The shell type loaded (Q switches): its numbers come from TankAmmoConfig, as the player tank's.")]
        [SerializeField] private ShellKind _kind = ShellKind.ArmourPiercing;

        [Tooltip("Seconds between shells.")]
        [SerializeField] private float _reload = 0.35f;

        [Tooltip("How fast (m/s) the tracer plays the shell's path: slowed down from the real speed so it can be followed.")]
        [SerializeField] private float _tracerSpeed = 70f;

        [Tooltip("Radius (m) of the bite a penetration takes (tough armour halves it).")]
        [SerializeField] private float _holeRadius = 0.26f;

        [Tooltip("Lines kept in the readout.")]
        [SerializeField] private int _lines = 9;

        private static readonly Color RicochetColor = new(0.45f, 0.8f, 1f);
        private static readonly Color NoPenColor = new(1f, 0.55f, 0.2f);
        private static readonly Color PenColor = new(0.4f, 1f, 0.35f);
        private static readonly Color ModuleColor = new(1f, 0.3f, 0.3f);
        private static readonly Color32 Scorch = new(28, 26, 24, 255);
        private static readonly Color32 Scratch = new(196, 198, 200, 255);

        private sealed class Shot
        {
            public readonly List<ArmourHit> Hits = new();
            public readonly List<Vector3> Path = new();
            public float Length;
            public float Played;
            public int Next;
            public Transform Tracer;
            public ShellKind Kind;
        }

        private struct Marker
        {
            public Vector3 Point;
            public string Text;
            public Color Color;
            public float Until;
        }

        private readonly List<Shot> _shots = new();
        private readonly List<Shot> _free = new();
        private readonly List<string> _log = new();
        private readonly List<Marker> _markers = new();
        private float _nextShot;
        private bool _map;
        private readonly List<GameObject> _maps = new();
        private GUIStyle _style;
        private GUIStyle _markerStyle;
        private int _shotNumber;

        /// <summary>The loaded type's armour numbers (TankAmmoConfig).</summary>
        public ArmourShell Shell => ShellOf(_kind);

        public ShellKind Kind
        {
            get => _kind;
            set => _kind = value;
        }

        public static ArmourShell ShellOf(ShellKind kind)
        {
            ShellAmmo ammo = TankAmmoConfig.Load().Get(kind);
            return new ArmourShell(ammo.penetration, ammo.calibre, ammo.speed, 0.08f);
        }

        /// <summary>The readout's lines, newest last.</summary>
        public IReadOnlyList<string> Log => _log;

        public Camera Camera
        {
            get => _camera;
            set => _camera = value;
        }

        private void Awake()
        {
            if (_camera == null)
                _camera = Camera.main;
        }

        private void Update() // perf-ok: the lab's own input and shells, not the game
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            if (mouse != null && _camera != null && mouse.leftButton.isPressed &&
                (keyboard == null || !keyboard.shiftKey.isPressed) && Time.time >= _nextShot)
            {
                _nextShot = Time.time + _reload;
                Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
                Vector3 from = _camera.transform.position + _camera.transform.right * 0.25f - _camera.transform.up * 0.25f;
                Vector3 to = Physics.Raycast(ray, out RaycastHit aim, 200f, ~0, QueryTriggerInteraction.Ignore)
                    ? aim.point
                    : ray.GetPoint(60f);
                Fire(from, to - from);
            }

            if (keyboard != null && keyboard.bKey.wasPressedThisFrame)
                ToggleMap();
            if (AmmoInput.SwitchPressed())
                _kind = AmmoUseCase.Next(_kind);

            Play(Time.deltaTime);
        }

        /// <summary>Fires a shell: traces it now, plays it back over the next frames.</summary>
        public void Fire(Vector3 from, Vector3 direction)
        {
            Shot shot = _free.Count > 0 ? Take() : new Shot();
            shot.Kind = _kind;
            ArmourShotUseCase.Trace(from, direction, ShellOf(_kind), Random.Range(-1f, 1f), shot.Hits, shot.Path,
                explosive: _kind == ShellKind.HighExplosive);
            shot.Length = 0f;
            for (int i = 1; i < shot.Path.Count; i++)
                shot.Length += Vector3.Distance(shot.Path[i - 1], shot.Path[i]);
            shot.Played = 0f;
            shot.Next = 0;
            shot.Tracer = MakeTracer();
            _shots.Add(shot);
            _shotNumber++;
        }

        private Shot Take()
        {
            Shot shot = _free[^1];
            _free.RemoveAt(_free.Count - 1);
            return shot;
        }

        /// <summary>Plays every shell in flight on to where it is now; its events go off as the tracer passes them.</summary>
        private void Play(float dt)
        {
            for (int s = _shots.Count - 1; s >= 0; s--)
            {
                Shot shot = _shots[s];
                shot.Played += _tracerSpeed * dt;
                while (shot.Next < shot.Hits.Count && shot.Hits[shot.Next].Distance <= shot.Played)
                    Apply(shot.Kind, shot.Hits[shot.Next++]);

                if (shot.Tracer != null)
                    Place(shot);
                if (shot.Played < shot.Length)
                    continue;
                while (shot.Next < shot.Hits.Count)
                    Apply(shot.Kind, shot.Hits[shot.Next++]);
                if (shot.Tracer != null)
                    Destroy(shot.Tracer.gameObject);
                shot.Tracer = null;
                _shots.RemoveAt(s);
                _free.Add(shot);
            }

            for (int i = _markers.Count - 1; i >= 0; i--)
                if (Time.time > _markers[i].Until)
                    _markers.RemoveAt(i);
        }

        private static void Place(Shot shot)
        {
            float left = shot.Played;
            for (int i = 1; i < shot.Path.Count; i++)
            {
                Vector3 a = shot.Path[i - 1];
                Vector3 b = shot.Path[i];
                float length = Vector3.Distance(a, b);
                if (left > length && i < shot.Path.Count - 1)
                {
                    left -= length;
                    continue;
                }

                Vector3 dir = length > 1e-4f ? (b - a) / length : Vector3.forward;
                shot.Tracer.position = a + dir * Mathf.Min(left, length);
                shot.Tracer.rotation = Quaternion.LookRotation(dir);
                return;
            }
        }

        /// <summary>One event of a shell: its effects on the tank, a line and a marker.</summary>
        private void Apply(ShellKind kind, in ArmourHit hit)
        {
            // A high-explosive shell bursts on the first thing it strikes, whatever its armour did to it.
            if (kind == ShellKind.HighExplosive)
                Burst(hit);

            if (hit.IsTerrain)
            {
                VoxelSparks.Emit(hit.Point, hit.Normal, hit.Direction, 12);
                return;
            }

            ArmourResult r = hit.Result;
            VoxelBody body = hit.Body;
            switch (r.Outcome)
            {
                case ArmourOutcome.Ricochet:
                    VoxelSparks.Emit(hit.Point, hit.Normal, Vector3.Reflect(hit.Direction, hit.Normal), 70);
                    // A bright scratch along the glance.
                    Mark(hit, Scratch, 0.05f, 0.4f, Vector3.ProjectOnPlane(hit.Direction, hit.Normal));
                    ArmourLabSounds.Play(ArmourOutcome.Ricochet, hit.Point);
                    break;
                case ArmourOutcome.NoPenetration:
                    VoxelSparks.Emit(hit.Point, hit.Normal, hit.Direction, 35);
                    if (body != null)
                    {
                        // A dent: about one voxel off the face, never through the plate; and a scorch round it.
                        VoxelDamage.Punch(hit.Point, hit.Direction, 0.07f, 2f, 6, body);
                        Mark(hit, Scorch, 0.14f, 0.14f, Vector3.ProjectOnPlane(Vector3.up, hit.Normal));
                    }

                    ArmourLabSounds.Play(ArmourOutcome.NoPenetration, hit.Point);
                    break;
                case ArmourOutcome.Penetration:
                case ArmourOutcome.SpacedPassed:
                    VoxelSparks.Emit(hit.Point, hit.Normal, hit.Direction, 40);
                    if (body != null)
                    {
                        float radius = r.Outcome == ArmourOutcome.SpacedPassed ? _holeRadius * 0.6f : _holeRadius;
                        VoxelDamage.Punch(hit.Point, hit.Direction, radius, 6f, 60, body);
                    }

                    ArmourLabSounds.Play(ArmourOutcome.Penetration, hit.Point);
                    break;
                case ArmourOutcome.ModuleHit:
                    if (body != null)
                        VoxelDamage.Punch(hit.Point, hit.Direction, 0.25f, 4f, 40, body);
                    break;
            }

            hit.Armour.Report(hit);
            string line = Line(kind, hit);
            _log.Add(line);
            while (_log.Count > _lines)
                _log.RemoveAt(0);
            Debug.Log("[Armour] " + line);
            _markers.Add(new Marker
            {
                Point = hit.Point, Text = ArmourMath.Label(r.Outcome), Color = ColorOf(r.Outcome), Until = Time.time + 5f
            });
        }

        /// <summary>The readout line with the shell type first: «[ББ] ВЛД: …».</summary>
        public static string Line(ShellKind kind, in ArmourHit hit) => $"[{TankAmmoConfig.Load().Get(kind).shortName}] {Line(hit)}";

        /// <summary>A high-explosive burst: the shell's blast crater and smoke, as the cannon's own (ExplosionUseCase).</summary>
        private static void Burst(in ArmourHit hit)
        {
            float radius = AmmoUseCase.DEFAULT_RADIUS * TankAmmoConfig.Load().Get(ShellKind.HighExplosive).radiusFactor;
            VoxelDamage.Blast(hit.Point, radius * HE_CRATER_SHARE, 9f);
            VoxelSmoke.Blast(hit.Point, radius * 0.7f);
        }

        /// <summary>The share of the blast radius a burst carves (ExplosionUseCase.VOXEL_CRATER_SHARE).</summary>
        private const float HE_CRATER_SHARE = 0.32f;

        /// <summary>The readout line of a hit: zone, nominal and effective mm, angle, penetration, the outcome.</summary>
        public static string Line(in ArmourHit hit)
        {
            ArmourResult r = hit.Result;
            if (r.Outcome == ArmourOutcome.ModuleHit)
                return $"{hit.Zone}: {ArmourMath.Label(r.Outcome)}";
            string over = r.Overmatched ? ", перекрытие калибром" : "";
            return $"{hit.Zone}: {r.Nominal:0} мм → {r.Effective:0} мм, угол {r.Angle:0}°, пробитие {r.Penetration:0} мм{over} — {ArmourMath.Label(r.Outcome)}";
        }

        private static Color ColorOf(ArmourOutcome outcome) => outcome switch
        {
            ArmourOutcome.Ricochet => RicochetColor,
            ArmourOutcome.NoPenetration => NoPenColor,
            ArmourOutcome.ModuleHit => ModuleColor,
            _ => PenColor
        };

        // ---------- Marks ----------

        /// <summary>Most scorch and scratch marks kept; the oldest goes first.</summary>
        private const int MAX_MARKS = 96;

        private readonly Queue<GameObject> _marks = new();
        private static readonly Dictionary<Color32, Material> s_markMaterials = new();

        /// <summary>
        /// A flat mark on the struck plate (a scorch, a scratch): a thin tile just over the surface, on the struck
        /// body so it turns with it. The voxelized tank's palette is full, so voxels cannot be repainted for it.
        /// </summary>
        private void Mark(in ArmourHit hit, Color32 color, float width, float length, Vector3 along)
        {
            if (hit.Body == null)
                return;
            if (along.sqrMagnitude < 1e-6f)
                along = Vector3.ProjectOnPlane(Vector3.forward, hit.Normal);
            var mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mark.name = "ArmourMark";
            DestroyImmediate(mark.GetComponent<Collider>());
            mark.transform.SetPositionAndRotation(hit.Point + hit.Normal * 0.02f, Quaternion.LookRotation(hit.Normal, along));
            mark.transform.localScale = new Vector3(width, length, 0.008f);
            mark.transform.SetParent(hit.Body.transform, true);
            var renderer = mark.GetComponent<Renderer>();
            renderer.sharedMaterial = MarkMaterial(color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _marks.Enqueue(mark);
            while (_marks.Count > MAX_MARKS)
            {
                GameObject old = _marks.Dequeue();
                if (old != null)
                    Destroy(old);
            }
        }

        private static Material MarkMaterial(Color32 color)
        {
            if (s_markMaterials.TryGetValue(color, out Material material) && material != null)
                return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = "ArmourMark", color = color };
            s_markMaterials[color] = material;
            return material;
        }

        private static Material s_tracer;

        private static Transform MakeTracer()
        {
            if (s_tracer == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                s_tracer = new Material(shader) { color = new Color(4f, 2.6f, 1f) };
            }

            var tracer = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tracer.name = "ArmourShell";
            Destroy(tracer.GetComponent<Collider>());
            tracer.transform.localScale = new Vector3(0.06f, 0.06f, 0.7f);
            tracer.GetComponent<Renderer>().sharedMaterial = s_tracer;
            return tracer.transform;
        }

        // ---------- The armour map ----------

        public bool MapShown => _map;

        /// <summary>Shows or hides the armour map on every armoured tank in the scene (rebuilt on each showing: holes count).</summary>
        public void ToggleMap()
        {
            _map = !_map;
            foreach (GameObject map in _maps)
                if (map != null)
                    Destroy(map);
            _maps.Clear();
            if (!_map)
                return;
            foreach (TankArmour armour in FindObjectsByType<TankArmour>(FindObjectsSortMode.None))
                _maps.Add(ArmourMapBuilder.Build(armour));
        }

        // ---------- Readout ----------

        private void OnGUI()
        {
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true };
            _markerStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            ShellAmmo ammo = TankAmmoConfig.Load().Get(_kind);
            GUI.Label(new Rect(16, 60, 1000, 60),
                $"ЛКМ — снаряд: <b>{ammo.fullName}</b> ({ammo.calibre:0} мм, пробитие {ammo.penetration:0} мм ±8%)   " +
                "Q — сменить снаряд   Shift+ЛКМ — пулемёт   B — карта брони", _style);

            float y = Screen.height - 24f * (_log.Count + 1) - 12f;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(10, y - 6, 880, 24f * _log.Count + 12), Texture2D.whiteTexture);
            GUI.color = Color.white;
            for (int i = 0; i < _log.Count; i++)
                GUI.Label(new Rect(16, y + 24f * i, 870, 24), _log[i], _style);

            if (_camera == null)
                return;
            foreach (Marker marker in _markers)
            {
                Vector3 screen = _camera.WorldToScreenPoint(marker.Point);
                if (screen.z <= 0f)
                    continue;
                var rect = new Rect(screen.x - 90f, Screen.height - screen.y - 30f, 180f, 22f);
                GUI.color = new Color(0f, 0f, 0f, 0.8f);
                GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), marker.Text, _markerStyle);
                GUI.color = marker.Color;
                GUI.Label(rect, marker.Text, _markerStyle);
            }

            GUI.color = Color.white;
        }
    }
}
