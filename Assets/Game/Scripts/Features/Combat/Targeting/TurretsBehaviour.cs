using System;
using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game
{
    /// <summary>
    /// The turret skill (GDD §3): up to three small machine-gun boxes on the hull — rear left, rear right, roof rear —
    /// shown by <see cref="CombatModifiers.TurretCount"/>, each aiming like the machine gun
    /// (<see cref="AutoTargetTracker"/>, the player's focus included) and firing the machine gun's bullet prefab
    /// through the pool every <c>turretInterval</c>, at <c>turretDamage × TurretDamageFactor</c>. The mounts and
    /// their weapon entities are made once at attach and only shown or hidden afterwards. They fire while the hull is
    /// intact, somebody drives and the tank is not stunned. On the tank; added by <see cref="TargetingSetup"/>.
    /// </summary>
    public sealed class TurretsBehaviour : IEntityInit, IEntityTick, IEntityDispose
    {
        private const int MAX_TURRETS = 3;

        private sealed class Turret
        {
            public GameObject Mount;
            public Transform Muzzle;
            public IEntity Weapon;
            public AutoTargetTracker Tracker;
            public float NextShot;
        }

        private readonly IEnemyTargets _targets;
        private readonly WeaponSettings _settings;
        private readonly Turret[] _turrets = new Turret[MAX_TURRETS];

        private IEntity _tank;
        private CombatModifiers _modifiers;
        private Action _onModifiersChanged;
        private Health _health;
        private IReactiveVariable<IEntity> _driver;
        private GameFactory _factory;
        private ReactiveInt _damage;
        private int _shown;
        private Rigidbody _body;

        public TurretsBehaviour(IEnemyTargets targets, WeaponSettings settings)
        {
            _targets = targets;
            _settings = settings;
        }

        /// <summary>Where the turrets throw their casings (experiment); null throws none. Set before the tank inits it.</summary>
        public CasingField Casings { get; set; }

        public void Init(IEntity tank)
        {
            _tank = tank;
            if (!tank.TryGetCombatModifiers(out _modifiers) || _modifiers == null)
                return;
            if (!GameContext.TryGetInstance(out GameContext context) || !context.TryGetGameFactory(out _factory))
                return;

            MonoEntity bullet = FindBulletPrefab(tank);
            if (bullet == null || !tank.TryGetTransform(out Transform root) || root == null)
                return;

            tank.TryGetHealth(out _health);
            tank.TryGetDriver(out _driver);
            _damage = new ReactiveInt(0);

            Collider[] hull = HullColliders(root);
            Bounds bounds = HullBounds(root);
            Transform parent = tank.TryGetHullVisual(out Transform hullVisual) && hullVisual != null ? hullVisual : root;
            tank.TryGetTurretVisual(out Transform turretVisual);
            Material material = HullMaterial(tank, root);
            for (int i = 0; i < MAX_TURRETS; i++)
                _turrets[i] = Build(i, root, parent, turretVisual, bounds, material, bullet, hull);
            tank.TryGetRiggedBody(out _body);

            _onModifiersChanged = Apply;
            _modifiers.Changed += _onModifiersChanged;
            Apply();
        }

        public void Dispose(IEntity tank)
        {
            if (_modifiers != null && _onModifiersChanged != null)
                _modifiers.Changed -= _onModifiersChanged;

            for (int i = 0; i < MAX_TURRETS; i++)
            {
                Turret turret = _turrets[i];
                if (turret?.Mount != null)
                    Object.Destroy(turret.Mount);
                _turrets[i] = null;
            }

            _shown = 0;
        }

        public void Tick(IEntity tank, float deltaTime)
        {
            if (_shown == 0)
                return;

            bool canFire = (_health == null || _health.Exists()) && (_driver == null || _driver.Value != null) &&
                           !TankStunUseCase.IsStunned(_tank);
            if (!canFire)
                return;

            float now = GameTicks.Time;
            for (int i = 0; i < _shown; i++)
            {
                Turret turret = _turrets[i];
                Vector3 muzzle = turret.Muzzle.position;
                if (!turret.Tracker.TryGetAimPoint(muzzle, deltaTime, out Vector3 aim))
                    continue;

                if (TargetGeometry.TryAim(muzzle, aim, _settings.maxPitch, out Quaternion rotation))
                    turret.Mount.transform.rotation = rotation;

                if (now < turret.NextShot)
                    continue;

                turret.NextShot = now + _settings.turretInterval;
                BulletShotUseCase.Fire(turret.Weapon, _factory, turret.Muzzle);

                // Experiment: a casing out of the box's right side, behind the muzzle.
                if (Casings != null)
                {
                    Transform mount = turret.Mount.transform;
                    Vector3 at = mount.position + mount.right * (_settings.turretSize.x * 0.5f + 0.02f);
                    Casings.Eject(at, mount.rotation, _body != null ? _body.linearVelocity : Vector3.zero);
                }
            }
        }

        /// <summary>Shows as many turrets as the skill gives and sets their damage.</summary>
        private void Apply()
        {
            int count = Mathf.Clamp(_modifiers.TurretCount, 0, MAX_TURRETS);
            _damage.Value = Mathf.Max(1, Mathf.RoundToInt(_settings.turretDamage * _modifiers.TurretDamageFactor));

            for (int i = 0; i < MAX_TURRETS; i++)
            {
                Turret turret = _turrets[i];
                if (turret == null)
                    continue;
                bool show = i < count;
                if (turret.Mount.activeSelf != show)
                    turret.Mount.SetActive(show);
                if (show && i >= _shown)
                    turret.NextShot = GameTicks.Time + _settings.turretInterval * (i + 1) / MAX_TURRETS;
            }

            _shown = count;
        }

        private Turret Build(int i, Transform root, Transform parent, Transform turretVisual, Bounds hull,
            Material material, MonoEntity bullet, Collider[] hullColliders)
        {
            Vector3 size = _settings.turretSize;
            // On the engine deck behind the main turret: the side boxes against the hull's edges (clear of the
            // antenna and the turret's sweep), the centre one against the rear edge.
            float rearZ = hull.min.z + size.z * 0.5f + 0.05f;
            Vector3 footprint = i switch
            {
                0 => new Vector3(hull.min.x + size.x * 0.5f + 0.02f, 0f, rearZ + 0.1f), // rear left
                1 => new Vector3(hull.max.x - size.x * 0.5f - 0.02f, 0f, rearZ + 0.1f), // rear right
                _ => new Vector3(hull.center.x, 0f, rearZ) // roof rear, centre
            };
            float deck = DeckTop(root, parent, turretVisual, hull.max.y, footprint, size);
            Vector3 local = new Vector3(footprint.x, deck + size.y * 0.5f, footprint.z);

            var mount = new GameObject($"CombatTurret_{i}");
            mount.transform.SetParent(parent, false);
            mount.transform.position = root.TransformPoint(local);
            mount.transform.rotation = root.rotation;

            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Box";
            // At once: a deferred Destroy leaves the box's collider in the hull until the frame ends.
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.transform.SetParent(mount.transform, false);
            box.transform.localScale = size;
            if (material != null)
                box.GetComponent<MeshRenderer>().sharedMaterial = material;

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(mount.transform, false);
            muzzle.localPosition = new Vector3(0f, 0f, size.z * 0.5f + 0.05f);

            IEntity weapon = new Entity();
            weapon.AddBulletPrefab(bullet);
            weapon.AddDamage(_damage);
            weapon.AddSpread(new Const<float>(_settings.turretSpread));
            weapon.AddFirePoint(muzzle);
            weapon.AddTransform(mount.transform);
            weapon.AddWeaponOwner(_tank);
            if (hullColliders.Length > 0)
                weapon.AddShooterColliders(hullColliders);

            var tracker = new AutoTargetTracker(_settings.turretRange, _settings.retargetInterval);
            tracker.Bind(_targets);

            mount.SetActive(false);
            return new Turret { Mount = mount, Muzzle = muzzle, Weapon = weapon, Tracker = tracker };
        }

        /// <summary>The machine gun's bullet; without a machine gun, the cannon's.</summary>
        private static MonoEntity FindBulletPrefab(IEntity tank)
        {
            if (tank.TryGetSecondaryWeapon(out IReactiveVariable<IEntity> mg) && mg.Value != null &&
                mg.Value.TryGetBulletPrefab(out MonoEntity bullet) && bullet != null)
                return bullet;
            if (tank.TryGetWeapon(out IReactiveVariable<IEntity> cannon) && cannon.Value != null &&
                cannon.Value.TryGetBulletPrefab(out MonoEntity shell) && shell != null)
                return shell;
            return null;
        }

        /// <summary>
        /// The hull's solid colliders on the tank's body: turret and machine-gun bullets fly through them
        /// (the weapon's ShooterColliders, SpawnUseCase.PassThroughShooter).
        /// </summary>
        internal static Collider[] HullColliders(Transform root)
        {
            var result = new List<Collider>();
            Rigidbody body = root.GetComponentInParent<Rigidbody>();
            foreach (Collider collider in root.GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger || !collider.enabled)
                    continue;
                if (body != null && collider.attachedRigidbody != body)
                    continue;
                result.Add(collider);
            }

            return result.ToArray();
        }

        /// <summary>
        /// The hull's box in the tank's local space: the largest solid BoxCollider of the tank's body (the hull; the
        /// smaller one is the turret), else the Tiger's hull. Not the renderers: under the hull visual sit the main
        /// turret and its gun, the antenna, the exhaust smoke (a world-space particle system whose bounds follow the
        /// trail), the skirts and the voxel skin, and their union is far bigger than the deck.
        /// </summary>
        internal static Bounds HullBounds(Transform root)
        {
            var best = new Bounds(new Vector3(0f, 0.645f, 0f), new Vector3(2.025f, 0.39f, 2.775f));
            float bestVolume = 0f;
            Rigidbody body = root.GetComponentInParent<Rigidbody>();
            foreach (BoxCollider box in root.GetComponentsInChildren<BoxCollider>())
            {
                if (box.isTrigger || !box.enabled || (body != null && box.attachedRigidbody != body))
                    continue;
                Bounds local = ToRootSpace(root, box.transform, new Bounds(box.center, box.size));
                float volume = local.size.x * local.size.y * local.size.z;
                if (volume <= bestVolume)
                    continue;
                best = local;
                bestVolume = volume;
            }

            return best;
        }

        /// <summary>
        /// The top of the deck under a box at <paramref name="footprint"/> (x, z in the tank's space): the hull's top,
        /// raised by low deck details lying on it (grilles, fans, hatches), so the box sits flush on them; tall thin
        /// parts (the antenna) and the main turret are left out.
        /// </summary>
        private static float DeckTop(Transform root, Transform hullVisual, Transform turretVisual, float hullTop,
            Vector3 footprint, Vector3 size)
        {
            const float LIE_TOLERANCE = 0.05f;
            const float MAX_DETAIL = 0.15f;

            float top = hullTop;
            float minX = footprint.x - size.x * 0.5f, maxX = footprint.x + size.x * 0.5f;
            float minZ = footprint.z - size.z * 0.5f, maxZ = footprint.z + size.z * 0.5f;
            foreach (MeshFilter filter in hullVisual.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.GetComponent<MeshRenderer>() == null)
                    continue;
                if (turretVisual != null && filter.transform.IsChildOf(turretVisual))
                    continue;
                Bounds b = ToRootSpace(root, filter.transform, filter.sharedMesh.bounds);
                if (b.max.x <= minX || b.min.x >= maxX || b.max.z <= minZ || b.min.z >= maxZ)
                    continue;
                if (Mathf.Abs(b.min.y - hullTop) > LIE_TOLERANCE || b.max.y > hullTop + MAX_DETAIL)
                    continue;
                top = Mathf.Max(top, b.max.y);
            }

            return top;
        }

        private static Bounds ToRootSpace(Transform root, Transform from, Bounds bounds)
        {
            Vector3 c = bounds.center;
            Vector3 e = bounds.extents;
            var result = new Bounds(root.InverseTransformPoint(from.TransformPoint(c - e)), Vector3.zero);
            for (int k = 1; k < 8; k++)
            {
                var corner = new Vector3((k & 1) == 0 ? -e.x : e.x, (k & 2) == 0 ? -e.y : e.y,
                    (k & 4) == 0 ? -e.z : e.z);
                result.Encapsulate(root.InverseTransformPoint(from.TransformPoint(c + corner)));
            }

            return result;
        }

        /// <summary>A material of the hull (or of any renderer on the tank), so the boxes match it.</summary>
        private static Material HullMaterial(IEntity tank, Transform root)
        {
            Transform from = tank.TryGetHullVisual(out Transform hull) && hull != null ? hull : root;
            foreach (Renderer renderer in from.GetComponentsInChildren<Renderer>())
                if (renderer is MeshRenderer && renderer.sharedMaterial != null)
                    return renderer.sharedMaterial;
            return null;
        }
    }
}
