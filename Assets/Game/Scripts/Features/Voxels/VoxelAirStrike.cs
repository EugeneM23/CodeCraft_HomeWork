using System.Collections.Generic;
using Atomic.Entities;
using Game.Voxels;
using Ticking;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Game
{
    /// <summary>
    /// VoxelSandbox: every few seconds an air bomb falls somewhere round the tank — not on it — and
    /// goes off hard: a wide crater with gentle walls (the tank drives out of it), big lumps of
    /// ground thrown high and outwards (no colliders:
    /// they land, break up and are gone in about 12 s), a fountain of clods, a cluster of fireballs, a flash, a camera shake that fades with distance
    /// and a shove for the tank when it is close.
    ///
    /// The Sandbox/AirStrike input action (X) stops the bombs and starts them again; bombs already falling
    /// still land. The sandbox HUD lists the key and whether the bombs are on.
    /// </summary>
    public sealed class VoxelAirStrike : MonoBehaviour, IUpdateListener
    {
        private const float HUD_REFRESH = 0.25f;

        [Tooltip("What the bombs fall round: the tank.")]
        [SerializeField] private Transform _target;

        [SerializeField] private float _interval = 5f;
        [SerializeField] private float _firstDelay = 3f;

        [Tooltip("Seconds from turning the bombs back on to the next one.")]
        [SerializeField] private float _resumeDelay = 1f;

        [Tooltip("Bombs land between these distances (m) from the target.")]
        [SerializeField] private float _minDistance = 10f;
        [SerializeField] private float _maxDistance = 24f;

        [Header("Fall")]
        [SerializeField] private float _dropHeight = 70f;
        [Tooltip("Speed (m/s) the bomb already has when it appears, down and a little along its slant.")]
        [SerializeField] private float _startSpeed = 35f;
        [Tooltip("How far (m) sideways the bomb travels on its way down: it falls on a slant, nose first.")]
        [SerializeField] private float _slant = 14f;

        [Header("Blast")]
        [Tooltip("Radius (m) of the blast itself: the fireball, the smoke, the shove.")]
        [FormerlySerializedAs("_craterRadius")]
        [SerializeField] private float _blastRadius = 3.4f;
        [Tooltip("Radius (m) of the crater's rim: a bowl with no wall at its edge.")]
        [SerializeField] private float _bowlRadius = 4f;
        [Tooltip("Steepest slope (degrees) of the crater's wall: the tank drives out of it. The depth follows: " +
                 "4 m and 32° make about 1.4 m.")]
        [SerializeField] private float _bowlSlope = 32f;
        [Tooltip("Speed (m/s) the clods leave the crater with.")]
        [SerializeField] private float _clodSpeed = 24f;
        [SerializeField] private int _lumps = 8;
        [Tooltip("Radius (m) of a lump of ground thrown whole, ±30 %.")]
        [SerializeField] private float _lumpSize = 0.85f;
        [Tooltip("Upward speed (m/s) of a lump, ±25 %. Debris falls at 2.2 g: 24 m/s throws it about 13 m up.")]
        [SerializeField] private float _lumpSpeed = 24f;
        [SerializeField] private int _debris = 2600;
        [Tooltip("How long (s, ±25 %) lumps and clods lie before they go: the crater does not fill with them.")]
        [SerializeField] private float _debrisLife = 12f;

        [Header("Effects")]
        [Tooltip("Pooled explosion effect (the tank shell's); a cluster of them makes one big fireball.")]
        [SerializeField] private MonoEntity _explosionVfx;
        [SerializeField] private int _fireballs = 6;
        [SerializeField] private float _flashIntensity = 9f;
        [SerializeField] private float _flashRange = 45f;
        [SerializeField] private float _shakeStrength = 1.2f;
        [SerializeField] private float _shakeDuration = 0.7f;
        [Tooltip("Shake fades to nothing this far (m) from the blast.")]
        [SerializeField] private float _shakeReach = 60f;
        [Tooltip("Speed change (m/s) the blast gives the tank at the crater's edge; it fades to nothing at Shove Reach.")]
        [SerializeField] private float _shove = 1.2f;
        [SerializeField] private float _shoveReach = 20f;

        /// <summary>Landed bombs' objects kept for the next drops; more than this are destroyed.</summary>
        private const int BOMB_POOL = 4;

        private readonly List<Bomb> _bombs = new();
        // With the frame each was put back: one is reused only after a whole frame inactive, so the whistle
        // (GameSignalAudio stops it when its bomb goes inactive) cannot carry over to the next bomb.
        private readonly Queue<(Transform bomb, int frame)> _freeBombs = new(BOMB_POOL);
        private readonly Stack<Bomb> _freeBombData = new(BOMB_POOL);
        private float _next;
        private bool _on = true;
        private bool _looped;
        private Light _flash;
        private float _flashTime = 1f;
        private System.Func<string> _hudLine;
        private string _hudText;
        private bool _hudOn;
        private float _hudRefreshTime;

        /// <summary>Whether new bombs fall; the Sandbox/AirStrike input action flips it.</summary>
        public bool On => _on;

        /// <summary>
        /// The run's air strike skill (AirStrikeSkillBehaviour) holds the prototype's own timer while it is on: only
        /// <see cref="Strike(Vector3)"/> drops bombs then. The X toggle still flips <see cref="On"/> underneath.
        /// </summary>
        public bool TimerSuppressed { get; set; }

        private sealed class Bomb
        {
            public Transform Transform;
            public Vector3 Velocity;

            /// <summary>Damage to the level's enemy targets where it lands (skill bombs); 0 for the prototype's.</summary>
            public float Damage;
            public float DamageRadius;
            public float MinFactor;
        }

        private void Start()
        {
            _next = GameTicks.Time + _firstDelay;

            var flash = new GameObject("AirStrikeFlash");
            flash.transform.SetParent(transform, false);
            _flash = flash.AddComponent<Light>();
            _flash.type = LightType.Point;
            _flash.color = new Color(1f, 0.72f, 0.4f);
            _flash.shadows = LightShadows.None;
            // Voxels take the flash in their own shader (VoxelFlash): as a light it drew every
            // chunk within 45 m a second time for as long as it burned.
            _flash.cullingMask = ~VoxelFlash.VoxelLayers;
            _flash.enabled = false;
        }

        private void OnEnable()
        {
            _hudLine ??= HudLine;
            VoxelHudLines.Add(_hudLine);
            _looped = GameTicks.TryAdd(this);
        }

        // The HUD asks every frame: the line is rebuilt only when the bombs are switched or, for a rebound key,
        // at most every HUD_REFRESH seconds (unscaled: keys are rebound under the pause); otherwise no allocation.
        private string HudLine()
        {
            float now = Time.unscaledTime;
            if (_hudText != null && _hudOn == _on && now < _hudRefreshTime)
                return _hudText;

            _hudOn = _on;
            _hudRefreshTime = now + HUD_REFRESH;
            string key = AppServices.Input.Controls.Sandbox.AirStrike.GetBindingDisplayString();
            _hudText = $"{key} — air strikes on / off (now {(_on ? "on" : "off")})";
            return _hudText;
        }

        private void OnDisable()
        {
            VoxelHudLines.Remove(_hudLine);
            if (_looped)
                GameTicks.Remove(this);
            _looped = false;
        }

        private void OnDestroy() => VoxelFlash.Clear();

        private void Update() // perf-ok: the toggle key reads input every frame; the strikes tick from the game loop with the app
        {
            if (AppServices.Input.Controls.Sandbox.AirStrike.WasPressedThisFrame())
            {
                _on = !_on;
                if (_on) GameSignals.RaiseAirStrikeCalled();
                // Back on: the next bomb a moment later, not at once and not a whole interval on.
                if (_on)
                    _next = GameTicks.Time + _resumeDelay;
            }

            if (!_looped)
                OnUpdate(Time.deltaTime);
        }

        // On the game loop with the app: no bombs fall and none move while the game is paused
        public void OnUpdate(float deltaTime)
        {
            if (_on && !TimerSuppressed && _target != null && GameTicks.Time >= _next)
            {
                _next = GameTicks.Time + _interval;
                Drop();
            }

            Fall(deltaTime);
            Fade(deltaTime);
        }

        private void Drop()
        {
            Vector2 ring = Random.insideUnitCircle.normalized * Random.Range(_minDistance, _maxDistance);
            Vector3 aim = _target.position + new Vector3(ring.x, 0f, ring.y);
            DropAt(aim, 0f, 0f, 0f);
        }

        /// <summary>A bomb falls on the point (it lands on whatever is below it) and goes off as the prototype's do.</summary>
        public void Strike(Vector3 point) => DropAt(point, 0f, 0f, 0f);

        /// <summary>
        /// <see cref="Strike(Vector3)"/>, and where it lands every enemy target of the level (entities and the horde)
        /// within <paramref name="damageRadius"/> takes <paramref name="damage"/>, down to <paramref name="minFactor"/>
        /// of it at the edge (EnemyTargets.DamageArea).
        /// </summary>
        public void Strike(Vector3 point, float damage, float damageRadius, float minFactor) =>
            DropAt(point, damage, damageRadius, minFactor);

        // The bomb objects come from a small pool (the skill drops several at once); one is built only when none is free.
        private void DropAt(Vector3 aim, float damage, float damageRadius, float minFactor)
        {
            // It comes in on a slant from a random side, so it is seen falling.
            Vector2 side = Random.insideUnitCircle.normalized * _slant;
            Vector3 start = aim + new Vector3(side.x, _dropHeight, side.y);
            Vector3 velocity = (aim - start).normalized * _startSpeed;

            Bomb bomb = _freeBombData.Count > 0 ? _freeBombData.Pop() : new Bomb();
            bomb.Transform = RentBomb(start);
            bomb.Velocity = velocity;
            bomb.Damage = damage;
            bomb.DamageRadius = damageRadius;
            bomb.MinFactor = minFactor;
            _bombs.Add(bomb);
            GameSignals.RaiseAirBombDropped(bomb.Transform);
        }

        private Transform RentBomb(Vector3 position)
        {
            while (_freeBombs.Count > 0 && _freeBombs.Peek().frame < Time.frameCount - 1)
            {
                Transform pooled = _freeBombs.Dequeue().bomb;
                if (pooled == null)
                    continue;
                pooled.SetPositionAndRotation(position, Quaternion.identity);
                pooled.gameObject.SetActive(true);
                return pooled;
            }

            return BuildBomb(position).transform;
        }

        /// <summary>A landed bomb's object back to the pool (destroyed past <see cref="BOMB_POOL"/>).</summary>
        private void ReturnBomb(Bomb bomb)
        {
            if (_freeBombs.Count < BOMB_POOL)
            {
                bomb.Transform.gameObject.SetActive(false);
                _freeBombs.Enqueue((bomb.Transform, Time.frameCount));
            }
            else
            {
                Destroy(bomb.Transform.gameObject);
            }

            bomb.Transform = null;
            if (_freeBombData.Count < BOMB_POOL)
                _freeBombData.Push(bomb);
        }

        private void Fall(float dt)
        {
            for (int i = _bombs.Count - 1; i >= 0; i--)
            {
                Bomb bomb = _bombs[i];
                if (bomb.Transform == null)
                {
                    _bombs.RemoveAt(i);
                    continue;
                }

                Vector3 from = bomb.Transform.position;
                bomb.Velocity += Physics.gravity * dt;
                Vector3 step = bomb.Velocity * dt;

                if (Physics.Raycast(from, step, out RaycastHit hit, step.magnitude + 0.1f, ~0,
                        QueryTriggerInteraction.Ignore) || from.y < -50f)
                {
                    Vector3 point = hit.collider != null ? hit.point : from;
                    float damage = bomb.Damage;
                    float damageRadius = bomb.DamageRadius;
                    float minFactor = bomb.MinFactor;
                    ReturnBomb(bomb);
                    _bombs.RemoveAt(i);
                    Explode(point);
                    if (damage > 0f)
                        DamageTargets(point, damage, damageRadius, minFactor);
                    continue;
                }

                bomb.Transform.position = from + step;
                bomb.Transform.rotation = Quaternion.LookRotation(bomb.Velocity.normalized) * Quaternion.Euler(-90f, 0f, 0f);
            }
        }

        private static void DamageTargets(Vector3 point, float damage, float radius, float minFactor)
        {
            if (GameContext.TryGetInstance(out GameContext context) && context.TryGetEnemyTargets(out IEnemyTargets targets))
                targets.DamageArea(point, radius, damage, minFactor);
        }

        private void Explode(Vector3 point)
        {
            VoxelDamage.AirBlast(point, _bowlRadius, _bowlSlope, _clodSpeed, _lumps, _lumpSize, _lumpSpeed,
                _debris, _debrisLife);
            VoxelSmoke.Blast(point, _blastRadius * 2.5f);

            SpawnFireballs(point);
            // The smoke is voxel cubes; the fireballs' particle smoke is stopped (SpawnFireballs).
            VoxelBlastSmoke.Spawn(point, _blastRadius);

            _flash.transform.position = point + Vector3.up * 3f;
            _flash.range = _flashRange;
            _flashTime = 0f;

            Shake(point);
            ShoveTarget(point);
            ExplosionEvents.Raise(point, _blastRadius);
        }

        private void SpawnFireballs(Vector3 point)
        {
            if (_explosionVfx == null || !GameContext.TryGetInstance(out GameContext context))
                return;

            GameFactory factory = context.GetGameFactory();
            for (int i = 0; i < _fireballs; i++)
            {
                // One in the middle, the rest round it and climbing: one fireball the size of the crater.
                Vector3 offset = i == 0
                    ? Vector3.zero
                    : new Vector3(Random.Range(-1f, 1f) * _blastRadius * 0.6f, Random.Range(0.5f, 3.5f),
                        Random.Range(-1f, 1f) * _blastRadius * 0.6f);
                IEntity vfx = factory.Create(_explosionVfx);
                Transform fireball = vfx.GetTransform();
                fireball.SetPositionAndRotation(point + offset, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

                // One fireball's own flash is enough next to the big one: each is a per-pixel
                // light that draws everything within 12 m again. The pooled effect lights up
                // again on its next use (ExplosionFlash.OnEnable).
                if (i > 0 && fireball.GetComponentInChildren<Light>() is { } light)
                    light.enabled = false;

                // The bomb's smoke is voxel (VoxelBlastSmoke): this pooled effect's own particle smoke is
                // stopped here; it plays again on its next use (play on awake), the cannon shell's included.
                Transform smoke = fireball.Find("Smoke");
                if (smoke != null && smoke.TryGetComponent(out ParticleSystem particles))
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void Fade(float dt)
        {
            if (_flash == null || _flashTime >= 1f)
                return;

            _flashTime += dt / 0.35f;
            float fade = 1f - Mathf.Clamp01(_flashTime);
            float intensity = _flashIntensity * fade * fade;
            _flash.intensity = LightIntensity.FromBuiltIn(intensity);
            _flash.enabled = _flashTime < 1f;

            if (_flash.enabled)
                VoxelFlash.Set(_flash.transform.position, _flash.range, _flash.color, intensity);
            else
                VoxelFlash.Clear();
        }

        private void Shake(Vector3 point)
        {
            Camera camera = Camera.main;
            float distance = camera != null ? Vector3.Distance(camera.transform.position, point) : 0f;
            float share = 1f - Mathf.Clamp01(distance / _shakeReach);
            if (share <= 0.05f)
                return;

            CameraShakeUseCase.ShakePlayerCamera(new CameraShakeArgs(_shakeDuration, _shakeStrength * share));
        }

        private void ShoveTarget(Vector3 point)
        {
            Rigidbody body = _target != null ? _target.GetComponentInParent<Rigidbody>() : null;
            if (body == null || body.isKinematic)
                return;

            Vector3 away = body.worldCenterOfMass - point;
            float distance = away.magnitude;
            float share = 1f - Mathf.Clamp01((distance - _blastRadius) / (_shoveReach - _blastRadius));
            if (share <= 0f)
                return;

            away.y = Mathf.Max(away.y, distance * 0.5f);
            // A push only, no spin: a random twist yanked the hull, and the camera with it.
            body.AddForce(away.normalized * (_shove * share), ForceMode.VelocityChange);
        }

        /// <summary>A dark green finned bomb out of primitives: nose at −Y, fins at +Y.</summary>
        private static GameObject BuildBomb(Vector3 position)
        {
            var root = new GameObject("AirBomb");
            root.transform.position = position;
            Material material = BombMaterial();

            Part(root, PrimitiveType.Capsule, new Vector3(0f, 0f, 0f), new Vector3(0.45f, 0.75f, 0.45f), material);
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.85f, 0f), new Vector3(0.22f, 0.25f, 0.22f), material);
            for (int i = 0; i < 2; i++)
            {
                GameObject fin = Part(root, PrimitiveType.Cube, new Vector3(0f, 1f, 0f), new Vector3(0.6f, 0.4f, 0.03f),
                    material);
                fin.transform.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
            }

            return root;
        }

        private static GameObject Part(GameObject root, PrimitiveType type, Vector3 position, Vector3 scale,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }

        private static Material s_bombMaterial;

        private static Material BombMaterial()
        {
            if (s_bombMaterial != null)
                return s_bombMaterial;

            s_bombMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.22f, 0.25f, 0.17f) };
            s_bombMaterial.SetFloat("_Smoothness", 0.35f);
            return s_bombMaterial;
        }
    }
}
