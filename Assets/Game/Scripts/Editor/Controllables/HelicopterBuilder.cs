using Atomic.Entities;
using HighlightPlus;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Game.EditorTools.TankBuilder;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the flyable helicopter out of primitives, its faster bullet, and parks one in the
    /// Playground.
    ///
    /// The shape is an attack helicopter in the Apache's mould at about a third of its size (S): narrow fuselage,
    /// stepped tandem canopies, engine pods, stub wings with rocket pods, an eight-barrel rotary gun under the nose,
    /// four-blade rotor (⌀3.1 m), 30 % shorter than the Apache's proportions (L), tail-wheel gear and a tail rotor high on the fin.
    ///
    /// Heli_01 (layer Character, every collider on the root — Collider.TryGetEntity looks no further;
    /// the root's origin is the bottom of the wheels):
    ///   GunPivot_Chin    under the nose, level; turned to the cursor point by HelicopterGunAimBehaviour
    ///     Gun            RangeWeaponInstaller (AR_01's, tighter, with the faster bullet), FirePoint, no casings;
    ///                    fired by MinigunBehaviour, one round per barrel coming round
    ///   CameraPoint
    ///   ExitPoints       Left, Right, Front
    ///   BodyVisual       everything visible, leaned by HelicopterTiltBehaviour; no colliders
    ///     GunVisual_Chin copies GunPivot_Chin's turn; its Barrels are spun by MinigunBehaviour
    ///     MainRotor, TailRotor   spun by RotorSpinBehaviour
    ///
    /// The rotors carry no colliders: the blades would catch on every wall top.
    /// Bullet_Heli is Bullet_01_AR at 60 m/s instead of 40, as long, with no casings; it ricochets as the rifle bullet does.
    /// Re-running rebuilds both prefabs and puts the Playground helicopter back where it started.
    /// </summary>
    public static class HelicopterBuilder
    {
        private const string LOG = "[HELI]";

        private const string VEHICLES_FOLDER = "Assets/Game/Prefabs/Vehicles";
        private const string HELI_PREFAB = VEHICLES_FOLDER + "/Heli_01.prefab";
        private const string BULLET_TEMPLATE = "Assets/Game/Prefabs/Projectiles/Bullet_01_AR.prefab";
        private const string BULLET_PREFAB = "Assets/Game/Prefabs/Projectiles/Bullet_Heli.prefab";
        private const string GUN_TEMPLATE = "Assets/Game/Prefabs/Weapons/AR_01.prefab";
        private const string BLAST_VFX_PREFAB = "Assets/Game/Prefabs/VFX/FX_RocketExplosion.prefab";
        private const string GUN_SHOT_CLIP = "Assets/Game/Audio/Weapons/Fire (MachineGun).wav";
        private const float GUN_SHOT_VOLUME = 0.6f;
        // 32 rounds a second need not be 32 sounds a second: every second round sounds.
        private const int GUN_SHOTS_PER_SOUND = 2;
        private const string PLAYGROUND_SCENE = "Assets/Game/Scenes/Playground.unity";

        private const string BODY_MATERIAL = "Assets/Game/Materials/MT_Heli_Body.mat";
        private const string GLASS_MATERIAL = "Assets/Game/Materials/MT_Heli_Glass.mat";
        private const string DARK_MATERIAL = "Assets/Game/Materials/MT_Tank_Track.mat";

        private static readonly Color BodyColour = new(0.2f, 0.27f, 0.18f);
        private static readonly Color GlassColour = new(0.08f, 0.12f, 0.22f);
        private static readonly Color DarkColour = new(0.13f, 0.13f, 0.12f);

        private const int LAYER_CHARACTER = 9;
        private const int LAYER_WEAPON = 13;

        private const float MASS = 1500f;

        /// <summary>
        /// Overall scale. Every length below is written for the half-size Apache and multiplied by
        /// this one number: 0.675 (a quarter, then 35 % up) makes it about a third of the real one.
        /// Changed in one place.
        /// </summary>
        private const float S = 0.675f;

        /// <summary>
        /// Length only (along Z): the airframe is 30 % shorter than the Apache's proportions, as wide
        /// and as tall. Positions, colliders and the lengthwise size of every part are multiplied by
        /// it; round parts (wheels, sensor ball, hub) and the guns keep their shape.
        /// </summary>
        private const float L = 0.7f;

        /// <summary>
        /// Main rotor diameter before S. Shortened more than the airframe (7.3 × L would be 5.1): the
        /// tail rotor comes 30 % nearer too, and the main blades must not sweep through its disc.
        /// </summary>
        private const float MAIN_ROTOR = 4.6f;

        // Minigun: one rotary gun under the nose, AR_01's weapon with the faster bullet. The rate is
        // the barrels' (HelicopterInstaller's minigun settings), not the weapon's cooldown.
        private const int GUN_DAMAGE = 8;
        private const float GUN_RATE = 0.01f;
        private const float GUN_SPREAD = 3.5f;
        private const int BARREL_COUNT = 8;
        private const float MINIGUN_MAX_SPIN = 1440f;
        private const float GUN_MAX_YAW = 75f;
        private const float BULLET_SPEED = 60f;
        private const float BULLET_LENGTH_FACTOR = 1f;

        // The minigun's mount, before S: under the nose, below the fuselage collider (from 0.48 up)
        // and behind the sensor turret's chin. The gun itself keeps its size (not shortened by L).
        private static readonly Vector3 GunMount = new(0f, 0.36f, 2.55f);
        // Barrels: a ring of BARREL_COUNT, BARREL_RING out from the spin axis, from the housing's
        // face to the muzzle.
        private const float BARREL_RING = 0.07f;
        private const float BARREL_START = 0.12f;
        private const float MUZZLE_Z = 0.95f;

        // Clear of the stub wings (±1.3 m) and of the nose.
        private static readonly Vector3 ExitLeft = new Vector3(-2.1f, 0f, 0.4f) * S;
        private static readonly Vector3 ExitRight = new Vector3(2.1f, 0f, 0.4f) * S;
        private static readonly Vector3 ExitFront = new Vector3(0f, 0f, 3.5f) * S;

        private static readonly Vector3 MarkerOffset = new Vector3(0f, 2.3f, 0f) * S;
        private const float CAMERA_DISTANCE = 1.15f;

        // In the arena's southern extension (PlaygroundBuilder.SOUTH_EXTENSION), on open floor below
        // the player's spawn: the rotor's tips reach 1.6 m out, the tail 2 m back.
        private static readonly Vector3 PlaygroundPosition = new(0f, 0f, -17f);

        [MenuItem("Tools/Helicopter/Build")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(VEHICLES_FOLDER))
                AssetDatabase.CreateFolder("Assets/Game/Prefabs", "Vehicles");

            Material body = SolidMaterial(BODY_MATERIAL, BodyColour, 0.35f);
            Material glass = SolidMaterial(GLASS_MATERIAL, GlassColour, 0.9f);
            Material dark = SolidMaterial(DARK_MATERIAL, DarkColour, 0.1f);
            PhysicsMaterial slick = SlickMaterial();

            MonoEntity bullet = BuildBullet();
            GameObject heli = BuildHelicopter(body, glass, dark, slick, bullet);

            HookUpPlayground(heli);

            AssetDatabase.SaveAssets();
            Debug.Log($"{LOG} Built Heli_01 and Bullet_Heli.");
        }

        // ---------------------------------------------------------------- bullet

        private static MonoEntity BuildBullet()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BULLET_PREFAB) == null)
                AssetDatabase.CopyAsset(BULLET_TEMPLATE, BULLET_PREFAB);

            GameObject contents = PrefabUtility.LoadPrefabContents(BULLET_PREFAB);

            try
            {
                BulletInstaller installer = contents.GetComponentInChildren<BulletInstaller>(true);
                SetFloat(installer, "_moveSpeed", BULLET_SPEED);

                // Ricochet as the rifle's bullet has it: count, angle and impact share are taken
                // from the template each build. A bullet that bounces back at the helicopter still
                // flies through it (SpawnUseCase.PassThroughShooter).
                BulletInstaller templateInstaller = AssetDatabase.LoadAssetAtPath<GameObject>(BULLET_TEMPLATE)
                    .GetComponentInChildren<BulletInstaller>(true);
                SerializedObject templateSettings = new(templateInstaller);
                SerializedObject bulletSettings = new(installer);
                foreach (string field in new[] { "_ricochetMaxCount", "_ricochetMaxAngle", "_ricochetImpactFactor" })
                    bulletSettings.CopyFromSerializedProperty(templateSettings.FindProperty(field));
                bulletSettings.ApplyModifiedPropertiesWithoutUndo();

                // A tracer BULLET_LENGTH_FACTOR times as long as the rifle's (now the same). The bullet is a stretched cube, so the
                // length is the root's Z scale. A sphere collider scales with the largest axis:
                // its radius is shortened by the same factor, so the hit stays as wide as before.
                // Taken from the template each build, so re-running does not keep doubling it.
                GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(BULLET_TEMPLATE);
                Vector3 scale = template.transform.localScale;
                contents.transform.localScale = new Vector3(scale.x, scale.y, scale.z * BULLET_LENGTH_FACTOR);

                SphereCollider templateSphere = template.GetComponent<SphereCollider>();
                if (templateSphere != null && contents.TryGetComponent(out SphereCollider sphere))
                    sphere.radius = templateSphere.radius / BULLET_LENGTH_FACTOR;
                PrefabUtility.SaveAsPrefabAsset(contents, BULLET_PREFAB);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(BULLET_PREFAB).GetComponent<MonoEntity>();
        }

        // ---------------------------------------------------------------- helicopter

        private static GameObject BuildHelicopter(Material body, Material glass, Material dark, PhysicsMaterial slick,
            MonoEntity bullet)
        {
            GameObject root = new GameObject("Heli_01") { layer = LAYER_CHARACTER };

            try
            {
                Rigidbody rigidbody = root.AddComponent<Rigidbody>();
                rigidbody.mass = MASS;
                rigidbody.linearDamping = 0f;
                rigidbody.angularDamping = 1f;
                rigidbody.useGravity = true;
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                // Level: only the visual leans. The heading turns through MoveRotation.
                rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                rigidbody.maxDepenetrationVelocity = 1f;

                AddColliders(root, slick);

                Transform visual = Child(root.transform, "BodyVisual", Vector3.zero);
                BuildModel(visual, body, glass, dark, out Transform mainRotor, out Transform tailRotor);
                // One rotary minigun under the nose.
                Vector3 mount = Len(GunMount) * S;
                Transform gunVisual = BuildGunVisual(visual, dark, "GunVisual_Chin", mount, out Transform barrels);
                Transform pivot = Child(root.transform, "GunPivot_Chin", mount);
                MonoEntity[] guns = { BuildGun(pivot, bullet) };
                Transform[] pivots = { pivot };
                Transform[] gunVisuals = { gunVisual };

                Transform cameraPoint = Child(root.transform, "CameraPoint", Vector3.zero);

                Transform exits = Child(root.transform, "ExitPoints", Vector3.zero);
                Transform[] exitPoints =
                {
                    Child(exits, "Left", ExitLeft),
                    Child(exits, "Right", ExitRight),
                    Child(exits, "Front", ExitFront)
                };

                HighlightEffect highlight = AddHighlight(visual);

                MonoEntity entity = root.AddComponent<MonoEntity>();
                HelicopterInstaller installer = root.AddComponent<HelicopterInstaller>();
                SetField(installer, "_mainRotor", mainRotor);
                SetField(installer, "_tailRotor", tailRotor);
                SetArray(installer, "_guns", guns);
                SetArray(installer, "_gunPivots", pivots);
                SetArray(installer, "_gunVisuals", gunVisuals);
                SetFloat(installer, "_gunMaxYaw", GUN_MAX_YAW);
                SetField(installer, "_minigunBarrels", barrels);
                SetInt(installer, "_barrelCount", BARREL_COUNT);
                SetFloat(installer, "_minigunMaxSpin", MINIGUN_MAX_SPIN);
                SetField(installer, "_rigidbody", rigidbody);
                SetField(installer, "_bodyVisual", visual);
                SetField(installer, "_cameraPoint", cameraPoint);
                SetFloat(installer, "_cameraDistance", CAMERA_DISTANCE);
                SetArray(installer, "_exitPoints", exitPoints);
                SetField(installer, "_highlight", highlight);
                SetVector(installer, "_markerOffset", MarkerOffset);
                SetField(installer, "_explosionVfx",
                    AssetDatabase.LoadAssetAtPath<GameObject>(BLAST_VFX_PREFAB).GetComponent<MonoEntity>());
                SetShake(installer, "_destroyShake", 0.45f, 0.4f);

                SetInstallers(entity, installer);

                return PrefabUtility.SaveAsPrefabAsset(root, HELI_PREFAB);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void AddColliders(GameObject root, PhysicsMaterial slick)
        {
            // The guns hang below the wing box and outside the fuselage box: no fire point inside either.
            Box(root, new Vector3(0f, 0.88f, 0.6f), new Vector3(0.8f, 0.8f, 4.0f), slick);    // fuselage
            Box(root, new Vector3(0f, 1.45f, 0.6f), new Vector3(0.6f, 0.5f, 2.4f), slick);    // canopies, engines' spine
            Box(root, new Vector3(0f, 1.0f, 0.25f), new Vector3(2.6f, 0.12f, 0.45f), slick);  // stub wings
            Box(root, new Vector3(0f, 1.0f, -2.85f), new Vector3(0.32f, 0.34f, 2.9f), slick); // tail boom
            Box(root, new Vector3(0f, 1.55f, -4.3f), new Vector3(0.1f, 1.0f, 0.55f), slick);  // fin

            // Tail-wheel gear: the bottoms of the wheels are the root's origin.
            Sphere(root, new Vector3(-0.55f, 0.2f, 1.5f), 0.2f, slick);
            Sphere(root, new Vector3(0.55f, 0.2f, 1.5f), 0.2f, slick);
            Sphere(root, new Vector3(0f, 0.12f, -4.0f), 0.12f, slick);
        }

        private static void Sphere(GameObject root, Vector3 centre, float radius, PhysicsMaterial material)
        {
            SphereCollider sphere = root.AddComponent<SphereCollider>();
            sphere.center = Len(centre) * S;
            sphere.radius = radius * S;
            sphere.sharedMaterial = material;
        }

        private static void Box(GameObject root, Vector3 centre, Vector3 size, PhysicsMaterial material)
        {
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.center = Len(centre) * S;
            box.size = Len(size) * S;
            box.sharedMaterial = material;
        }

        private static void BuildModel(Transform visual, Material body, Material glass, Material dark,
            out Transform mainRotor, out Transform tailRotor)
        {
            // Fuselage: narrow and long, the nose stepping down to the sensor turret.
            Part(PrimitiveType.Cube, "Fuselage", visual, body, new Vector3(0f, 0.88f, 0.45f), Vector3.zero,
                new Vector3(0.7f, 0.8f, 3.7f));
            Part(PrimitiveType.Cube, "Nose", visual, body, new Vector3(0f, 0.78f, 2.5f), Vector3.zero,
                new Vector3(0.5f, 0.5f, 0.5f));
            Part(PrimitiveType.Sphere, "SensorTurret", visual, dark, new Vector3(0f, 0.68f, 2.78f), Vector3.zero,
                new Vector3(0.32f, 0.32f, 0.32f), keepShape: true);

            // Tandem cockpit: the gunner in front and lower, the pilot behind and higher.
            Part(PrimitiveType.Cube, "GunnerCanopy", visual, glass, new Vector3(0f, 1.38f, 1.75f),
                new Vector3(-8f, 0f, 0f), new Vector3(0.52f, 0.32f, 0.75f));
            Part(PrimitiveType.Cube, "PilotCanopy", visual, glass, new Vector3(0f, 1.5f, 0.95f),
                new Vector3(-6f, 0f, 0f), new Vector3(0.54f, 0.42f, 0.8f));
            Part(PrimitiveType.Cube, "Spine", visual, body, new Vector3(0f, 1.42f, -0.2f), Vector3.zero,
                new Vector3(0.6f, 0.36f, 1.6f));

            // Engine pods either side of the spine, exhausts at the back.
            foreach (float x in new[] { -0.45f, 0.45f })
            {
                Part(PrimitiveType.Cylinder, "Engine", visual, body, new Vector3(x, 1.38f, -0.35f),
                    new Vector3(90f, 0f, 0f), new Vector3(0.32f, 0.6f, 0.32f));
                Part(PrimitiveType.Cylinder, "Exhaust", visual, dark, new Vector3(x, 1.38f, -0.98f),
                    new Vector3(90f, 0f, 0f), new Vector3(0.22f, 0.06f, 0.22f));
            }

            // Stub wings: a rocket pod under each. The gun hangs under the nose (BuildGunVisual).
            Part(PrimitiveType.Cube, "StubWings", visual, body, new Vector3(0f, 1.0f, 0.25f), Vector3.zero,
                new Vector3(2.6f, 0.06f, 0.45f));

            foreach (float side in new[] { -1f, 1f })
            {
                Part(PrimitiveType.Cube, "Pylon", visual, dark, new Vector3(0.85f * side, 0.92f, 0.3f), Vector3.zero,
                    new Vector3(0.05f, 0.12f, 0.2f));
                Part(PrimitiveType.Cylinder, "RocketPod", visual, body, new Vector3(0.85f * side, 0.8f, 0.3f),
                    new Vector3(90f, 0f, 0f), new Vector3(0.2f, 0.38f, 0.2f));
            }

            // Tail: boom, swept fin, stabilator low at its foot.
            Part(PrimitiveType.Cube, "TailBoom", visual, body, new Vector3(0f, 1.0f, -2.85f), Vector3.zero,
                new Vector3(0.3f, 0.32f, 2.9f));
            Part(PrimitiveType.Cube, "Fin", visual, body, new Vector3(0f, 1.55f, -4.3f), new Vector3(-12f, 0f, 0f),
                new Vector3(0.07f, 1.0f, 0.55f));
            Part(PrimitiveType.Cube, "Stabilator", visual, body, new Vector3(0f, 1.1f, -4.3f), Vector3.zero,
                new Vector3(1.1f, 0.04f, 0.32f));

            // Tail-wheel gear: two main wheels under the cockpit, one under the tail.
            foreach (float x in new[] { -0.55f, 0.55f })
            {
                Part(PrimitiveType.Cube, "GearLeg", visual, dark, new Vector3(x * 0.85f, 0.4f, 1.5f), Vector3.zero,
                    new Vector3(0.06f, 0.4f, 0.06f));
                Part(PrimitiveType.Cylinder, "Wheel", visual, dark, new Vector3(x, 0.2f, 1.5f),
                    new Vector3(0f, 0f, 90f), new Vector3(0.4f, 0.06f, 0.4f), keepShape: true);
            }

            Part(PrimitiveType.Cube, "TailGearLeg", visual, dark, new Vector3(0f, 0.5f, -4.0f), Vector3.zero,
                new Vector3(0.05f, 0.6f, 0.05f));
            Part(PrimitiveType.Cylinder, "TailWheel", visual, dark, new Vector3(0f, 0.12f, -4.0f),
                new Vector3(0f, 0f, 90f), new Vector3(0.24f, 0.04f, 0.24f), keepShape: true);

            // Main rotor: four blades. The disc stays round: MAIN_ROTOR both ways.
            Part(PrimitiveType.Cylinder, "Mast", visual, dark, new Vector3(0f, 1.75f, 0f), Vector3.zero,
                new Vector3(0.14f, 0.15f, 0.14f), keepShape: true);
            mainRotor = Child(visual, "MainRotor", new Vector3(0f, 1.92f, 0f) * S);
            Part(PrimitiveType.Cube, "Blade", mainRotor, dark, Vector3.zero, Vector3.zero,
                new Vector3(MAIN_ROTOR, 0.03f, 0.2f), keepShape: true);
            Part(PrimitiveType.Cube, "Blade", mainRotor, dark, Vector3.zero, Vector3.zero,
                new Vector3(0.2f, 0.03f, MAIN_ROTOR), keepShape: true);
            Part(PrimitiveType.Cylinder, "Hub", mainRotor, dark, Vector3.zero, Vector3.zero,
                new Vector3(0.3f, 0.06f, 0.3f), keepShape: true);

            // Tail rotor on the left of the fin's top, four blades, spinning about its X.
            tailRotor = Child(visual, "TailRotor", Len(new Vector3(-0.1f, 1.95f, -4.42f)) * S);
            Part(PrimitiveType.Cube, "Blade", tailRotor, dark, Vector3.zero, Vector3.zero,
                new Vector3(0.02f, 1.4f, 0.1f), keepShape: true);
            Part(PrimitiveType.Cube, "Blade", tailRotor, dark, Vector3.zero, Vector3.zero,
                new Vector3(0.02f, 0.1f, 1.4f), keepShape: true);
        }

        /// <summary>
        /// The minigun people see, inside the leaning body, at its pivot's place; it copies the
        /// pivot's turn. A housing, and in front of it <paramref name="barrels"/> — a ring of
        /// BARREL_COUNT barrels held by two clamps, spun about its forward by MinigunBehaviour.
        /// </summary>
        private static Transform BuildGunVisual(Transform visual, Material dark, string name, Vector3 position,
            out Transform barrels)
        {
            Transform gun = Child(visual, name, position);
            // The gun keeps its length: the fire point is at the full MUZZLE_Z.
            Part(PrimitiveType.Cube, "Housing", gun, dark, Vector3.zero, Vector3.zero, new Vector3(0.22f, 0.2f, 0.26f),
                keepShape: true);

            barrels = Child(gun, "Barrels", Vector3.zero);
            float length = MUZZLE_Z - BARREL_START;
            float middle = BARREL_START + length * 0.5f;

            for (int i = 0; i < BARREL_COUNT; i++)
            {
                float angle = i * Mathf.PI * 2f / BARREL_COUNT;
                Vector3 offset = new(Mathf.Cos(angle) * BARREL_RING, Mathf.Sin(angle) * BARREL_RING, middle);
                Part(PrimitiveType.Cylinder, "Barrel", barrels, dark, offset, new Vector3(90f, 0f, 0f),
                    new Vector3(0.035f, length * 0.5f, 0.035f), keepShape: true);
            }

            // Clamps round the ring: one at the back, one near the muzzle. A spindle down the middle.
            float clamp = (BARREL_RING + 0.03f) * 2f;
            Part(PrimitiveType.Cylinder, "Clamp", barrels, dark, new Vector3(0f, 0f, BARREL_START + 0.04f),
                new Vector3(90f, 0f, 0f), new Vector3(clamp, 0.03f, clamp), keepShape: true);
            Part(PrimitiveType.Cylinder, "Clamp", barrels, dark, new Vector3(0f, 0f, MUZZLE_Z - 0.1f),
                new Vector3(90f, 0f, 0f), new Vector3(clamp, 0.025f, clamp), keepShape: true);
            Part(PrimitiveType.Cylinder, "Spindle", barrels, dark, new Vector3(0f, 0f, middle),
                new Vector3(90f, 0f, 0f), new Vector3(0.05f, length * 0.5f, 0.05f), keepShape: true);

            return gun;
        }

        /// <summary>
        /// AR_01's range weapon — its shake and muzzle setup, without its casings — much tighter, with no ammo
        /// count and the faster bullet. Its cooldown is not used: MinigunBehaviour sets the rate.
        /// </summary>
        private static MonoEntity BuildGun(Transform pivot, MonoEntity bullet)
        {
            Transform gunObject = Child(pivot, "Gun", Vector3.zero);
            gunObject.gameObject.layer = LAYER_WEAPON;
            Transform firePoint = Child(gunObject, "FirePoint", new Vector3(0f, 0f, MUZZLE_Z) * S);

            MonoEntity entity = gunObject.gameObject.AddComponent<MonoEntity>();
            RangeWeaponInstaller weapon = gunObject.gameObject.AddComponent<RangeWeaponInstaller>();

            RangeWeaponInstaller template = AssetDatabase.LoadAssetAtPath<GameObject>(GUN_TEMPLATE)
                .GetComponentInChildren<RangeWeaponInstaller>(true);
            EditorUtility.CopySerialized(template, weapon);

            SerializedObject serialized = new(weapon);
            serialized.FindProperty("_id").intValue = (int)WeaponID.HeliGun;
            serialized.FindProperty("_damage").intValue = GUN_DAMAGE;
            serialized.FindProperty("_fireRate._duration").floatValue = GUN_RATE;
            serialized.FindProperty("_fireRate._current").floatValue = 0f;
            serialized.FindProperty("_firePoint").objectReferenceValue = firePoint;
            serialized.FindProperty("_bulletPrefab").objectReferenceValue = bullet;
            serialized.FindProperty("_spread").floatValue = GUN_SPREAD;
            serialized.FindProperty("_hasAmmo").boolValue = false;
            // No casings: at 32 rounds a second they were a heap of bodies. Without a shell point and
            // prefab RangeWeaponInstaller adds no ShellPoint and the fire action throws none.
            serialized.FindProperty("_shellPoint").objectReferenceValue = null;
            serialized.FindProperty("_shellPrefab").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // The shot sound: the machine gun clip, every GUN_SHOTS_PER_SOUND-th round.
            SetInstallers(entity, weapon, AddShotSound(gunObject.gameObject, GUN_SHOT_CLIP, GUN_SHOT_VOLUME, voices: 4,
                shotsPerSound: GUN_SHOTS_PER_SOUND));
            return entity;
        }

        // ---------------------------------------------------------------- scene

        private static void HookUpPlayground(GameObject heliPrefab)
        {
            Scene scene = SceneManager.GetActiveScene();

            if (scene.path != PLAYGROUND_SCENE)
            {
                Debug.LogWarning($"{LOG} Playground is not the active scene; the scene was left alone.");
                return;
            }

            // Someone's unsaved work in the scene is theirs to save: the helicopter still goes in,
            // but the save is left to them.
            bool hadUnsavedEdits = scene.isDirty;

            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name.StartsWith("Heli_01"))
                    Object.DestroyImmediate(root);

            GameObject heli = (GameObject)PrefabUtility.InstantiatePrefab(heliPrefab, scene);
            heli.name = "Heli_01";
            heli.transform.SetPositionAndRotation(PlaygroundPosition, Quaternion.identity);

            EditorSceneManager.MarkSceneDirty(scene);

            if (hadUnsavedEdits)
                Debug.LogWarning($"{LOG} Playground had unsaved edits before the build, so it was not saved: " +
                                 "the helicopter is in the open scene, save it when those edits are ready.");
            else
                EditorSceneManager.SaveScene(scene);
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>The airframe's lengthwise shortening, before S.</summary>
        private static Vector3 Len(Vector3 v) => new(v.x, v.y, v.z * L);

        /// <summary>
        /// A visible primitive, no collider. Position and size are written for the half-size model
        /// and scaled by S here.
        ///
        /// The airframe is also shortened by L: a part laid out on the body has its position moved
        /// in along Z, and, unless <paramref name="keepShape"/>, the side of it that lies along the
        /// helicopter is shortened — whichever local axis that is after its rotation, so a cylinder
        /// turned to lie lengthwise gets shorter rather than thinner. Parts inside a rotor or a gun
        /// sit at their own origin and keep their place.
        /// </summary>
        private static GameObject Part(PrimitiveType type, string name, Transform parent, Material material,
            Vector3 localPosition, Vector3 localEuler, Vector3 localScale, bool keepShape = false)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.layer = parent.gameObject.layer;
            part.transform.SetParent(parent, false);

            Quaternion rotation = Quaternion.Euler(localEuler);
            if (parent.name == "BodyVisual")
                localPosition = Len(localPosition);

            if (!keepShape)
            {
                // The local axis that lies closest to the helicopter's length.
                Vector3 along = Quaternion.Inverse(rotation) * Vector3.forward;
                Vector3 abs = new(Mathf.Abs(along.x), Mathf.Abs(along.y), Mathf.Abs(along.z));
                int axis = abs.x > abs.y ? (abs.x > abs.z ? 0 : 2) : (abs.y > abs.z ? 1 : 2);
                localScale[axis] *= L;
            }

            part.transform.SetLocalPositionAndRotation(localPosition * S, rotation);
            part.transform.localScale = localScale * S;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }
    }
}
