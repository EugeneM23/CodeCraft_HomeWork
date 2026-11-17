using System.Collections.Generic;
using Atomic.Entities;
using HighlightPlus;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the drivable tank out of primitives, its cannon shell, and parks one in the Playground.
    ///
    /// Tank_01 (layer Character, every collider on the root — Collider.TryGetEntity looks no further):
    ///   TurretPivot      kept level in yaw by TurretAimBehaviour; carries the Cannon and its FirePoint
    ///   CameraPoint      at the character's camera point height, the camera offset is shared
    ///   ExitPoints       Left, Right, Back
    ///   Suspension       Mount_L1..L5, Mount_R1..R5: where each wheel's spring hangs, front to rear
    ///   HullVisual       everything visible; no colliders
    ///     Wheel_L1..R5   road wheels, moved and spun by TankTrackView
    ///     TrackL/R_Link  lower-run links, laid between the wheels by TankTrackView
    ///     TurretVisual   copies TurretPivot's yaw
    ///
    /// The body rides on a raycast suspension (TankSuspensionBehaviour): five sprung wheels a side
    /// hold the hull box off the ground, free to pitch and roll. The hull box is the only collider
    /// below the turret, and it sits above the wheels' reach, so on flat ground only the springs
    /// touch the floor.
    ///
    /// TankShell_01 is Rocket_01 made faster, shorter-lived and wider in its blast.
    ///
    /// It also widens the player's interaction probe to the Character layer, on the [Character]
    /// prefab and on the Playground's unpacked character, so standing next to the tank finds it.
    /// Re-running rebuilds the prefabs and puts the Playground tank back where it started.
    /// </summary>
    public static class TankBuilder
    {
        private const string LOG = "[TANK]";

        private const string ROCKET_PREFAB = "Assets/Game/Prefabs/Projectiles/Rocket_01.prefab";
        // The shell ricochets as this bullet does.
        private const string RICOCHET_TEMPLATE = "Assets/Game/Prefabs/Projectiles/Bullet_01.prefab";
        private const string RPG_PREFAB = "Assets/Game/Prefabs/Weapons/RPG_01.prefab";
        private const string AMMO_ITEM_PREFAB = "Assets/Game/Prefabs/Items/AmmoItem.prefab";
        private const string BLAST_VFX_PREFAB = "Assets/Game/Prefabs/VFX/FX_RocketExplosion.prefab";
        private const string CHARACTER_PREFAB = "Assets/Game/Prefabs/Characters/[Character].prefab";
        private const string PLAYGROUND_SCENE = "Assets/Game/Scenes/Playground.unity";

        private const string VEHICLES_FOLDER = "Assets/Game/Prefabs/Vehicles";
        private const string TANK_PREFAB = VEHICLES_FOLDER + "/Tank_01.prefab";
        private const string SHELL_PREFAB = "Assets/Game/Prefabs/Projectiles/TankShell_01.prefab";

        // The shell's crumb spray: the rocket's, with twice as many small and medium crumbs. A copy
        // of its own, so the rocket launcher keeps its spray, and a pool of its own by name.
        private const string ROCKET_CRUMBS_PREFAB = "Assets/Game/Prefabs/VFX/FX_ExplosionCrumbs.prefab";
        private const string SHELL_CRUMBS_PREFAB = "Assets/Game/Prefabs/VFX/FX_TankShellCrumbs.prefab";
        private const int SHELL_CRUMBS_FACTOR = 2;
        // The crumbs fly ballistic — gravity, no drag — so range grows faster than speed: ×1.6 on
        // the launch speed carries them about twice as far. They live a fifth longer, so the
        // longest flights still land before they vanish, and the pooled effect outlives them.
        private const float SHELL_CRUMBS_SPEED = 1.6f;
        private const float SHELL_CRUMBS_LIFE = 1.2f;

        // Driving into a destructible breaks it about the point of contact, over the hull's half-width.
        private const float RAM_RADIUS = 1.1f;
        private const float RAM_MIN_SPEED = 1f;
        private const float RAM_SPLIT_CHANCE = 0.3f;

        private const string BODY_MATERIAL = "Assets/Game/Materials/MT_Tank_Body.mat";
        private const string TRACK_MATERIAL = "Assets/Game/Materials/MT_Tank_Track.mat";
        private const string PHYSICS_MATERIAL = "Assets/Game/Materials/PM_Tank.physicMaterial";
        // The model's other colours. Tread is the visible belt (links) and the voxel track; steel,
        // black and lens only colour voxels, their primitives are hidden once the skin is built.
        private const string TREAD_MATERIAL = "Assets/Game/Materials/MT_Tank_Tread.mat";
        private const string STEEL_MATERIAL = "Assets/Game/Materials/MT_Tank_Steel.mat";
        private const string BLACK_MATERIAL = "Assets/Game/Materials/MT_Tank_Black.mat";
        private const string LENS_MATERIAL = "Assets/Game/Materials/MT_Tank_Lens.mat";
        // One track link: chunky shoes in a unit cube, stretched along each span by TankTrackView.
        private const string LINK_MESH = VEHICLES_FOLDER + "/Tank_01_TrackLink.asset";

        private const int LAYER_CHARACTER = 9;
        private const int LAYER_WEAPON = 13;
        private const int INTERACT_MASK = (1 << 6) | (1 << LAYER_CHARACTER);

        // Tiger I in sand (dunkelgelb) with grey streaks, after the concept art.
        private static readonly Color BodyColour = new(0.72f, 0.57f, 0.34f);
        private static readonly Color TrackColour = new(0.13f, 0.13f, 0.12f);
        private static readonly Color TreadColour = new(0.23f, 0.21f, 0.2f);
        private static readonly Color SteelColour = new(0.3f, 0.295f, 0.29f);
        private static readonly Color BlackColour = new(0.06f, 0.06f, 0.065f);
        private static readonly Color LensColour = new(0.95f, 0.93f, 0.82f);
        private static readonly Color CamoGrey = new(0.46f, 0.45f, 0.43f);
        private static readonly Color CamoDark = new(0.36f, 0.355f, 0.345f);
        private static readonly Color CamoLight = new(0.8f, 0.66f, 0.42f);
        private static readonly Color DecalWhite = new(0.93f, 0.93f, 0.9f);
        private static readonly Color DecalBlack = new(0.05f, 0.05f, 0.055f);
        // Little shade noise: the concept's faces are flat colour, the camouflage does the work.
        private const float COLOR_NOISE = 0.06f;

        /// <summary>
        /// Every length below is the full-size design times this. The tank is built at three
        /// quarters of it; change this one number to resize all of it — model, colliders, wheels,
        /// springs, gun, exits and marker together.
        /// </summary>
        private const float S = 0.75f;

        /// <summary>
        /// One voxel of the skin: 4 cm at full size, 3 cm built. The model is voxel art — boxes laid
        /// on this grid in whole voxels (<see cref="V"/>), so every face lands on a voxel boundary
        /// and comes out straight.
        /// </summary>
        private const float VU = 0.04f * S;

        // Body. Mass is not scaled: the springs are worked out from it, and a heavy body is what
        // keeps flying bricks and floor chips from shoving the tank about.
        private const float MASS = 3000f;
        // The hull collider's bottom: well above the visible belly. In a puddle the tracks sink a
        // whole mud thickness (0.32 m) below the floor's collider (MudSurface.SinkAt); a box down at
        // the belly would sit on the floor first and hold the tank up out of the mud.
        private const float HULL_COLLIDER_BOTTOM = 0.6f * S;
        // The deck, in voxels and in metres.
        private const int DECK = 28;
        private const float HULL_TOP = DECK * VU;
        private const float HULL_WIDTH = 2.7f * S;
        private const float HULL_LENGTH = 3.7f * S;
        // Low and a touch to the rear, under the engine: the hull settles level on its springs.
        private static readonly Vector3 CentreOfMass = new(0f, 0.35f * S, -0.05f * S);
        private const float ANGULAR_DAMPING = 1f;

        // Running gear. The origin of the tank is on the ground when it rests on its springs: with
        // half the travel taken up by the weight the spring is 0.4 long, and the wheel centre sits
        // one wheel radius off the ground.
        private const float TRACK_X = 1.05f * S;
        // Eight stations a side, as the Tiger I's torsion bars (515 mm apart on the real one), front
        // to rear. Each carries one sprung wheel set; the springs share the weight between them
        // (SuspensionMath), so more stations do not make the ride stiffer.
        private static readonly float[] WheelZ =
            { 1.12f * S, 0.8f * S, 0.48f * S, 0.16f * S, -0.16f * S, -0.48f * S, -0.8f * S, -1.12f * S };
        private const float WHEEL_RADIUS = 0.3f * S;
        private const float MOUNT_HEIGHT = 0.7f * S;
        private const float SPRING_MAX = 0.55f * S;
        private const float SPRING_MIN = 0.25f * S;
        private const float STATIC_SAG = 0.5f;
        private const float DAMPING_RATIO = 0.4f;
        // N·s at the muzzle. 900 rocked the hull 2.3°, 4000 about 9°; this is meant to throw it about.
        private const float RECOIL_IMPULSE = 12000f;

        // The camera sits this much further out while driving.
        private const float CAMERA_DISTANCE = 1.1f;

        // The barrel's recoil: how far it runs back (full size, ×S), how fast, how long it sits back
        // and how long the recuperator takes to run it out again. Exaggerated on purpose: 0.4 m
        // snapping straight back was lost under the flash, the smoke and the shake.
        private const float GUN_RECOIL_DISTANCE = 0.8f * S;
        private const float GUN_KICK_TIME = 0.05f;
        private const float GUN_HOLD_TIME = 0.12f;
        private const float GUN_RETURN_TIME = 0.8f;

        // Track, at full size: the links lie on the ground under the wheels (TankTrackView puts a
        // link's bottom where the cast sphere meets it); the tyres, 6 voxels in radius, sink into
        // the links' top a little, as a wheel sits between the guide horns.
        private const float TRACK_THICKNESS = 0.1f;
        private const float TRACK_WIDTH = 0.5f;
        // Sprocket in front and idler behind, as on the Tiger, in voxels: their centres, where the
        // lower run starts and ends; the upper run lies on the wheels' tops.
        private const int SPROCKET_Z = 37;
        private const int IDLER_Z = -40;
        private const int TRACK_END_Y = 10;

        private const float TURRET_RING = HULL_TOP;
        // The turret, as a box that does not turn (AddColliders): its top a voxel under the roof.
        private const float TURRET_COLLIDER_HEIGHT = 0.52f * S;
        // The gun's axis, 6 voxels over the ring, and the muzzle a little ahead of the brake.
        private const float GUN_HEIGHT = (DECK + 6) * VU;
        private const float MUZZLE_Z = 81 * VU;
        private static readonly Vector3 CannonFirePoint = new(0f, GUN_HEIGHT, MUZZLE_Z);

        // The driver's capsule (radius 0.38) must clear the tracks at every exit.
        private static readonly Vector3 ExitLeft = new(-2.2f * S, 0f, 0f);
        private static readonly Vector3 ExitRight = new(2.2f * S, 0f, 0f);
        private static readonly Vector3 ExitBack = new(0f, 0f, -2.9f * S);

        // Where the "get in" marker hangs: just above the turret.
        private static readonly Vector3 MarkerOffset = new(0f, 2.8f * S, 0f);

        // Same as the character's camera point, so getting in does not change the framing.
        private static readonly Vector3 CameraPointPosition = new(0f, 0f, 1f);

        // Cannon and shell, from the spec.
        private const int CANNON_DAMAGE = 150;
        private const float CANNON_RELOAD = 2f;
        private const float CANNON_SHAKE_DURATION = 0.45f;
        private const float CANNON_SHAKE_STRENGTH = 0.75f;
        private const float SHELL_SPEED = 60f;
        private const float SHELL_LIFETIME = 2f;
        private const float SHELL_RADIUS = 5f;
        private const float SHELL_BREACH_RADIUS = 0.9f;
        // Largest angle between the shell's flight and a surface that still bounces it off.
        private const float SHELL_RICOCHET_MAX_ANGLE = 25f;

        // The spent cannon casing: a brass case thrown out of the turret on every shot, as the AR
        // throws its own. Full size; it lies on the floor for a while before it goes back to the pool.
        private const string CASING_PREFAB = "Assets/Game/Prefabs/Projectiles/TankCasing_01.prefab";
        private const string BRASS_MATERIAL = "Assets/Game/Materials/MT_Tank_Brass.mat";
        private const string CASING_PHYSICS_MATERIAL = "Assets/Game/Materials/PM_TankCasing.physicMaterial";
        private static readonly Color BrassColour = new(0.8f, 0.62f, 0.26f);
        // Half as large again as first built (×1.25, then ×1.2): at 0.55 × 0.13 it read as a rifle
        // round beside the tank.
        private const float CASING_SIZE = 1.5f;
        private const float CASING_LENGTH = 0.55f * CASING_SIZE;
        private const float CASING_DIAMETER = 0.13f * CASING_SIZE;
        private const float CASING_MASS = 0.8f;
        private const float CASING_LIFETIME = 10f;
        // rad/s it tumbles at as it leaves the turret, about 2.4 turns a second.
        private const float CASING_SPIN = 15f;
        // Right of the turret's rear, above its collider (top 1.68) with room for the casing's own
        // radius: a casing born inside the collider would be shot out by the solver.
        private static readonly Vector3 CasingPoint = new(0.6f, 1.8f, -0.45f);

        // Turret machine gun: the AR_02's bullet and damage, faster and tighter, endless belt.
        private const string MACHINE_GUN_TEMPLATE = "Assets/Game/Prefabs/Weapons/AR_02.prefab";
        private const string CANNON_SHOT_CLIP = "Assets/Game/Audio/etfx_shoot_grenade.wav";
        private const float CANNON_SHOT_VOLUME = 1f;
        private const string MACHINE_GUN_SHOT_CLIP = "Assets/Game/Audio/Weapons/Fire (MachineGun).wav";
        private const float MACHINE_GUN_SHOT_VOLUME = 0.6f;
        // Whose audio source a vehicle gun's shot sound copies: its mixer group and range.
        private const string SHOT_AUDIO_TEMPLATE = "Assets/Game/Prefabs/Weapons/AR_01.prefab";
        // Above the default 128 (lower is more important): a busy scene silences other sounds first.
        private const int SHOT_PRIORITY = 32;
        private const int MACHINE_GUN_DAMAGE = 10;
        private const float MACHINE_GUN_RATE = 0.08f;
        private const float MACHINE_GUN_SPREAD = 2f;
        // On a low post over the loader's hatch, full size: the barrel's axis. The muzzle stands
        // above the turret collider's top (1.68): that box does not turn with the turret, and turned
        // 45° a muzzle any lower would sit inside it and shoot the tank itself.
        private static readonly Vector3 MachineGunMount = new(0.38f, 1.78f, 0f);
        private const float MACHINE_GUN_MUZZLE_Z = 1.0f;
        // Its fire point, on the turret visual: just ahead of the muzzle.
        private static readonly Vector3 MachineGunFirePoint =
            new Vector3(MachineGunMount.x, MachineGunMount.y, MACHINE_GUN_MUZZLE_Z + 0.05f) * S - new Vector3(0f, TURRET_RING, 0f);

        private static readonly Vector3 PlaygroundPosition = new(-2.2f, 0f, -3.5f);

        [MenuItem("Tools/Tank/Build")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(VEHICLES_FOLDER))
                AssetDatabase.CreateFolder("Assets/Game/Prefabs", "Vehicles");

            Paints paints = LoadPaints();
            PhysicsMaterial slick = SlickMaterial();

            MonoEntity shell = BuildShell();
            MonoEntity casing = BuildCasing();
            MonoEntity smoke = TankFxBuilder.BuildMuzzle();
            GameObject tank = BuildTank(paints, slick, shell, casing, smoke);

            WidenInteractionOnCharacterPrefab();
            HookUpPlayground(tank);

            AssetDatabase.SaveAssets();
            Debug.Log($"{LOG} Built Tank_01 and TankShell_01.");
        }

        // ---------------------------------------------------------------- assets

        internal static Material SolidMaterial(string path, Color colour, float glossiness)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = colour;
            material.SetFloat("_Glossiness", glossiness);
            material.SetFloat("_Metallic", 0.25f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// The drive sets the velocity itself, so friction would only fight it — most of all when
        /// the hull turns on the spot. Minimum makes this win over whatever the ground carries.
        /// </summary>
        internal static PhysicsMaterial SlickMaterial()
        {
            PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PHYSICS_MATERIAL);

            if (material == null)
            {
                material = new PhysicsMaterial("PM_Tank");
                AssetDatabase.CreateAsset(material, PHYSICS_MATERIAL);
            }

            material.dynamicFriction = 0f;
            material.staticFriction = 0f;
            material.bounciness = 0f;
            material.frictionCombine = PhysicsMaterialCombine.Minimum;
            material.bounceCombine = PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// The shell bounces exactly as the bullets do: their ricochet count, angle and RayFire
        /// share are copied over, so retuning the bullets retunes the shell with the next build.
        /// </summary>
        private static void CopyRicochet(BulletInstaller from, BulletInstaller to)
        {
            SerializedObject source = new(from);
            SerializedObject target = new(to);

            foreach (string field in new[] { "_ricochetMaxCount", "_ricochetMaxAngle", "_ricochetImpactFactor" })
                target.CopyFromSerializedProperty(source.FindProperty(field));

            target.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The rocket's crumb spray with every burst doubled — twice the small crumbs thrown forward
        /// and back, twice the medium ones — and thrown about twice as far. ExplosionUseCase still
        /// tints them with the struck surface's material.
        /// </summary>
        private static MonoEntity BuildShellCrumbs()
        {
            AssetDatabase.DeleteAsset(SHELL_CRUMBS_PREFAB);
            AssetDatabase.CopyAsset(ROCKET_CRUMBS_PREFAB, SHELL_CRUMBS_PREFAB);

            GameObject root = PrefabUtility.LoadPrefabContents(SHELL_CRUMBS_PREFAB);

            try
            {
                root.name = "FX_TankShellCrumbs";

                float longest = 0f;

                foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = system.main;
                    main.maxParticles *= SHELL_CRUMBS_FACTOR;

                    ParticleSystem.MinMaxCurve speed = main.startSpeed;
                    speed.constantMin *= SHELL_CRUMBS_SPEED;
                    speed.constantMax *= SHELL_CRUMBS_SPEED;
                    main.startSpeed = speed;

                    ParticleSystem.MinMaxCurve life = main.startLifetime;
                    life.constantMin *= SHELL_CRUMBS_LIFE;
                    life.constantMax *= SHELL_CRUMBS_LIFE;
                    main.startLifetime = life;
                    longest = Mathf.Max(longest, life.constantMax);

                    var emission = system.emission;
                    var bursts = new ParticleSystem.Burst[emission.burstCount];
                    emission.GetBursts(bursts);

                    for (int i = 0; i < bursts.Length; i++)
                    {
                        // constant is the same field as constantMax: scaling it too would quadruple.
                        ParticleSystem.MinMaxCurve count = bursts[i].count;
                        count.constantMin *= SHELL_CRUMBS_FACTOR;
                        count.constantMax *= SHELL_CRUMBS_FACTOR;
                        bursts[i].count = count;
                    }

                    emission.SetBursts(bursts);
                }

                // The pooled slot must outlast the longest-lived crumb, or it vanishes mid-flight.
                HitEffectInstaller effect = root.GetComponent<HitEffectInstaller>();
                SerializedObject pooled = new(effect);
                SerializedProperty lifeTime = pooled.FindProperty("_lifeTime");
                lifeTime.floatValue = Mathf.Max(lifeTime.floatValue, longest + 0.3f);
                pooled.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, SHELL_CRUMBS_PREFAB).GetComponent<MonoEntity>();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static MonoEntity BuildShell()
        {
            AssetDatabase.DeleteAsset(SHELL_PREFAB);
            AssetDatabase.CopyAsset(ROCKET_PREFAB, SHELL_PREFAB);

            GameObject root = PrefabUtility.LoadPrefabContents(SHELL_PREFAB);

            try
            {
                root.name = "TankShell_01";

                BulletInstaller bullet = root.GetComponent<BulletInstaller>();
                SetFloat(bullet, "_moveSpeed", SHELL_SPEED);
                SetFloat(bullet, "_lifeTime", SHELL_LIFETIME);

                ExplosionInstaller explosion = root.GetComponent<ExplosionInstaller>();
                SetFloat(explosion, "_radius", SHELL_RADIUS);
                SetFloat(explosion, "_breachRadius", SHELL_BREACH_RADIUS);
                SetField(explosion, "_crumbsPrefab", BuildShellCrumbs());

                CopyRicochet(AssetDatabase.LoadAssetAtPath<GameObject>(RICOCHET_TEMPLATE).GetComponent<BulletInstaller>(), bullet);
                // Not the bullets' 90°: at that angle the shell bounced off every wall it met and
                // never exploded on the first one, so walls only broke on its third contact. Only a
                // glancing hit bounces; anything steeper explodes and breaches the wall.
                SetFloat(bullet, "_ricochetMaxAngle", SHELL_RICOCHET_MAX_ANGLE);

                return PrefabUtility.SaveAsPrefabAsset(root, SHELL_PREFAB).GetComponent<MonoEntity>();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// A big brass casing on a dynamic body, with the stock CasingInstaller so it is pooled and
        /// timed out like the rifle casings. RangeWeaponFireAction throws it up and out to the right
        /// of the casing point on every shot. It is a little bouncy so it rings off the hull and the
        /// floor instead of landing dead.
        /// </summary>
        private static MonoEntity BuildCasing()
        {
            Material brass = SolidMaterial(BRASS_MATERIAL, BrassColour, 0.75f);
            brass.SetFloat("_Metallic", 0.85f);

            PhysicsMaterial clang = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(CASING_PHYSICS_MATERIAL);
            if (clang == null)
            {
                clang = new PhysicsMaterial("PM_TankCasing");
                AssetDatabase.CreateAsset(clang, CASING_PHYSICS_MATERIAL);
            }
            clang.bounciness = 0.3f;
            clang.dynamicFriction = 0.4f;
            clang.staticFriction = 0.5f;
            clang.bounceCombine = PhysicsMaterialCombine.Maximum;
            clang.frictionCombine = PhysicsMaterialCombine.Average;
            EditorUtility.SetDirty(clang);

            GameObject root = new GameObject("TankCasing_01");

            try
            {
                Rigidbody body = root.AddComponent<Rigidbody>();
                body.mass = CASING_MASS;
                body.linearDamping = 0.05f;
                body.angularDamping = 0.3f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
                collider.direction = 2;
                collider.radius = CASING_DIAMETER * 0.5f * S;
                collider.height = CASING_LENGTH * S;
                collider.sharedMaterial = clang;

                // Lying along +Z: the case, the wider rim at the back, the primer in the rim.
                float half = CASING_LENGTH * 0.5f;
                float rim = 0.012f * CASING_SIZE;
                Part(PrimitiveType.Cylinder, "Case", root.transform, brass, Vector3.zero, new Vector3(90f, 0f, 0f),
                    new Vector3(CASING_DIAMETER, half, CASING_DIAMETER));
                Part(PrimitiveType.Cylinder, "Rim", root.transform, brass, new Vector3(0f, 0f, -half), new Vector3(90f, 0f, 0f),
                    new Vector3(CASING_DIAMETER * 1.18f, rim, CASING_DIAMETER * 1.18f));
                Material dark = AssetDatabase.LoadAssetAtPath<Material>(TRACK_MATERIAL);
                Part(PrimitiveType.Cylinder, "Primer", root.transform, dark, new Vector3(0f, 0f, -half - rim),
                    new Vector3(90f, 0f, 0f), new Vector3(0.27f * CASING_DIAMETER, rim / 3f, 0.27f * CASING_DIAMETER));

                MonoEntity entity = root.AddComponent<MonoEntity>();
                CasingInstaller installer = root.AddComponent<CasingInstaller>();
                SetField(installer, "_rb", body);
                SetFloat(installer, "_lifeTime", CASING_LIFETIME);
                SetFloat(installer, "_spin", CASING_SPIN);
                SetInstallers(entity, installer);

                return PrefabUtility.SaveAsPrefabAsset(root, CASING_PREFAB).GetComponent<MonoEntity>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---------------------------------------------------------------- tank

        private static GameObject BuildTank(Paints paints, PhysicsMaterial slick, MonoEntity shell,
            MonoEntity casing, MonoEntity smoke)
        {
            GameObject root = new GameObject("Tank_01") { layer = LAYER_CHARACTER };

            try
            {
                Rigidbody rigidbody = root.AddComponent<Rigidbody>();
                rigidbody.mass = MASS;
                rigidbody.linearDamping = 0f;
                rigidbody.angularDamping = ANGULAR_DAMPING;
                rigidbody.useGravity = true;
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                // Free to pitch and roll on the springs; TankSuspensionBehaviour stops it turning over.
                rigidbody.constraints = RigidbodyConstraints.None;
                rigidbody.centerOfMass = CentreOfMass;

                AddColliders(root, slick);
                CollisionContactReceiver contacts = root.AddComponent<CollisionContactReceiver>();

                Transform hull = Child(root.transform, "HullVisual", Vector3.zero);
                ParticleSystem[] exhaust = TankFxBuilder.BuildExhaust(hull, paints.Steel, S);
                BuildHullModel(hull, paints);
                Transform turretVisual = Child(hull, "TurretVisual", new Vector3(0f, TURRET_RING, 0f));
                BuildTurretModel(turretVisual, paints);

                BuildRunningGear(root.transform, hull, paints,
                    out Transform[] mounts, out Transform[] wheels, out Transform[][] ends, out Transform[][] links);

                Transform pivot = Child(root.transform, "TurretPivot", Vector3.zero);
                MonoEntity cannon = BuildCannon(pivot, shell, casing);
                ParticleSystem wisp = TankFxBuilder.BuildWisp(cannon.transform.Find("FirePoint"));
                MonoEntity machineGun = BuildMachineGun(turretVisual);
                Transform gunVisual = turretVisual.Find("Gun");

                Transform cameraPoint = Child(root.transform, "CameraPoint", CameraPointPosition);

                Transform exits = Child(root.transform, "ExitPoints", Vector3.zero);
                Transform[] exitPoints =
                {
                    Child(exits, "Left", ExitLeft),
                    Child(exits, "Right", ExitRight),
                    Child(exits, "Back", ExitBack)
                };

                HighlightEffect highlight = AddHighlight(hull);

                // The primitives are rebuilt as voxels when the tank starts (shells chew them off),
                // and the tank crushes voxel walls it drives into.
                ConfigureSkin(root.AddComponent<Game.Voxels.VoxelizedModel>(), paints);
                root.AddComponent<Game.Voxels.VoxelRammer>();

                MonoEntity entity = root.AddComponent<MonoEntity>();
                TankInstaller installer = root.AddComponent<TankInstaller>();
                SetField(installer, "_turretPivot", pivot);
                SetField(installer, "_turretVisual", turretVisual);
                SetField(installer, "_cannon", cannon);
                SetField(installer, "_machineGun", machineGun);
                SetField(installer, "_gunVisual", gunVisual);
                SetFloat(installer, "_gunRecoilDistance", GUN_RECOIL_DISTANCE);
                SetFloat(installer, "_gunKickTime", GUN_KICK_TIME);
                SetFloat(installer, "_gunHoldTime", GUN_HOLD_TIME);
                SetFloat(installer, "_gunReturnTime", GUN_RETURN_TIME);
                SetField(installer, "_muzzleSmoke", smoke);
                SetField(installer, "_muzzleWisp", wisp);
                SetArray(installer, "_exhaust", exhaust);
                SetFloat(installer, "_cameraDistance", CAMERA_DISTANCE);
                SetField(installer, "_rigidbody", rigidbody);
                SetField(installer, "_hullVisual", hull);
                SetField(installer, "_cameraPoint", cameraPoint);
                SetArray(installer, "_exitPoints", exitPoints);
                SetField(installer, "_highlight", highlight);
                SetField(installer, "_explosionVfx",
                    AssetDatabase.LoadAssetAtPath<GameObject>(BLAST_VFX_PREFAB).GetComponent<MonoEntity>());
                SetShake(installer, "_destroyShake", 0.45f, 0.4f);
                SetVector(installer, "_markerOffset", MarkerOffset);

                SetArray(installer, "_wheelMounts", mounts);
                SetArray(installer, "_wheelVisuals", wheels);
                SetFloat(installer, "_wheelRadius", WHEEL_RADIUS);
                SetFloat(installer, "_springMaxLength", SPRING_MAX);
                SetFloat(installer, "_springMinLength", SPRING_MIN);
                SetFloat(installer, "_staticSag", STATIC_SAG);
                SetFloat(installer, "_dampingRatio", DAMPING_RATIO);
                SetFloat(installer, "_recoilImpulse", RECOIL_IMPULSE);
                SetField(installer, "_contacts", contacts);
                SetFloat(installer, "_ramRadius", RAM_RADIUS);
                SetFloat(installer, "_ramMinSpeed", RAM_MIN_SPEED);
                SetFloat(installer, "_ramSplitChance", RAM_SPLIT_CHANCE);
                SetTrack(installer, "_leftTrack", ends[0][0], ends[0][1], links[0]);
                SetTrack(installer, "_rightTrack", ends[1][0], ends[1][1], links[1]);

                SetInstallers(entity, installer);

                return PrefabUtility.SaveAsPrefabAsset(root, TANK_PREFAB);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void AddColliders(GameObject root, PhysicsMaterial slick)
        {
            BoxCollider hull = root.AddComponent<BoxCollider>();
            BoxCollider turret = root.AddComponent<BoxCollider>();
            ShapeColliders(hull, turret);
            hull.sharedMaterial = slick;
            turret.sharedMaterial = slick;
        }

        private static void ShapeColliders(BoxCollider hull, BoxCollider turret)
        {
            float hullHeight = HULL_TOP - HULL_COLLIDER_BOTTOM;
            hull.center = new Vector3(0f, HULL_COLLIDER_BOTTOM + hullHeight * 0.5f, 0f);
            hull.size = new Vector3(HULL_WIDTH, hullHeight, HULL_LENGTH);

            // The turret, as a box on the hull. It stays put while the turret turns: a square box
            // round the long turret is close enough, and a collider that turned would need a body
            // of its own.
            turret.center = new Vector3(0f, TURRET_RING + TURRET_COLLIDER_HEIGHT * 0.5f, -0.1f * S);
            turret.size = new Vector3(1.6f * S, TURRET_COLLIDER_HEIGHT, 1.6f * S);
        }

        // ---------------------------------------------------------------- model

        /// <summary>The model's materials: their colours become the voxels' colours.</summary>
        private struct Paints
        {
            /// <summary>Sand paint, under the camouflage.</summary>
            public Material Body;
            /// <summary>Tracks and tyres; the belt's links keep it as meshes.</summary>
            public Material Tread;
            /// <summary>Bare metal: hubs, grilles, antenna, exhausts.</summary>
            public Material Steel;
            /// <summary>Slits, the bore, holes.</summary>
            public Material Black;
            public Material Lens;
        }

        private static Paints LoadPaints()
        {
            Material body = SolidMaterial(BODY_MATERIAL, BodyColour, 0.3f);
            Material track = SolidMaterial(TRACK_MATERIAL, TrackColour, 0.1f);
            Material tread = SolidMaterial(TREAD_MATERIAL, TreadColour, 0.1f);
            // Mud cakes onto the hull and running gear (TankDirtBehaviour).
            body.shader = Shader.Find("Game/TankDirt");
            track.shader = Shader.Find("Game/TankDirt");
            tread.shader = Shader.Find("Game/TankDirt");

            return new Paints
            {
                Body = body,
                Tread = tread,
                Steel = SolidMaterial(STEEL_MATERIAL, SteelColour, 0.35f),
                Black = SolidMaterial(BLACK_MATERIAL, BlackColour, 0.1f),
                Lens = SolidMaterial(LENS_MATERIAL, LensColour, 0.8f)
            };
        }

        // The turret's number and its crosses, one character per voxel.
        private static readonly string[] Number131 =
        {
            ".#.####..#",
            "##....#.##",
            ".#....#..#",
            ".#..###..#",
            ".#....#..#",
            ".#....#..#",
            ".#.####..#"
        };

        private static readonly string[] Cross =
        {
            "...WKKW...",
            "...WKKW...",
            "...WKKW...",
            "WWWWKKWWWW",
            "KKKKKKKKKK",
            "KKKKKKKKKK",
            "WWWWKKWWWW",
            "...WKKW...",
            "...WKKW...",
            "...WKKW..."
        };

        /// <summary>
        /// The voxel skin: 3 cm voxels with little shade noise, sand under grey streaks, the number
        /// and a cross on each side of the turret. The upper run is voxels with the hull; only the
        /// lower run's links stay meshes, as TankTrackView slides them.
        /// </summary>
        private static void ConfigureSkin(Game.Voxels.VoxelizedModel skin, Paints paints)
        {
            var camouflage = new Game.Voxels.VoxelizedModel.Camouflage
            {
                Paint = paints.Body,
                Patch = CamoGrey,
                Dark = CamoDark,
                Light = CamoLight,
                Frequency = 1.5f,
                Threshold = 0.2f
            };

            var decals = new List<Game.Voxels.VoxelizedModel.Decal>();
            for (int side = -1; side <= 1; side += 2)
            {
                // Turret-local voxels on the flat side: the number on z -7..2, y 4..10, behind the vision
                // port; the cross behind the number on z -19..-10, y 3..12.
                decals.Add(new Game.Voxels.VoxelizedModel.Decal
                {
                    Part = "HullVisual/TurretVisual", Side = side, Center = new Vector2(-2f, 7.5f) * VU,
                    Rows = Number131, Keys = "#", Colors = new[] { DecalWhite }
                });
                decals.Add(new Game.Voxels.VoxelizedModel.Decal
                {
                    Part = "HullVisual/TurretVisual", Side = side, Center = new Vector2(-14f, 8f) * VU,
                    Rows = Cross, Keys = "WK", Colors = new[] { DecalWhite, DecalBlack }
                });
            }

            skin.Configure(new[] { "HullVisual/Track?_Link*" }, VU, COLOR_NOISE, camouflage, decals.ToArray());
        }

        /// <summary>
        /// The hull, after the Tiger I and the concept art: straight boxes of whole voxels. A belly
        /// between the tracks; the superstructure over them with straight vertical sides out to the
        /// track's edge and a thin fender along their foot; the nose ahead of the front plate with
        /// spare track links; visor, ball mount, headlight; hatches, engine deck, antenna, exhausts.
        /// </summary>
        /// <remarks>Hull voxels: x across (right +), y up from the ground, z forward; the deck at <see cref="DECK"/>.</remarks>
        private static void BuildHullModel(Transform hull, Paints p)
        {
            // Proportions from a Tiger I side drawing at 7.35 px a voxel: front plate at z 33, rear
            // plate at -44, the nose to 45, deck at 28, the side fender band from 16 to 21.
            V(hull, "LowerHull", p.Body, -20, 7, -44, 20, 24, 33);
            V(hull, "Superstructure", p.Body, -33, 16, -44, 33, DECK, 33);
            V(hull, "Nose", p.Body, -20, 8, 33, 20, 18, 45);
            V(hull, "NoseBottom", p.Body, -20, 7, 33, 20, 8, 41);
            V(hull, "Glacis", p.Body, -20, 18, 33, 20, 21, 38);

            for (int s = -1; s <= 1; s += 2)
            {
                string side = s < 0 ? "L" : "R";
                // The side fender: a straight band a voxel proud along the foot of the vertical side;
                // mudguards stepping down over the sprocket, and one over the idler.
                VS(hull, $"Fender{side}", p.Body, s, 33, 16, -44, 34, 21, 33);
                VS(hull, $"FrontGuard{side}", p.Body, s, 20, 19, 33, 34, 21, 40);
                VS(hull, $"FrontGuardTip{side}", p.Body, s, 20, 17, 40, 34, 19, 46);
                VS(hull, $"RearGuard{side}", p.Body, s, 20, 16, -47, 34, 18, -44);
                // Tow shackles on the nose.
                VS(hull, $"Shackle{side}", p.Body, s, 13, 8, 45, 17, 12, 47);
                VS(hull, $"ShackleEye{side}", p.Black, s, 14, 9, 46, 16, 11, 47);
                // Tools on the side above the fender: cable, shovel, clamps.
                VS(hull, $"Cable{side}", p.Steel, s, 33, 24, -38, 34, 25, 18);
                VS(hull, $"Shovel{side}", p.Tread, s, 33, 23, 20, 34, 26, 30);
                VS(hull, $"ClampFront{side}", p.Body, s, 33, 23, 2, 34, 26, 4);
                VS(hull, $"ClampRear{side}", p.Body, s, 33, 23, -22, 34, 26, -20);
                // Tall exhaust housings on the rear plate, round TankFxBuilder's pipes, above the deck.
                VS(hull, $"ExhaustHousing{side}", p.Steel, s, 10, 12, -48, 17, DECK + 3, -44);
                VS(hull, $"ExhaustCap{side}", p.Tread, s, 11, DECK + 3, -47, 16, DECK + 4, -45);
                // Engine deck: grille slats and a fan cover each side.
                for (int k = 0; k < 5; k++)
                    VS(hull, $"Grille{side}{k}", p.Tread, s, 9, DECK, -30 + k * 2, 24, DECK + 1, -29 + k * 2);
                VS(hull, $"Fan{side}", p.Steel, s, 10, DECK, -42, 22, DECK + 1, -32);
                VS(hull, $"FanRim{side}", p.Body, s, 12, DECK + 1, -40, 20, DECK + 2, -34);
                // Driver's and radio operator's hatches with their periscopes.
                VS(hull, $"Hatch{side}", p.Body, s, 11, DECK, 20, 20, DECK + 1, 29);
                VS(hull, $"HatchHinge{side}", p.Steel, s, 12, DECK + 1, 20, 19, DECK + 2, 21);
                VS(hull, $"Periscope{side}", p.Steel, s, 14, DECK, 29, 17, DECK + 2, 31);
            }

            V(hull, "EngineHatch", p.Body, -6, DECK, -42, 6, DECK + 2, -31);
            V(hull, "EngineHatchHandle", p.Steel, -2, DECK + 2, -38, 2, DECK + 3, -37);

            // Front plate: the driver's visor on the left, the ball mount on the right, the headlight.
            V(hull, "Visor", p.Body, -20, 22, 33, -10, 26, 35);
            V(hull, "VisorSlit", p.Black, -18, 23, 34, -12, 24, 35);
            V(hull, "VisorBrow", p.Body, -21, 26, 33, -9, 27, 36);
            V(hull, "BallMount", p.Body, 12, 21, 33, 18, 26, 35);
            V(hull, "BallMountRim", p.Body, 13, 22, 35, 17, 25, 36);
            V(hull, "BallMountGun", p.Black, 14, 23, 35, 16, 24, 36);
            V(hull, "Headlight", p.Steel, 2, 25, 33, 7, 29, 36);
            V(hull, "HeadlightLens", p.Lens, 3, 26, 35, 6, 28, 36);

            // Spare track links: two rows lying on the nose, three hung on the lower front plate.
            for (int i = 0; i < 7; i++)
            {
                V(hull, $"SpareDeckA{i}", p.Tread, -17 + i * 5, 18, 38, -13 + i * 5, 19, 41);
                V(hull, $"SpareDeckB{i}", p.Tread, -15 + i * 5, 18, 41, -11 + i * 5, 19, 44);
            }

            for (int row = 0; row < 3; row++)
            for (int i = 0; i < 7; i++)
            {
                int x = -17 + i * 5 + (row == 1 ? 2 : 0);
                if (x + 4 <= 19)
                    V(hull, $"SpareFront{row}{i}", p.Tread, x, 15 - row * 3, 45, x + 4, 17 - row * 3, 46);
            }

            // Antenna, rear right behind the turret: base, mast, and a rod one voxel thin.
            V(hull, "AntennaBase", p.Steel, 19, DECK, -36, 23, DECK + 2, -32);
            V(hull, "AntennaMast", p.Steel, 20, DECK + 2, -35, 22, DECK + 9, -33);
            V(hull, "AntennaRod", p.Steel, 20, DECK + 9, -35, 21, DECK + 34, -34);
        }

        /// <summary>
        /// The turret: a long horseshoe in voxel steps with flat sides for the number and the cross,
        /// the commander's cupola rear left, the loader's hatch, vision ports and smoke dischargers,
        /// the mantlet, and the gun — barrel and double-baffle brake in the "Gun" group that recoils.
        /// </summary>
        /// <remarks>Turret voxels: y up from the deck, z from the turret's pivot.</remarks>
        private static void BuildTurretModel(Transform turret, Paints p)
        {
            V(turret, "Ring", p.Steel, -15, 0, -18, 15, 1, 12);
            // Footprint rows (z from, z to, half-width): a horseshoe, narrow at the mantlet, the flat
            // sides z -19..5 carrying the number and the cross, rounded behind.
            int[,] rows =
            {
                { -27, -24, 12 }, { -24, -21, 16 }, { -21, -19, 19 }, { -19, 5, 20 }, { 5, 9, 19 }, { 9, 13, 18 },
                { 13, 17, 16 }
            };
            for (int r = 0; r < rows.GetLength(0); r++)
                V(turret, $"Body{r}", p.Body, -rows[r, 2], 1, rows[r, 0], rows[r, 2], 14, rows[r, 1]);

            V(turret, "RoofFront", p.Body, -10, 14, 5, 10, 15, 15);
            V(turret, "Vent", p.Steel, 2, 15, 8, 5, 16, 11);
            V(turret, "LoaderHatch", p.Body, 4, 14, -6, 13, 15, 3);
            V(turret, "LoaderHinge", p.Steel, 5, 15, -6, 12, 16, -5);

            // Commander's cupola, left of centre: low and wide as on the drawing, its corners cut,
            // vision slits all round, cap and hatch.
            V(turret, "Cupola", p.Body, -15, 14, -15, -3, 17, -3);
            V(turret, "CupolaX", p.Body, -16, 14, -14, -2, 17, -4);
            V(turret, "CupolaZ", p.Body, -14, 14, -16, -4, 17, -2);
            for (int k = 0; k < 3; k++)
            {
                V(turret, $"SlitFront{k}", p.Black, -14 + k * 4, 15, -3, -12 + k * 4, 16, -2);
                V(turret, $"SlitBack{k}", p.Black, -14 + k * 4, 15, -16, -12 + k * 4, 16, -15);
                V(turret, $"SlitLeft{k}", p.Black, -16, 15, -14 + k * 4, -15, 16, -12 + k * 4);
                V(turret, $"SlitRight{k}", p.Black, -3, 15, -14 + k * 4, -2, 16, -12 + k * 4);
            }

            V(turret, "CupolaCap", p.Body, -14, 17, -14, -4, 18, -4);
            V(turret, "CupolaHatch", p.Steel, -12, 18, -12, -6, 19, -6);

            for (int s = -1; s <= 1; s += 2)
            {
                string side = s < 0 ? "L" : "R";
                // A vision port ahead of the number; three smoke dischargers on each front corner.
                VS(turret, $"Port{side}", p.Body, s, 17, 4, 8, 21, 11, 12);
                VS(turret, $"PortSlitTop{side}", p.Black, s, 20, 8, 9, 21, 9, 11);
                VS(turret, $"PortSlitBottom{side}", p.Black, s, 20, 5, 9, 21, 6, 11);
                for (int k = 0; k < 3; k++)
                    VS(turret, $"Smoke{side}{k}", p.Steel, s, 16, 7 + k * 2, 13, 18, 8 + k * 2, 16);
            }

            // Mantlet with the coaxial gun and the sight.
            V(turret, "Mantlet", p.Body, -12, 1, 17, 12, 13, 22);
            V(turret, "MantletCheekL", p.Body, -14, 2, 17, -12, 12, 20);
            V(turret, "MantletCheekR", p.Body, 12, 2, 17, 14, 12, 20);
            V(turret, "MantletFront", p.Body, -10, 2, 22, 10, 12, 23);
            V(turret, "Coax", p.Black, 6, 7, 22, 8, 9, 23);
            V(turret, "Sight", p.Black, -8, 7, 22, -6, 9, 23);

            // Barrel and brake slide back together through the mantlet on a shot
            // (GunRecoilBehaviour). Axis 6 voxels over the deck (GUN_HEIGHT): the thick first
            // length out of the mantlet, the long barrel with its corners cut, the double-baffle
            // brake with a port a side in each baffle and the bore; the muzzle point just ahead (MUZZLE_Z).
            Transform gun = Child(turret, "Gun", Vector3.zero);
            V(gun, "BarrelBase", p.Body, -2, 4, 23, 2, 8, 35);
            V(gun, "Barrel", p.Body, -2, 5, 35, 2, 7, 73);
            V(gun, "BarrelCross", p.Body, -1, 4, 35, 1, 8, 73);
            V(gun, "Collar", p.Steel, -2, 4, 71, 2, 8, 73);
            V(gun, "Brake", p.Body, -3, 4, 73, 3, 8, 79);
            V(gun, "BrakeCross", p.Body, -2, 3, 73, 2, 9, 79);
            V(gun, "BrakePortRear", p.Black, -3, 5, 74, 3, 7, 75);
            V(gun, "BrakePortFront", p.Black, -3, 5, 77, 3, 7, 78);
            V(gun, "Bore", p.Black, -1, 5, 78, 1, 7, 79);

            // The roof machine gun's look, on a low post over the loader's hatch (MachineGunMount).
            // "MachineGunBody", not "MachineGun": that name belongs to the weapon entity beside it.
            V(turret, "MachineGunPost", p.Steel, 8, 14, -4, 10, 15, -2);
            V(turret, "MachineGunBody", p.Steel, 8, 15, -6, 11, 17, 4);
            V(turret, "MachineGunBarrel", p.Steel, 9, 16, 4, 10, 17, 25);
        }

        /// <summary>
        /// Eight sprung stations a side, interleaved as on the Tiger I, all inside the track's width:
        /// four rows of tyres across it, the even stations (2, 4, 6, 8) in the outer and third rows,
        /// the odd ones in the second and inner rows — neighbours never share a row, so their wheels
        /// pass between each other. The outer wheels' discs end flush with the track's outer edge;
        /// the odd stations' sit three voxels further in. Each station is one wheel set on one axle:
        /// it spins and rides its own spring.
        ///
        /// The sprocket is in front and the idler behind, both wrapped in track; the upper run lies on
        /// the wheels under the hull's side; the lower run is links laid at rest from the sprocket
        /// under every wheel to the idler, and TankTrackView keeps them following the wheels.
        /// </summary>
        private static void BuildRunningGear(Transform root, Transform hull, Paints p,
            out Transform[] mounts, out Transform[] wheels, out Transform[][] ends, out Transform[][] links)
        {
            Transform suspension = Child(root, "Suspension", Vector3.zero);
            Mesh linkMesh = TrackLinkMesh();

            int perSide = WheelZ.Length;
            mounts = new Transform[perSide * 2];
            wheels = new Transform[perSide * 2];
            ends = new Transform[2][];
            links = new Transform[2][];

            for (int s = 0; s < 2; s++)
            {
                int side = s == 0 ? -1 : 1;
                string tag = s == 0 ? "L" : "R";
                float x = side * TRACK_X;

                for (int i = 0; i < perSide; i++)
                {
                    int index = s * perSide + i;
                    mounts[index] = Child(suspension, $"Mount_{tag}{i + 1}", new Vector3(x, MOUNT_HEIGHT, WheelZ[i]));

                    // Wheel voxels across the track, outward positive; the track's edge is at 6.25.
                    Transform wheel = Child(hull, $"Wheel_{tag}{i + 1}", new Vector3(x, WHEEL_RADIUS, WheelZ[i]));
                    if (i % 2 == 1)
                    {
                        Disc(wheel, "Tyre", p.Tread, side, 3, 5, 6f);
                        Disc(wheel, "Disc", p.Body, side, 4, 6, 5f);
                        Disc(wheel, "Hub", p.Steel, side, 5, 6, 2f);
                        Disc(wheel, "HubCap", p.Body, side, 5, 6, 1f);
                        Disc(wheel, "InnerTyre", p.Tread, side, -3, 0, 6f);
                    }
                    else
                    {
                        Disc(wheel, "Tyre", p.Tread, side, 0, 2, 6f);
                        Disc(wheel, "Disc", p.Body, side, 1, 3, 5f);
                        Disc(wheel, "Hub", p.Steel, side, 2, 3, 2f);
                        Disc(wheel, "InnerTyre", p.Tread, side, -6, -3, 6f);
                    }

                    wheels[index] = wheel;
                }

                Vector3 frontAt = new(x, TRACK_END_Y * VU, SPROCKET_Z * VU);
                Vector3 rearAt = new(x, TRACK_END_Y * VU, IDLER_Z * VU);

                Transform front = Child(hull, $"Track{tag}_Front", frontAt);
                Disc(front, "Wrap", p.Tread, side, -6, 6, 6f);
                Disc(front, "Sprocket", p.Steel, side, 5, 6, 5f);
                for (int k = 0; k < 8; k++)
                {
                    float angle = k * 45f;
                    GameObject tooth = Primitive(PrimitiveType.Cube, $"Tooth{k}", front, p.Steel);
                    tooth.transform.SetLocalPositionAndRotation(
                        new Vector3(side * 5.5f, Mathf.Cos(angle * Mathf.Deg2Rad) * 5.5f, Mathf.Sin(angle * Mathf.Deg2Rad) * 5.5f) * VU,
                        Quaternion.Euler(-angle, 0f, 0f));
                    tooth.transform.localScale = new Vector3(1f, 1.5f, 1.6f) * VU;
                }

                Disc(front, "SprocketHub", p.Body, side, 5, 6, 2.5f);
                Disc(front, "SprocketCap", p.Steel, side, 5, 6, 1.2f);

                Transform rear = Child(hull, $"Track{tag}_Rear", rearAt);
                Disc(rear, "Wrap", p.Tread, side, -6, 6, 6f);
                Disc(rear, "Idler", p.Body, side, 5, 6, 5f);
                Disc(rear, "IdlerHub", p.Steel, side, 5, 6, 2f);
                ends[s] = new[] { front, rear };

                // Upper run on the wheels' tops under the hull's side, grousers along its outer edge.
                VS(hull, $"Track{tag}_Upper", p.Tread, side, 20, 14, IDLER_Z, 32, 16, SPROCKET_Z);
                for (int z = IDLER_Z; z < SPROCKET_Z; z += 4)
                    VS(hull, $"Track{tag}_Grouser{z}", p.Tread, side, 32, 13, z, 33, 16, z + 2);

                // At rest: from the sprocket, under each wheel (the link's bottom on the ground), to the idler.
                Vector3[] chain = new Vector3[perSide + 2];
                chain[0] = frontAt;
                for (int i = 0; i < perSide; i++)
                    chain[i + 1] = new Vector3(x, TRACK_THICKNESS * S * 0.5f, WheelZ[i]);
                chain[perSide + 1] = rearAt;

                links[s] = new Transform[perSide + 1];
                for (int i = 0; i < perSide + 1; i++)
                {
                    Vector3 span = chain[i + 1] - chain[i];
                    GameObject link = Primitive(PrimitiveType.Cube, $"Track{tag}_Link{i + 1}", hull, p.Tread);
                    link.GetComponent<MeshFilter>().sharedMesh = linkMesh;
                    link.transform.SetLocalPositionAndRotation((chain[i] + chain[i + 1]) * 0.5f, Quaternion.LookRotation(span));
                    link.transform.localScale = new Vector3(TRACK_WIDTH * S, TRACK_THICKNESS * S, span.magnitude);
                    links[s][i] = link.transform;
                }
            }
        }

        /// <summary>
        /// One link of the lower run in a unit cube, as TankTrackView stretches it from wheel to
        /// wheel: three shoes with gaps between them, a grouser under each and end connectors at
        /// the sides, so the belt reads as chunky track. An asset, so the prefab keeps it.
        /// </summary>
        private static Mesh TrackLinkMesh()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(LINK_MESH);
            if (mesh == null)
            {
                mesh = new Mesh { name = "Tank_01_TrackLink" };
                AssetDatabase.CreateAsset(mesh, LINK_MESH);
            }

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            Vector3[] faces = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

            void Box(Vector3 min, Vector3 max)
            {
                Vector3 centre = (min + max) * 0.5f;
                Vector3 extent = (max - min) * 0.5f;

                foreach (Vector3 normal in faces)
                {
                    Vector3 a = normal.x != 0f ? Vector3.up : Vector3.right;
                    Vector3 b = Vector3.Cross(normal, a);
                    Vector3 face = centre + Vector3.Scale(normal, extent);
                    Vector3 ea = Vector3.Scale(a, extent);
                    Vector3 eb = Vector3.Scale(b, extent);

                    int first = vertices.Count;
                    vertices.Add(face - ea - eb);
                    vertices.Add(face + ea - eb);
                    vertices.Add(face + ea + eb);
                    vertices.Add(face - ea + eb);
                    for (int k = 0; k < 4; k++)
                        normals.Add(normal);
                    uvs.Add(new Vector2(0f, 0f));
                    uvs.Add(new Vector2(1f, 0f));
                    uvs.Add(new Vector2(1f, 1f));
                    uvs.Add(new Vector2(0f, 1f));
                    triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
                }
            }

            const int shoes = 3;
            const float gap = 0.06f;
            for (int k = 0; k < shoes; k++)
            {
                float z0 = -0.5f + k / (float)shoes + gap * 0.5f;
                float z1 = z0 + 1f / shoes - gap;
                float mid = (z0 + z1) * 0.5f;
                float grouser = (z1 - z0) * 0.25f;

                Box(new Vector3(-0.5f, -0.1f, z0), new Vector3(0.5f, 0.5f, z1));
                Box(new Vector3(-0.5f, -0.5f, mid - grouser), new Vector3(0.5f, -0.1f, mid + grouser));
                Box(new Vector3(-0.56f, -0.2f, z0 + 0.04f), new Vector3(-0.5f, 0.4f, z1 - 0.04f));
                Box(new Vector3(0.5f, -0.2f, z0 + 0.04f), new Vector3(0.56f, 0.4f, z1 - 0.04f));
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// <summary>A box of whole voxels, [x0, x1) × [y0, y1) × [z0, z1) in the parent's space.</summary>
        private static GameObject V(Transform parent, string name, Material material,
            float x0, float y0, float z0, float x1, float y1, float z1)
        {
            GameObject box = Primitive(PrimitiveType.Cube, name, parent, material);
            box.transform.localPosition = new Vector3(x0 + x1, y0 + y1, z0 + z1) * (0.5f * VU);
            box.transform.localScale = new Vector3(x1 - x0, y1 - y0, z1 - z0) * VU;
            return box;
        }

        /// <summary>The box on side <paramref name="side"/> (-1 left, +1 right); x is given for the right side.</summary>
        private static GameObject VS(Transform parent, string name, Material material, int side,
            float x0, float y0, float z0, float x1, float y1, float z1) =>
            side > 0
                ? V(parent, name, material, x0, y0, z0, x1, y1, z1)
                : V(parent, name, material, -x1, y0, z0, -x0, y1, z1);

        /// <summary>
        /// A disc on the parent's X axis from <paramref name="x0"/> to <paramref name="x1"/> voxels
        /// (given for the right side, mirrored on the left), <paramref name="radius"/> voxels across.
        /// </summary>
        private static GameObject Disc(Transform parent, string name, Material material, int side,
            float x0, float x1, float radius)
        {
            GameObject disc = Primitive(PrimitiveType.Cylinder, name, parent, material);
            disc.transform.SetLocalPositionAndRotation(new Vector3((x0 + x1) * 0.5f * side * VU, 0f, 0f),
                Quaternion.Euler(0f, 0f, 90f));
            // A cylinder primitive is 2 long on its axis and 1 across.
            disc.transform.localScale = new Vector3(2f * radius, (x1 - x0) * 0.5f, 2f * radius) * VU;
            return disc;
        }

        /// <summary>A visible primitive without a collider, placed by the caller.</summary>
        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.layer = parent.gameObject.layer;
            part.transform.SetParent(parent, false);
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }

        /// <summary>
        /// Rebuilds only the look of the Tank_01 prefab — hull, turret, gun, wheels, track and the
        /// voxel skin — with the points and colliders that follow it: the muzzle, the casing hatch,
        /// the machine gun's muzzle, the hull and turret boxes. Every other setting on the prefab
        /// (drive, drift, weapons, effects) stays as it is.
        /// </summary>
        [MenuItem("Tools/Tank/Rebuild Model")]
        public static void RebuildModel()
        {
            Paints paints = LoadPaints();
            GameObject root = PrefabUtility.LoadPrefabContents(TANK_PREFAB);

            try
            {
                Transform hull = root.transform.Find("HullVisual");
                Transform turret = hull != null ? hull.Find("TurretVisual") : null;
                Transform machineGun = turret != null ? turret.Find("MachineGun") : null;
                Transform cannon = root.transform.Find("TurretPivot/Cannon");
                TankInstaller installer = root.GetComponent<TankInstaller>();
                Game.Voxels.VoxelizedModel skin = root.GetComponent<Game.Voxels.VoxelizedModel>();
                BoxCollider[] colliders = root.GetComponents<BoxCollider>();

                if (turret == null || machineGun == null || cannon == null || installer == null || skin == null ||
                    colliders.Length < 2)
                {
                    Debug.LogError($"{LOG} Tank_01 is not laid out as TankBuilder builds it; run Tools/Tank/Build.");
                    return;
                }

                // Out with the old look: all under the hull but the turret and the exhaust, all under
                // the turret but the machine gun entity, and the spring mounts.
                for (int i = hull.childCount - 1; i >= 0; i--)
                {
                    Transform child = hull.GetChild(i);
                    if (child != turret && !child.name.StartsWith("Exhaust"))
                        Object.DestroyImmediate(child.gameObject);
                }

                for (int i = turret.childCount - 1; i >= 0; i--)
                    if (turret.GetChild(i) != machineGun)
                        Object.DestroyImmediate(turret.GetChild(i).gameObject);

                Transform oldSuspension = root.transform.Find("Suspension");
                if (oldSuspension != null)
                    Object.DestroyImmediate(oldSuspension.gameObject);

                turret.localPosition = new Vector3(0f, TURRET_RING, 0f);
                BuildHullModel(hull, paints);
                BuildTurretModel(turret, paints);
                BuildRunningGear(root.transform, hull, paints,
                    out Transform[] mounts, out Transform[] wheels, out Transform[][] ends, out Transform[][] links);

                cannon.Find("FirePoint").localPosition = CannonFirePoint;
                cannon.Find("CasingPoint").localPosition = CasingPoint * S;
                machineGun.Find("FirePoint").localPosition = MachineGunFirePoint;
                ShapeColliders(colliders[0], colliders[1]);

                SetField(installer, "_gunVisual", turret.Find("Gun"));
                SetArray(installer, "_wheelMounts", mounts);
                SetArray(installer, "_wheelVisuals", wheels);
                SetTrack(installer, "_leftTrack", ends[0][0], ends[0][1], links[0]);
                SetTrack(installer, "_rightTrack", ends[1][0], ends[1][1], links[1]);
                ConfigureSkin(skin, paints);

                PrefabUtility.SaveAsPrefabAsset(root, TANK_PREFAB);
                AssetDatabase.SaveAssets();
                Debug.Log($"{LOG} Rebuilt Tank_01's model: {mounts.Length / 2} stations a side.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// The cannon is the stock range weapon: no ammo, no shells, a slow reload. It hangs from the
        /// level pivot, with the fire point at the muzzle, clear of every hull collider.
        /// </summary>
        private static MonoEntity BuildCannon(Transform pivot, MonoEntity shell, MonoEntity casing)
        {
            Transform cannonObject = Child(pivot, "Cannon", Vector3.zero);
            cannonObject.gameObject.layer = LAYER_WEAPON;
            Transform firePoint = Child(cannonObject, "FirePoint", CannonFirePoint);
            // The spent casing is thrown out of a hatch on the turret's right, above the turret's
            // collider: a casing born inside it would be shot out by the solver.
            Transform casingPoint = Child(cannonObject, "CasingPoint", CasingPoint * S);

            MonoEntity entity = cannonObject.gameObject.AddComponent<MonoEntity>();
            RangeWeaponInstaller weapon = cannonObject.gameObject.AddComponent<RangeWeaponInstaller>();

            // The rocket launcher's settings first: the animator controller is required (AddValue
            // throws on null) even though nothing animates a cannon.
            RangeWeaponInstaller rpg = AssetDatabase.LoadAssetAtPath<GameObject>(RPG_PREFAB)
                .GetComponent<RangeWeaponInstaller>();
            EditorUtility.CopySerialized(rpg, weapon);

            SerializedObject serialized = new(weapon);
            serialized.FindProperty("_id").intValue = (int)WeaponID.TankCannon;
            serialized.FindProperty("_damage").intValue = CANNON_DAMAGE;
            serialized.FindProperty("_fireRate._duration").floatValue = CANNON_RELOAD;
            serialized.FindProperty("_fireRate._current").floatValue = 0f;
            serialized.FindProperty("_firePoint").objectReferenceValue = firePoint;
            serialized.FindProperty("_bulletPrefab").objectReferenceValue = shell;
            serialized.FindProperty("_spread").floatValue = 0f;
            serialized.FindProperty("_hasAmmo").boolValue = false;
            serialized.FindProperty("_shellPoint").objectReferenceValue = casingPoint;
            serialized.FindProperty("_shellPrefab").objectReferenceValue = casing;
            serialized.FindProperty("_shakeArgs.<ShakeDuration>k__BackingField").floatValue = CANNON_SHAKE_DURATION;
            serialized.FindProperty("_shakeArgs.<ShakeStrength>k__BackingField").floatValue = CANNON_SHAKE_STRENGTH;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SetInstallers(entity, weapon, AddShotSound(cannonObject.gameObject, CANNON_SHOT_CLIP, CANNON_SHOT_VOLUME));
            return entity;
        }

        /// <summary>
        /// The turret machine gun: the AR_02's range weapon with its bullet and damage, a faster
        /// rate, tighter spread and no ammo count.
        ///
        /// It is fixed to the visible turret, not to the level pivot the cannon hangs from: it turns
        /// with the turret and tilts with the hull. Hung from the level pivot, its muzzle stayed at
        /// the same height while the hull pitched under it, and with the nose thrown up by the
        /// cannon's kick the front of the hull rose past the muzzle — the gun shot into its own
        /// tank. Fixed to the turret it keeps its place above the turret's collider whatever the
        /// hull does.
        /// </summary>
        private static MonoEntity BuildMachineGun(Transform turretVisual)
        {
            Transform gunObject = Child(turretVisual, "MachineGun", Vector3.zero);
            gunObject.gameObject.layer = LAYER_WEAPON;
            Transform firePoint = Child(gunObject, "FirePoint", MachineGunFirePoint);

            MonoEntity entity = gunObject.gameObject.AddComponent<MonoEntity>();
            RangeWeaponInstaller weapon = gunObject.gameObject.AddComponent<RangeWeaponInstaller>();

            RangeWeaponInstaller template = AssetDatabase.LoadAssetAtPath<GameObject>(MACHINE_GUN_TEMPLATE)
                .GetComponentInChildren<RangeWeaponInstaller>(true);
            EditorUtility.CopySerialized(template, weapon);

            SerializedObject serialized = new(weapon);
            serialized.FindProperty("_id").intValue = (int)WeaponID.TankMachineGun;
            serialized.FindProperty("_damage").intValue = MACHINE_GUN_DAMAGE;
            serialized.FindProperty("_fireRate._duration").floatValue = MACHINE_GUN_RATE;
            serialized.FindProperty("_fireRate._current").floatValue = 0f;
            serialized.FindProperty("_firePoint").objectReferenceValue = firePoint;
            serialized.FindProperty("_spread").floatValue = MACHINE_GUN_SPREAD;
            serialized.FindProperty("_hasAmmo").boolValue = false;
            serialized.FindProperty("_shellPoint").objectReferenceValue = null;
            serialized.FindProperty("_shellPrefab").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SetInstallers(entity, weapon,
                AddShotSound(gunObject.gameObject, MACHINE_GUN_SHOT_CLIP, MACHINE_GUN_SHOT_VOLUME, voices: 3));
            return entity;
        }

        /// <summary>The pickups' outline, on the whole visible tank.</summary>
        internal static HighlightEffect AddHighlight(Transform hull)
        {
            HighlightEffect template = AssetDatabase.LoadAssetAtPath<GameObject>(AMMO_ITEM_PREFAB)
                .GetComponentInChildren<HighlightEffect>(true);

            HighlightEffect highlight = hull.gameObject.AddComponent<HighlightEffect>();

            if (template != null)
                EditorUtility.CopySerialized(template, highlight);

            highlight.effectGroup = TargetOptions.Children;
            highlight.effectTarget = hull;
            highlight.enabled = false;
            return highlight;
        }

        // ---------------------------------------------------------------- character and scene

        private static void WidenInteractionOnCharacterPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CHARACTER_PREFAB);

            try
            {
                if (WidenInteraction(root))
                    PrefabUtility.SaveAsPrefabAsset(root, CHARACTER_PREFAB);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>The interaction probe also looks at the Character layer, where the tank lives.</summary>
        private static bool WidenInteraction(GameObject character)
        {
            PlayerCharacterCoreInstaller core = character.GetComponent<PlayerCharacterCoreInstaller>();

            if (core == null)
            {
                Debug.LogError($"{LOG} No PlayerCharacterCoreInstaller on {character.name}.");
                return false;
            }

            SerializedObject serialized = new(core);
            SerializedProperty mask = serialized.FindProperty("_interactInstaller._layerMask");
            mask.intValue |= INTERACT_MASK;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        /// <summary>
        /// The Playground's [Character] was cloned from GameScene and unpacked, so the prefab change
        /// does not reach it: it is widened here too. The tank is parked south-west of the centre,
        /// clear of the aggro zone, between the player's spawn and the gallery.
        /// </summary>
        private static void HookUpPlayground(GameObject tankPrefab)
        {
            Scene scene = SceneManager.GetActiveScene();

            if (scene.path != PLAYGROUND_SCENE)
            {
                Debug.LogWarning($"{LOG} Playground is not the active scene; the scene was left alone.");
                return;
            }

            // Someone's unsaved work in the scene — a hand-placed obstacle, a half-done edit — is
            // theirs to save or throw away. The tank still goes in, but the save is left to them.
            bool hadUnsavedEdits = scene.isDirty;

            GameObject character = GameObject.Find("[Character]");

            if (character == null)
                Debug.LogError($"{LOG} No [Character] in the Playground.");
            else if (!PrefabUtility.IsPartOfPrefabInstance(character))
                WidenInteraction(character);

            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name.StartsWith("Tank_01"))
                    Object.DestroyImmediate(root);

            GameObject tank = (GameObject)PrefabUtility.InstantiatePrefab(tankPrefab, scene);
            tank.name = "Tank_01";
            tank.transform.SetPositionAndRotation(PlaygroundPosition, Quaternion.identity);

            EditorSceneManager.MarkSceneDirty(scene);

            if (hadUnsavedEdits)
                Debug.LogWarning($"{LOG} Playground had unsaved edits before the build, so it was not saved: " +
                                 "the new tank is in the open scene, save it when those edits are ready.");
            else
                EditorSceneManager.SaveScene(scene);
        }

        // ---------------------------------------------------------------- helpers

        internal static Transform Child(Transform parent, string name, Vector3 localPosition)
        {
            Transform child = new GameObject(name) { layer = parent.gameObject.layer }.transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            return child;
        }

        /// <summary>A visible primitive, no collider. Position and size are full-size and scaled by S here.</summary>
        private static GameObject Part(PrimitiveType type, string name, Transform parent, Material material,
            Vector3 localPosition, Vector3 localEuler, Vector3 localScale)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.layer = parent.gameObject.layer;
            part.transform.SetParent(parent, false);
            part.transform.SetLocalPositionAndRotation(localPosition * S, Quaternion.Euler(localEuler));
            part.transform.localScale = localScale * S;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }

        /// <summary>
        /// A shot sound for a vehicle gun, which has no visual in a hand to play one:
        /// <paramref name="voices"/> audio sources copied from AR_01's (its mixer group and range),
        /// restarted in turn by a WeaponFireSoundInstaller playing <paramref name="clipPath"/> once
        /// per shot. A fast gun gets several voices so a shot is not cut off at once; the priority
        /// is raised so the shots are not the first voices Unity silences when the scene is busy.
        /// A very fast gun sounds every <paramref name="shotsPerSound"/>-th shot only.
        /// Goes into SetInstallers after the gun's RangeWeaponInstaller.
        /// </summary>
        internal static WeaponFireSoundInstaller AddShotSound(GameObject gun, string clipPath, float volume,
            int voices = 1, int shotsPerSound = 1)
        {
            AudioSource template = AssetDatabase.LoadAssetAtPath<GameObject>(SHOT_AUDIO_TEMPLATE)
                .GetComponentInChildren<AudioSource>(true);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);

            Object[] sources = new Object[Mathf.Max(1, voices)];
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource audio = gun.AddComponent<AudioSource>();
                EditorUtility.CopySerialized(template, audio);
                audio.playOnAwake = false;
                audio.loop = false;
                audio.clip = clip;
                audio.volume = volume;
                audio.priority = SHOT_PRIORITY;
                sources[i] = audio;
            }

            WeaponFireSoundInstaller sound = gun.AddComponent<WeaponFireSoundInstaller>();
            SetArray(sound, "_sources", sources);
            SetArray(sound, "_clips", new Object[] { clip });
            SetInt(sound, "_shotsPerSound", shotsPerSound);
            return sound;
        }

        internal static void SetInstallers(MonoEntity entity, params MonoEntityInstaller[] installers)
        {
            SerializedObject serialized = new(entity);
            SerializedProperty list = serialized.FindProperty("sceneInstallers");
            list.arraySize = installers.Length;

            for (int i = 0; i < installers.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = installers[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SerializedProperty Property(SerializedObject serialized, string field)
        {
            SerializedProperty property = serialized.FindProperty(field);

            if (property == null)
                Debug.LogError($"{LOG} No serialized field '{field}' on {serialized.targetObject.GetType().Name}.");

            return property;
        }

        internal static void SetField(Object target, string field, Object value)
        {
            SerializedObject serialized = new(target);
            SerializedProperty property = Property(serialized, field);
            if (property == null) return;

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetArray(Object target, string field, Object[] values)
        {
            SerializedObject serialized = new(target);
            SerializedProperty property = Property(serialized, field);
            if (property == null) return;

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetInt(Object target, string field, int value)
        {
            SerializedObject serialized = new(target);
            SerializedProperty property = Property(serialized, field);
            if (property == null) return;

            property.intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetFloat(Object target, string field, float value)
        {
            SerializedObject serialized = new(target);
            SerializedProperty property = Property(serialized, field);
            if (property == null) return;

            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetVector(Object target, string field, Vector3 value)
        {
            SerializedObject serialized = new(target);
            SerializedProperty property = Property(serialized, field);
            if (property == null) return;

            property.vector3Value = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetTrack(Object target, string field, Transform front, Transform rear, Transform[] links)
        {
            SerializedObject serialized = new(target);
            SerializedProperty rig = Property(serialized, field);
            if (rig == null) return;

            rig.FindPropertyRelative("Front").objectReferenceValue = front;
            rig.FindPropertyRelative("Rear").objectReferenceValue = rear;

            SerializedProperty list = rig.FindPropertyRelative("Links");
            list.arraySize = links.Length;
            for (int i = 0; i < links.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = links[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetShake(Object target, string field, float duration, float strength)
        {
            SerializedObject serialized = new(target);
            SerializedProperty shake = Property(serialized, field);
            if (shake == null) return;

            shake.FindPropertyRelative("<ShakeDuration>k__BackingField").floatValue = duration;
            shake.FindPropertyRelative("<ShakeStrength>k__BackingField").floatValue = strength;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
