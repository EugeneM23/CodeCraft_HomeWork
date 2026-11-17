using Atomic.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the grenades and hooks them up on the player character (the [Character] prefab and the
    /// open Playground scene, whose character is not a prefab instance).
    ///
    /// Both grenades share one body: a small egg with a fuse lever on a bouncy, continuously collided
    /// rigidbody with a GrenadeInstaller. Grenade_01 (olive) carries the rocket's ExplosionInstaller
    /// settings with a wider radius; Grenade_Flash_01 (light grey, blue band) carries a FlashInstaller
    /// that blinds enemies (BlindBuffConfig) and dazzles the player, with the FX_Flashbang effect.
    /// GrenadeItem and FlashGrenadeItem are AmmoItem with the ammo box swapped for a grenade and
    /// AmmoPickUpInstaller for GrenadePickUpInstaller of the matching type.
    ///
    /// On the character: the grenade slots, the throw point, and a Screen Space Overlay canvas
    /// &lt;FlashScreen&gt; with the white-out image (FlashScreenInstaller) and the grenade HUD line.
    ///
    /// Re-running rebuilds the prefabs and the effect, and re-points the character's installers,
    /// keeping its throw point, canvas and the grenade counts set in the inspector.
    /// </summary>
    public static class GrenadeBuilder
    {
        private const string LOG = "[GRENADES]";
        private const string ROCKET_BOMB_TYPE = "RayfireBomb";

        private const string ROCKET_PREFAB = "Assets/Game/Prefabs/Projectiles/Rocket_01.prefab";
        private const string AMMO_ITEM_PREFAB = "Assets/Game/Prefabs/Items/AmmoItem.prefab";
        private const string CHARACTER_PREFAB = "Assets/Game/Prefabs/Characters/[Character].prefab";

        private const string GRENADE_PREFAB = "Assets/Game/Prefabs/Projectiles/Grenade_01.prefab";
        private const string FLASH_PREFAB = "Assets/Game/Prefabs/Projectiles/Grenade_Flash_01.prefab";
        private const string GRENADE_ITEM_PREFAB = "Assets/Game/Prefabs/Items/GrenadeItem.prefab";
        private const string FLASH_ITEM_PREFAB = "Assets/Game/Prefabs/Items/FlashGrenadeItem.prefab";
        private const string FLASH_VFX_PREFAB = "Assets/Game/Prefabs/VFX/FX_Flashbang.prefab";
        private const string BLIND_BUFF = "Assets/Game/Configs/Buffs/BlindBuffConfig.asset";

        private const string BODY_MATERIAL = "Assets/Game/Materials/MT_Grenade_01.mat";
        private const string FLASH_BODY_MATERIAL = "Assets/Game/Materials/MT_GrenadeFlash_01.mat";
        private const string FLASH_BAND_MATERIAL = "Assets/Game/Materials/MT_GrenadeFlashBand_01.mat";
        private const string LEVER_MATERIAL = "Assets/Game/Materials/MT_GrenadeLever_01.mat";
        private const string PHYSICS_MATERIAL = "Assets/Game/Materials/PM_Grenade.physicMaterial";
        private const string GLOW_MATERIAL = "Assets/Game/Materials/glow1_ADD.mat";
        private const string PLAYGROUND_SCENE = "Assets/Game/Scenes/Playground.unity";

        private const string THROW_POINT = "<GrenadeThrowPoint>";
        // Right shoulder, a little ahead of the chest: the grenade leaves outside the capsule.
        private static readonly Vector3 ThrowPointPosition = new(0.3f, 1.5f, 0.5f);

        private static readonly Color BodyColour = new(0.29f, 0.33f, 0.17f);
        private static readonly Color FlashBodyColour = new(0.78f, 0.78f, 0.75f);
        private static readonly Color FlashBandColour = new(0.15f, 0.35f, 0.8f);
        private static readonly Color LeverColour = new(0.55f, 0.55f, 0.55f);

        private const float EXPLOSION_RADIUS = 5f;
        // The rocket bursts on the wall itself and breaches 0.6 m around the hit. A grenade lies on
        // the floor, often a metre or two from a wall: it needs a wider breach to reach the bricks
        // at the foot of the wall.
        private const float BREACH_RADIUS = 1.8f;
        // A sixth of the rocket's push: a grenade cracks a wall open, it does not hurl the bricks across the room.
        private const float BREACH_SPEED = 8f / 6f;
        private const float BOMB_STRENGTH = 2f / 6f;
        private const float BODY_RADIUS = 0.07f;
        private const float ANGULAR_DAMPING = 2f;

        // The flash grenade: no damage, a shorter fuse, a wide reach.
        private const float FLASH_FUSE = 1.5f;
        private const float FLASH_RADIUS = 8f;
        private const float BLIND_SECONDS = 4f;
        private const float BLIND_STUMBLE_SPEED = 0.35f;
        private const float FLASH_SHAKE_DURATION = 0.15f;
        private const float FLASH_SHAKE_STRENGTH = 0.1f;

        private const int FRAG_COUNT = 3;
        private const int FLASH_COUNT = 2;

        // Pickups just south of the player's spawn, on either side of the walk to the weapons:
        // frag grenades inside, flash grenades outside.
        private static readonly Vector3[] FragPickupPositions = { new(2f, 0f, -10.5f), new(-2f, 0f, -10.5f) };
        private static readonly Vector3[] FlashPickupPositions = { new(3.5f, 0f, -10.5f), new(-3.5f, 0f, -10.5f) };

        private const string FLASH_SCREEN = "<FlashScreen>";

        [MenuItem("Tools/Grenades/Build")]
        public static void Build()
        {
            Material lever = SolidMaterial(LEVER_MATERIAL, LeverColour);
            Material fragBody = SolidMaterial(BODY_MATERIAL, BodyColour);
            Material flashBody = SolidMaterial(FLASH_BODY_MATERIAL, FlashBodyColour);
            Material flashBand = SolidMaterial(FLASH_BAND_MATERIAL, FlashBandColour);
            PhysicsMaterial bounce = BouncyMaterial();

            BlindBuffConfig blind = BlindBuff();
            MonoEntity flashVfx = BuildFlashVfx();

            GameObject frag = BuildFragGrenade(fragBody, lever, bounce);
            GameObject flash = BuildFlashGrenade(flashBody, lever, flashBand, bounce, blind, flashVfx);

            BuildPickup(GRENADE_ITEM_PREFAB, "GrenadeItem", GrenadeType.Frag, fragBody, lever, null);
            BuildPickup(FLASH_ITEM_PREFAB, "FlashGrenadeItem", GrenadeType.Flash, flashBody, lever, flashBand);

            HookUpCharacterPrefab(frag, flash);
            HookUpPlayground(frag, flash);

            AssetDatabase.SaveAssets();
            Debug.Log($"{LOG} Built Grenade_01, Grenade_Flash_01, their pickups, FX_Flashbang and the player's grenades.");
        }

        // ---------------------------------------------------------------- assets

        private static Material SolidMaterial(string path, Color colour)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = colour;
            material.SetFloat("_Glossiness", 0.35f);
            material.SetFloat("_Metallic", 0.2f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static PhysicsMaterial BouncyMaterial()
        {
            PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PHYSICS_MATERIAL);

            if (material == null)
            {
                material = new PhysicsMaterial("PM_Grenade");
                AssetDatabase.CreateAsset(material, PHYSICS_MATERIAL);
            }

            material.bounciness = 0.35f;
            material.dynamicFriction = 0.6f;
            material.staticFriction = 0.6f;
            material.bounceCombine = PhysicsMaterialCombine.Maximum;
            material.frictionCombine = PhysicsMaterialCombine.Average;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static BlindBuffConfig BlindBuff()
        {
            BlindBuffConfig config = AssetDatabase.LoadAssetAtPath<BlindBuffConfig>(BLIND_BUFF);

            if (config == null)
            {
                config = ScriptableObject.CreateInstance<BlindBuffConfig>();
                AssetDatabase.CreateAsset(config, BLIND_BUFF);
            }

            SerializedObject serialized = new(config);
            serialized.FindProperty("<Name>k__BackingField").stringValue = "Blind";
            serialized.FindProperty("<Duration>k__BackingField").floatValue = BLIND_SECONDS;
            serialized.FindProperty("<StumbleSpeed>k__BackingField").floatValue = BLIND_STUMBLE_SPEED;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        // ---------------------------------------------------------------- flash effect

        /// <summary>
        /// A pooled flash: a bright white point light that dies in a third of a second, and one big
        /// white glow sprite that swells and fades. Everything plays on enable, so re-renting the
        /// pooled effect replays it.
        /// </summary>
        private static MonoEntity BuildFlashVfx()
        {
            GameObject root = new GameObject("FX_Flashbang");

            try
            {
                MonoEntity entity = root.AddComponent<MonoEntity>();
                HitEffectInstaller effect = root.AddComponent<HitEffectInstaller>();
                SetFloat(effect, "_lifeTime", 1f);
                SetInstallers(entity, effect);

                GameObject flash = new GameObject("Flash");
                flash.transform.SetParent(root.transform, false);
                flash.transform.localPosition = Vector3.up * 0.7f;
                Light light = flash.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = Color.white;
                light.range = 16f;
                light.intensity = 12f;
                light.renderMode = LightRenderMode.ForcePixel;
                light.shadows = LightShadows.None;
                ExplosionFlash fade = flash.AddComponent<ExplosionFlash>();
                SetFloat(fade, "_duration", 0.35f);
                SetFloat(fade, "_intensity", 12f);

                GameObject burst = new GameObject("Burst");
                burst.transform.SetParent(root.transform, false);
                ParticleSystem particles = burst.AddComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                ParticleSystem.MainModule main = particles.main;
                main.duration = 0.3f;
                main.loop = false;
                main.playOnAwake = true;
                main.startLifetime = 0.3f;
                main.startSpeed = 0f;
                main.startSize = 6f;
                main.startColor = Color.white;
                main.maxParticles = 1;
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                ParticleSystem.EmissionModule emission = particles.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

                ParticleSystem.ShapeModule shape = particles.shape;
                shape.enabled = false;

                ParticleSystem.ColorOverLifetimeModule colour = particles.colorOverLifetime;
                colour.enabled = true;
                Gradient gradient = new();
                gradient.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
                colour.color = gradient;

                ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.3f));

                ParticleSystemRenderer renderer = burst.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(GLOW_MATERIAL);
                renderer.renderMode = ParticleSystemRenderMode.Billboard;

                return PrefabUtility.SaveAsPrefabAsset(root, FLASH_VFX_PREFAB).GetComponent<MonoEntity>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---------------------------------------------------------------- grenades

        private static GameObject BuildFragGrenade(Material bodyMaterial, Material leverMaterial, PhysicsMaterial bounce)
        {
            GameObject rocket = AssetDatabase.LoadAssetAtPath<GameObject>(ROCKET_PREFAB);
            GameObject root = NewGrenade("Grenade_01", rocket.layer, bodyMaterial, leverMaterial, null, bounce,
                out MonoEntity entity, out GrenadeInstaller grenade);

            try
            {
                // The rocket's blast, bomb included, a little wider: a grenade lands beside a target
                // rather than on it. The bomb's type is taken off the rocket's component, so this
                // builder needs no RayFire types.
                Component rocketBomb = rocket.GetComponent(ROCKET_BOMB_TYPE);
                Component bomb = root.AddComponent(rocketBomb.GetType());
                EditorUtility.CopySerialized(rocketBomb, bomb);
                SetFloat(bomb, "strength", BOMB_STRENGTH);

                ExplosionInstaller explosion = root.AddComponent<ExplosionInstaller>();
                EditorUtility.CopySerialized(rocket.GetComponent<ExplosionInstaller>(), explosion);
                SetField(explosion, "_bomb", bomb);
                SetFloat(explosion, "_radius", EXPLOSION_RADIUS);
                SetFloat(explosion, "_breachRadius", BREACH_RADIUS);
                SetFloat(explosion, "_breachSpeed", BREACH_SPEED);

                SetInstallers(entity, grenade, explosion);

                return PrefabUtility.SaveAsPrefabAsset(root, GRENADE_PREFAB);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildFlashGrenade(Material bodyMaterial, Material leverMaterial, Material bandMaterial,
            PhysicsMaterial bounce, BuffConfig blind, MonoEntity vfx)
        {
            GameObject rocket = AssetDatabase.LoadAssetAtPath<GameObject>(ROCKET_PREFAB);
            GameObject root = NewGrenade("Grenade_Flash_01", rocket.layer, bodyMaterial, leverMaterial, bandMaterial,
                bounce, out MonoEntity entity, out GrenadeInstaller grenade);

            try
            {
                SetInt(grenade, "_damage", 0);
                SetFloat(grenade, "_fuse", FLASH_FUSE);

                FlashInstaller flash = root.AddComponent<FlashInstaller>();
                SetFloat(flash, "_radius", FLASH_RADIUS);
                SetField(flash, "_blindBuff", blind);
                SetField(flash, "_vfxPrefab", vfx);

                // A light jolt: a flash bangs, it does not blow anything up.
                SerializedObject flashSettings = new(flash);
                SerializedProperty shake = flashSettings.FindProperty("_shake");
                shake.FindPropertyRelative("<ShakeDuration>k__BackingField").floatValue = FLASH_SHAKE_DURATION;
                shake.FindPropertyRelative("<ShakeStrength>k__BackingField").floatValue = FLASH_SHAKE_STRENGTH;
                flashSettings.ApplyModifiedPropertiesWithoutUndo();

                SetInstallers(entity, grenade, flash);

                return PrefabUtility.SaveAsPrefabAsset(root, FLASH_PREFAB);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>The body both grenades share. The caller saves the prefab and destroys the root.</summary>
        private static GameObject NewGrenade(string name, int layer, Material bodyMaterial, Material leverMaterial,
            Material bandMaterial, PhysicsMaterial bounce, out MonoEntity entity, out GrenadeInstaller grenade)
        {
            GameObject root = new GameObject(name) { layer = layer };

            CreateModel(root.transform, bodyMaterial, leverMaterial, bandMaterial);

            SphereCollider collider = root.AddComponent<SphereCollider>();
            collider.radius = BODY_RADIUS;
            collider.sharedMaterial = bounce;

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 0.4f;
            body.linearDamping = 0f;
            // Only calms the tumble. Stopping the grenade where it lands is GrenadeLandingBehaviour's
            // job: held from rolling, a sphere just slides on friction instead.
            body.angularDamping = ANGULAR_DAMPING;
            body.useGravity = true;
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            entity = root.AddComponent<MonoEntity>();
            CollisionEventReceiver receiver = root.AddComponent<CollisionEventReceiver>();

            grenade = root.AddComponent<GrenadeInstaller>();
            SetField(grenade, "_rigidbody", body);
            SetField(grenade, "_collisionReceiver", receiver);

            return root;
        }

        /// <summary>An egg-shaped body with a grey fuse lever on top and an optional band, no colliders.</summary>
        private static void CreateModel(Transform parent, Material bodyMaterial, Material leverMaterial, Material bandMaterial)
        {
            Transform model = new GameObject("<Model>") { layer = parent.gameObject.layer }.transform;
            model.SetParent(parent, false);

            GameObject body = Primitive(PrimitiveType.Sphere, "Body", model, bodyMaterial);
            body.transform.localScale = new Vector3(0.12f, 0.15f, 0.12f);

            if (bandMaterial != null)
            {
                GameObject band = Primitive(PrimitiveType.Cylinder, "Band", model, bandMaterial);
                band.transform.localScale = new Vector3(0.125f, 0.012f, 0.125f);
            }

            GameObject lever = Primitive(PrimitiveType.Cylinder, "Lever", model, leverMaterial);
            lever.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            lever.transform.localScale = new Vector3(0.045f, 0.015f, 0.045f);

            GameObject spoon = Primitive(PrimitiveType.Cube, "Spoon", model, leverMaterial);
            spoon.transform.localPosition = new Vector3(0.035f, 0.045f, 0f);
            spoon.transform.localRotation = Quaternion.Euler(0f, 0f, -20f);
            spoon.transform.localScale = new Vector3(0.015f, 0.07f, 0.025f);
        }

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.layer = parent.gameObject.layer;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }

        // ---------------------------------------------------------------- pickups

        private static void BuildPickup(string path, string name, GrenadeType type, Material bodyMaterial,
            Material leverMaterial, Material bandMaterial)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(AMMO_ITEM_PREFAB);

            try
            {
                root.name = name;

                // The ammo box carries the HighlightEffect ItemVisualInstaller points at: keep the
                // object, swap its mesh for the grenade body and hang the lever under it.
                MeshRenderer box = root.GetComponentInChildren<MeshRenderer>(true);
                GameObject model = box.gameObject;
                model.name = "Grenade";

                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                model.GetComponent<MeshFilter>().sharedMesh = sphere.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(sphere);

                box.sharedMaterials = new[] { bodyMaterial };
                model.transform.localPosition = new Vector3(0f, 0.25f, 0f);
                model.transform.localRotation = Quaternion.Euler(0f, 0f, 15f);
                model.transform.localScale = new Vector3(0.28f, 0.35f, 0.28f);

                foreach (Transform child in model.transform)
                    Object.DestroyImmediate(child.gameObject);

                if (bandMaterial != null)
                {
                    GameObject band = Primitive(PrimitiveType.Cylinder, "Band", model.transform, bandMaterial);
                    band.transform.localScale = new Vector3(1.04f, 0.08f, 1.04f);
                }

                GameObject lever = Primitive(PrimitiveType.Cylinder, "Lever", model.transform, leverMaterial);
                lever.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                lever.transform.localScale = new Vector3(0.35f, 0.1f, 0.35f);

                GameObject spoon = Primitive(PrimitiveType.Cube, "Spoon", model.transform, leverMaterial);
                spoon.transform.localPosition = new Vector3(0.28f, 0.3f, 0f);
                spoon.transform.localRotation = Quaternion.Euler(0f, 0f, -20f);
                spoon.transform.localScale = new Vector3(0.1f, 0.45f, 0.18f);

                BoxCollider collider = root.GetComponent<BoxCollider>();
                collider.center = new Vector3(0f, 0.25f, 0f);
                collider.size = new Vector3(0.4f, 0.5f, 0.4f);

                // The ammo installer goes; the grenade one takes its place at the head of the list,
                // ahead of ItemVisualInstaller, which listens to the PickUpEvent it adds.
                Object.DestroyImmediate(root.GetComponent<AmmoPickUpInstaller>());
                GrenadePickUpInstaller pickup = root.AddComponent<GrenadePickUpInstaller>();
                SetEnum(pickup, "_type", (int)type);
                SetInstallers(root.GetComponent<MonoEntity>(), pickup, root.GetComponent<ItemVisualInstaller>());

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ---------------------------------------------------------------- character

        private static void HookUpCharacterPrefab(GameObject frag, GameObject flash)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CHARACTER_PREFAB);

            try
            {
                HookUp(root, frag, flash);
                PrefabUtility.SaveAsPrefabAsset(root, CHARACTER_PREFAB);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// The Playground's [Character] was cloned from GameScene and unpacked, so the prefab change
        /// does not reach it. Its grenade pickups are placed here too.
        /// </summary>
        private static void HookUpPlayground(GameObject frag, GameObject flash)
        {
            Scene scene = SceneManager.GetActiveScene();

            if (scene.path != PLAYGROUND_SCENE)
            {
                Debug.LogWarning($"{LOG} Playground is not the active scene; the scene was left alone.");
                return;
            }

            GameObject character = GameObject.Find("[Character]");

            if (character == null)
            {
                Debug.LogError($"{LOG} No [Character] in the Playground.");
                return;
            }

            if (!PrefabUtility.IsPartOfPrefabInstance(character))
                HookUp(character, frag, flash);

            PlacePickups(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void HookUp(GameObject character, GameObject frag, GameObject flash)
        {
            Transform throwPoint = character.transform.Find(THROW_POINT);

            if (throwPoint == null)
            {
                throwPoint = new GameObject(THROW_POINT).transform;
                throwPoint.SetParent(character.transform, false);
                throwPoint.localPosition = ThrowPointPosition;
            }

            FlashScreen(character.transform, out Image whiteout, out Text hud);
            MonoEntity entity = character.GetComponent<MonoEntity>();

            if (!character.TryGetComponent(out GrenadeThrowerInstaller grenades))
                grenades = character.AddComponent<GrenadeThrowerInstaller>();

            SetSlots(grenades, (GrenadeType.Frag, frag, FRAG_COUNT), (GrenadeType.Flash, flash, FLASH_COUNT));
            SetField(grenades, "_throwPoint", throwPoint);
            SetField(grenades, "_hud", hud);
            AppendInstaller(entity, grenades);

            if (!character.TryGetComponent(out FlashScreenInstaller dazzle))
                dazzle = character.AddComponent<FlashScreenInstaller>();

            SetField(dazzle, "_whiteout", whiteout);
            AppendInstaller(entity, dazzle);
        }

        /// <summary>
        /// The slots in switching order. A slot already there keeps the count it was given in the
        /// inspector; a new one starts with the default.
        /// </summary>
        private static void SetSlots(GrenadeThrowerInstaller installer,
            params (GrenadeType type, GameObject prefab, int count)[] slots)
        {
            SerializedObject serialized = new(installer);
            SerializedProperty list = serialized.FindProperty("_slots");

            int[] counts = new int[slots.Length];
            for (int s = 0; s < slots.Length; s++)
            {
                counts[s] = slots[s].count;

                for (int i = 0; i < list.arraySize; i++)
                {
                    SerializedProperty old = list.GetArrayElementAtIndex(i);
                    if (old.FindPropertyRelative("type").enumValueIndex == (int)slots[s].type)
                        counts[s] = old.FindPropertyRelative("count").intValue;
                }
            }

            list.arraySize = slots.Length;

            for (int s = 0; s < slots.Length; s++)
            {
                SerializedProperty slot = list.GetArrayElementAtIndex(s);
                slot.FindPropertyRelative("type").enumValueIndex = (int)slots[s].type;
                slot.FindPropertyRelative("prefab").objectReferenceValue = slots[s].prefab.GetComponent<MonoEntity>();
                slot.FindPropertyRelative("count").intValue = counts[s];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// A Screen Space Overlay canvas on the character: the white full-screen image a flash fades
        /// in, and the grenade line in the bottom-left corner. Created once, then only looked up.
        /// </summary>
        private static void FlashScreen(Transform character, out Image whiteout, out Text hud)
        {
            Transform screen = character.Find(FLASH_SCREEN);

            if (screen == null)
            {
                GameObject canvasObject = new GameObject(FLASH_SCREEN, typeof(RectTransform));
                canvasObject.transform.SetParent(character, false);
                screen = canvasObject.transform;

                Canvas canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;

                CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                // The HUD line first, the white-out over it: a flash hides everything.
                GameObject textObject = new GameObject("GrenadeHud", typeof(RectTransform));
                textObject.transform.SetParent(screen, false);
                RectTransform textRect = (RectTransform)textObject.transform;
                textRect.anchorMin = textRect.anchorMax = textRect.pivot = Vector2.zero;
                textRect.anchoredPosition = new Vector2(32f, 28f);
                textRect.sizeDelta = new Vector2(700f, 50f);
                Text text = textObject.AddComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 30;
                text.color = Color.white;
                text.alignment = TextAnchor.LowerLeft;
                text.raycastTarget = false;
                Shadow shadow = textObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
                shadow.effectDistance = new Vector2(2f, -2f);

                GameObject imageObject = new GameObject("Whiteout", typeof(RectTransform));
                imageObject.transform.SetParent(screen, false);
                RectTransform imageRect = (RectTransform)imageObject.transform;
                imageRect.anchorMin = Vector2.zero;
                imageRect.anchorMax = Vector2.one;
                imageRect.offsetMin = imageRect.offsetMax = Vector2.zero;
                Image image = imageObject.AddComponent<Image>();
                image.color = new Color(1f, 1f, 1f, 0f);
                image.raycastTarget = false;
                image.enabled = false;
            }

            whiteout = screen.Find("Whiteout").GetComponent<Image>();
            hud = screen.Find("GrenadeHud").GetComponent<Text>();
        }

        private static void PlacePickups(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name.StartsWith("GrenadeItem") || root.name.StartsWith("FlashGrenadeItem"))
                    Object.DestroyImmediate(root);

            Place(scene, GRENADE_ITEM_PREFAB, "GrenadeItem", FragPickupPositions);
            Place(scene, FLASH_ITEM_PREFAB, "FlashGrenadeItem", FlashPickupPositions);
        }

        private static void Place(Scene scene, string path, string name, Vector3[] positions)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject pickup = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                pickup.name = i == 0 ? name : $"{name} ({i})";
                pickup.transform.SetPositionAndRotation(positions[i], Quaternion.identity);
            }
        }

        // ---------------------------------------------------------------- helpers

        private static void SetInstallers(MonoEntity entity, params MonoEntityInstaller[] installers)
        {
            SerializedObject serialized = new(entity);
            SerializedProperty list = serialized.FindProperty("sceneInstallers");
            list.arraySize = installers.Length;

            for (int i = 0; i < installers.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = installers[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AppendInstaller(MonoEntity entity, MonoEntityInstaller installer)
        {
            SerializedObject serialized = new(entity);
            SerializedProperty list = serialized.FindProperty("sceneInstallers");

            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == installer)
                    return;

            int index = list.arraySize;
            list.arraySize = index + 1;
            list.GetArrayElementAtIndex(index).objectReferenceValue = installer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SerializedProperty Property(SerializedObject serialized, string field)
        {
            SerializedProperty property = serialized.FindProperty(field);

            if (property == null)
                Debug.LogError($"{LOG} No serialized field '{field}' on {serialized.targetObject.GetType().Name}.");

            return property;
        }

        private static void SetField(Object target, string field, Object value)
        {
            SerializedObject serialized = new(target);
            SerializedProperty property = Property(serialized, field);
            if (property == null) return;

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string field, float value)
        {
            SerializedObject serialized = new(target);
            SerializedProperty property = Property(serialized, field);
            if (property == null) return;

            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(Object target, string field, int value)
        {
            SerializedObject serialized = new(target);
            SerializedProperty property = Property(serialized, field);
            if (property == null) return;

            property.intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(Object target, string field, int index)
        {
            SerializedObject serialized = new(target);
            SerializedProperty property = Property(serialized, field);
            if (property == null) return;

            property.enumValueIndex = index;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
