using System.Linq;
using Atomic.Entities;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the Regax enemy out of the zombie's Atomic setup and the Rapax art.
    ///
    /// The prefab is described here rather than authored by hand for the same reason the
    /// playground is: Zombie.prefab is 55 000 lines of YAML, and hand-editing a skinned
    /// hierarchy at that size is how prefabs get corrupted.
    ///
    /// Everything gameplay comes from Zombie.prefab untouched -- MonoEntity and the five
    /// installers with all their serialized tuning. Only the visual subtrees are swapped, and
    /// only the references that pointed into them are rewritten. Keeping the &lt;Skeleton&gt;,
    /// &lt;Skin&gt;, &lt;Collider&gt;, &lt;Audio&gt;, &lt;Weapons&gt;, &lt;VFX&gt; and
    /// &lt;PatrolPoints&gt; objects themselves, and replacing their contents, is what keeps the
    /// rest of the wiring valid.
    /// </summary>
    public static class RegaxPrefabBuilder
    {
        private const string LOG = "[REGAX]";

        private const string SOURCE_PREFAB = "Assets/Game/Prefabs/Characters/Zombie.prefab";
        private const string TARGET_PREFAB = "Assets/Game/Prefabs/Characters/Regax.prefab";

        private const string MODEL = "Assets/3rdParty/Rapax/FBX Files/SK_Rapax.fbx";
        private const string MATERIAL = "Assets/3rdParty/Rapax/Materials/M_Rapax.mat";
        private const string CONTROLLER = "Assets/Game/Animations/Rapax/AC_Rapax.controller";
        private const string WEAPON = "Assets/Game/Prefabs/Weapons/Regax_Hand.prefab";

        private const string ATTACK_MODEL = "Assets/3rdParty/Rapax/FBX Files/Rapax@RightClawsAttack.fbx";
        private const string ATTACK_CLIP = "Assets/Game/Animations/Rapax/Rapax_Attack.anim";

        private const string HIT_EFFECT =
            "Assets/3rdParty/RPG_VFXPack/Prefab/HitImpact_VFX_Pref/HitImpact_VFX_Zombie.prefab";

        /// <summary>
        /// KriptoFX Volumetric Blood FX's effects, as the pack ships them: a volumetric gush and
        /// its ground decal each, at the pack's own animation speed. Every one of them in this
        /// folder (not its SimplePlaneDecals copies) goes into the death blood config.
        /// AttachedBloodDecal is left out: it is a stain for a body, with no gush.
        /// </summary>
        private const string DEATH_BLOOD_PACK = "Assets/3rdParty/KriptoFX/VolumetricBloodFX/Prefabs";

        /// <summary>
        /// Which effect a death throws, at what size and in what colour (green).
        /// See <see cref="DeathBloodConfig"/> for the values.
        /// </summary>
        private const string DEATH_BLOOD_CONFIG = "Assets/Game/Configs/Vfx/RegaxDeathBlood.asset";

        /// <summary>
        /// The bone the blood is anchored to, standing in for the zombie's Spine_01.
        /// </summary>
        private const string SPINE_BONE = "Rapax_ Spine";

        private const string DAMAGE_CAST_EVENT = "damage_cast_event";

        /// <summary>
        /// Where in the attack clip the melee damage goes out. Zombie_Attack fires
        /// damage_cast_event 0.8s into 2.5s, so Regax uses the same fraction of its own,
        /// shorter swing. Tune by eye if the claws and the hit drift apart.
        /// </summary>
        private const float DAMAGE_CAST_FRACTION = 0.8f / 2.5f;

        /// <summary>
        /// Regax's capsule height over the zombie's, used to lift the blood to the same
        /// place on a body more than twice as tall.
        /// </summary>
        private const float HEIGHT_SCALE = 4.28f / 1.8f;

        /// <summary>
        /// One tank shell kills: the cannon deals 150 at the centre of a 5 m blast and down to a
        /// quarter at its edge, so 60 goes within about 4 m of where the shell lands. A rifle
        /// (10 a bullet) needs six hits.
        /// </summary>
        private const int HEALTH = 60;

        [MenuItem("Tools/Regax/Build")]
        public static void Build()
        {
            if (!BuildAttackClip())
                return;

            BuildPrefab();
        }

        // ------------------------------------------------------------------ attack clip

        /// <summary>
        /// Refreshes the standalone attack clip that AC_Rapax's Attack state plays, carrying the
        /// damage_cast_event this project listens for.
        /// </summary>
        /// <remarks>
        /// The enemy's melee damage does not come from its animator at all: DamageCastBehaviour
        /// opens a 0.1s damage window when it hears damage_cast_event, and that event lives in
        /// Zombie_Attack.anim. Swap the clip for one of Rapax's and Regax swings without
        /// ever dealing damage. The event cannot be added to the clip inside the FBX, so the clip
        /// is copied out as an asset of its own and the event added there. AC_Rapax points at
        /// that asset, so refreshing it in place is all this has to do.
        /// </remarks>
        private static bool BuildAttackClip()
        {
            AnimationClip source = AssetDatabase.LoadAllAssetsAtPath(ATTACK_MODEL)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => !clip.name.StartsWith("__preview__"));

            if (source == null)
            {
                Debug.LogError($"{LOG} no animation clip inside {ATTACK_MODEL}");
                return false;
            }

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ATTACK_CLIP);

            if (clip == null)
            {
                clip = Object.Instantiate(source);
                clip.name = "Rapax_Attack";
                AssetDatabase.CreateAsset(clip, ATTACK_CLIP);
            }
            else
            {
                // CopySerialized brings the name across too, which would rename the asset's main
                // object to whatever the clip is called inside the FBX.
                EditorUtility.CopySerialized(source, clip);
                clip.name = "Rapax_Attack";
            }

            // The animators who made this swing marked where it lands, with an event of their
            // own project's naming: CallEvent("DamageCast"). Their timing beats any guess, so it
            // is reused and only the call renamed to the one this project listens for. The
            // zombie's fraction is the fallback if a clip carries no such mark.
            AnimationEvent authored = AnimationUtility.GetAnimationEvents(source)
                .FirstOrDefault(e => e.stringParameter == "DamageCast");

            AnimationEvent cast = new()
            {
                time = authored?.time ?? clip.length * DAMAGE_CAST_FRACTION,
                functionName = "ReceiveEvent",
                stringParameter = DAMAGE_CAST_EVENT
            };

            // Everything else the clip came with goes: the pack's other events call CallEvent,
            // which nothing here implements, and Unity logs an error for every one that fires.
            AnimationUtility.SetAnimationEvents(clip, new[] { cast });
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
            Debug.Log($"{LOG} attack clip ready, damage cast at {cast.time:0.###}s of {clip.length:0.###}s");

            return true;
        }

        // ------------------------------------------------------------------ prefab

        private static void BuildPrefab()
        {
            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SOURCE_PREFAB);

            if (sourcePrefab == null)
            {
                Debug.LogError($"{LOG} missing {SOURCE_PREFAB}");
                return;
            }

            GameObject root = (GameObject) PrefabUtility.InstantiatePrefab(sourcePrefab);

            // OutermostRoot, not Completely: the zombie carries its weapon as a nested prefab
            // instance, and Completely would unpack that too, leaving the new enemy's weapon a
            // loose copy that no longer follows Regax_Hand.prefab.
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot,
                InteractionMode.AutomatedAction);

            root.name = "Regax";

            if (!SwapSkeletonAndSkin(root) ||
                !ResizeColliders(root) ||
                !SwapEffects(root) ||
                !SwapWeapon(root) ||
                !Rewire(root))
            {
                Object.DestroyImmediate(root);
                return;
            }

            // The zombie had every child on its own layer; the art and the effects arrive on 0.
            SetLayerRecursively(root, root.layer);

            PrefabUtility.SaveAsPrefabAsset(root, TARGET_PREFAB);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            Debug.Log($"{LOG} built {TARGET_PREFAB}");

            // The zombie it is rebuilt from has no gibs; put them back.
            RegaxGibsBuilder.WireExistingGibs();

            // …and trades them for the voxel death (Docs/superpowers/specs/2026-11-16-regax-voxel-death.md).
            RegaxVoxelDeathBuilder.Apply();
        }

        private static bool SwapSkeletonAndSkin(GameObject root)
        {
            Transform skeleton = root.transform.Find("<Skeleton>");
            Transform skin = root.transform.Find("<Skin>");

            if (skeleton == null || skin == null)
            {
                Debug.LogError($"{LOG} the source prefab has no <Skeleton> or <Skin>");
                return false;
            }

            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(MODEL);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL);
            RuntimeAnimatorController controller =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CONTROLLER);
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(MODEL).OfType<Avatar>().FirstOrDefault();

            if (modelAsset == null || material == null || controller == null || avatar == null)
            {
                Debug.LogError($"{LOG} missing model, material, controller or avatar for Regax");
                return false;
            }

            GameObject model = (GameObject) PrefabUtility.InstantiatePrefab(modelAsset);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            SkinnedMeshRenderer renderer = model.GetComponentInChildren<SkinnedMeshRenderer>();

            if (renderer == null)
            {
                Debug.LogError($"{LOG} {MODEL} has no SkinnedMeshRenderer");
                Object.DestroyImmediate(model);
                return false;
            }

            // The bone tree is the model's other child. The avatar's bone paths are relative to
            // the GameObject holding the Animator, so the tree has to sit directly under
            // <Skeleton> -- one level deeper and every path misses.
            Transform bones = null;

            foreach (Transform child in model.transform)
            {
                if (child != renderer.transform)
                {
                    bones = child;
                    break;
                }
            }

            if (bones == null)
            {
                Debug.LogError($"{LOG} {MODEL} has no bone hierarchy beside its renderer");
                Object.DestroyImmediate(model);
                return false;
            }

            // Out with the zombie's bones, and with its hair and beard attached to them.
            for (int i = skeleton.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(skeleton.GetChild(i).gameObject);

            renderer.sharedMaterial = material;

            Object.DestroyImmediate(skin.gameObject);

            bones.SetParent(skeleton, false);

            // Under <Skeleton>, not beside it as the zombie has it. An Animator decides whether
            // to cull itself by looking for renderers in its own hierarchy, and with the skin a
            // sibling it finds none, concludes it is never on screen, and -- on the prefab's
            // CullUpdateTransforms -- stops writing the bones. The animation runs, the state
            // machine advances, and Regax stands frozen. The source project nests it this
            // way too. The zombie has the same arrangement and the same frozen locomotion; it is
            // left alone here, since this port does not touch that prefab.
            renderer.transform.SetParent(skeleton, false);
            renderer.gameObject.name = "<Skin>";

            Object.DestroyImmediate(model);

            Animator animator = skeleton.GetComponent<Animator>();

            if (animator == null)
            {
                Debug.LogError($"{LOG} <Skeleton> has no Animator");
                return false;
            }

            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;

            return true;
        }

        private static bool ResizeColliders(GameObject root)
        {
            Transform collider = root.transform.Find("<Collider>");

            if (collider == null)
            {
                Debug.LogError($"{LOG} the source prefab has no <Collider>");
                return false;
            }

            // Regax stands about 4.3 units tall against the zombie's 1.8, so both capsules
            // -- the one on <Collider> and the one on the root, which DeathBehaviour switches off
            // -- take the source project's height.
            //
            // The radius does not: the source enemy's 0.71 makes Regax unable to attack
            // here. EnemyCharacterAttackBehaviour swings only inside one unit, measured centre to centre,
            // and 0.71 against the player's 0.38 keeps the two capsules 1.09 apart at their
            // closest -- the zombie's 0.3 left it 0.68 and well inside. At 0.5 the pair closes to
            // 0.88 and Regax can reach, which is what the number is for.
            foreach (CapsuleCollider capsule in new[]
                     {
                         collider.GetComponent<CapsuleCollider>(),
                         root.GetComponent<CapsuleCollider>()
                     })
            {
                if (capsule == null)
                    continue;

                capsule.radius = 0.5f;
                capsule.height = 4.28f;
                capsule.center = new Vector3(0f, 2f, 0f);
            }

            return true;
        }

        private static bool SwapEffects(GameObject root)
        {
            Transform vfx = root.transform.Find("<VFX>");

            if (vfx == null)
            {
                Debug.LogError($"{LOG} the source prefab has no <VFX>");
                return false;
            }

            GameObject hit = AssetDatabase.LoadAssetAtPath<GameObject>(HIT_EFFECT);

            if (hit == null)
            {
                Debug.LogError($"{LOG} missing the hit effect prefab");
                return false;
            }

            for (int i = vfx.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(vfx.GetChild(i).gameObject);

            // TakeDamageBloodBehaviour plays these two by name of field, one for bullets and one
            // for melee, and calls Play(withChildren: true) -- so a single instance each, with
            // the pack's own Wave, Particles and Glow underneath, is all that is needed.
            //
            // The heights are the zombie's own, scaled to this body. The hit effect's asset sits
            // at (15, 1, 5) -- it was authored at a position in the other project's scene -- so
            // leaving its transform alone would play the blood off to one side of Regax.
            AddEffect(hit, vfx, "BulletDamageBlood", 1f * HEIGHT_SCALE, active: true);
            AddEffect(hit, vfx, "MeleeDamageBlood", 1.4f * HEIGHT_SCALE, active: true);

            // The death blood is not kept inside Regax: DeathBloodInstaller (wired in Rewire)
            // spawns one of the pack's effects at <VFX> when Regax dies.
            return true;
        }

        // ------------------------------------------------------------------ death blood

        /// <summary>
        /// Rebuilds the death blood config from the pack and puts the installer on the built
        /// Regax.prefab, without rebuilding Regax from the zombie.
        /// </summary>
        [MenuItem("Tools/Regax/Refresh Death Blood")]
        public static void RefreshDeathBlood()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"{LOG} stop Play first: the refresh rewrites Regax.prefab");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(TARGET_PREFAB);

            try
            {
                if (!WireDeathBlood(root))
                    return;

                PrefabUtility.SaveAsPrefabAsset(root, TARGET_PREFAB);
                Debug.Log($"{LOG} Regax now throws one of the effects in {DEATH_BLOOD_CONFIG} on death");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Every effect in the pack's prefab folder, in the pack's own order (Blood1, Blood2,
        /// Blood2_Left, ... Blood15). The config's sizes and colours are kept when it exists.
        /// </summary>
        private static DeathBloodConfig EnsureDeathBloodConfig()
        {
            DeathBloodConfig config = AssetDatabase.LoadAssetAtPath<DeathBloodConfig>(DEATH_BLOOD_CONFIG);

            if (config == null)
            {
                string folder = System.IO.Path.GetDirectoryName(DEATH_BLOOD_CONFIG)!.Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder)!.Replace('\\', '/'),
                        System.IO.Path.GetFileName(folder));

                config = ScriptableObject.CreateInstance<DeathBloodConfig>();
                AssetDatabase.CreateAsset(config, DEATH_BLOOD_CONFIG);
            }

            int Number(string name)
            {
                string digits = new(name.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
                return digits.Length > 0 ? int.Parse(digits) : int.MaxValue;
            }

            config.variants = System.IO.Directory.GetFiles(DEATH_BLOOD_PACK, "*.prefab")
                .Select(path => path.Replace('\\', '/'))
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                // The pack's scripts live in Assembly-CSharp, out of this assembly's reach; an effect
                // with a gush is told by the gush's shader instead.
                .Where(prefab => prefab != null && prefab.GetComponentsInChildren<Renderer>(true)
                    .Any(r => r.sharedMaterial != null && r.sharedMaterial.shader.name.EndsWith("BFX_Blood")))
                .OrderBy(prefab => Number(prefab.name))
                .ThenBy(prefab => prefab.name.Length)
                .ThenBy(prefab => prefab.name)
                .ToArray();

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            return config;
        }

        /// <summary>
        /// DeathBloodInstaller on &lt;VFX&gt;, listed in the entity's installers; the old switched-off
        /// DeathBlood child and CharacterVfxInstaller's hold on it go.
        /// </summary>
        private static bool WireDeathBlood(GameObject root)
        {
            Transform vfx = root.transform.Find("<VFX>");
            CharacterVfxInstaller effects = root.GetComponentInChildren<CharacterVfxInstaller>(true);
            MonoEntity entity = root.GetComponent<MonoEntity>();

            if (vfx == null || effects == null || entity == null)
            {
                Debug.LogError($"{LOG} missing <VFX>, CharacterVfxInstaller or MonoEntity");
                return false;
            }

            Transform old = vfx.Find("DeathBlood");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            SetField(effects, "_deathEffect", null);

            DeathBloodInstaller installer = vfx.GetComponent<DeathBloodInstaller>();
            if (installer == null)
                installer = vfx.gameObject.AddComponent<DeathBloodInstaller>();

            SetField(installer, "_config", EnsureDeathBloodConfig());
            SetField(installer, "_anchor", vfx);

            SerializedObject serialized = new(entity);
            SerializedProperty list = serialized.FindProperty("sceneInstallers");
            bool listed = false;
            for (int i = 0; i < list.arraySize; i++)
                listed |= list.GetArrayElementAtIndex(i).objectReferenceValue == installer;

            if (!listed)
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = installer;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return true;
        }

        private static void AddEffect(GameObject prefab, Transform parent, string name, float localY,
            bool active)
        {
            // Left as a nested prefab instance, so edits to the effect still reach the enemy.
            // The rename, the playOnAwake change and the active flag become overrides on it.
            GameObject instance = (GameObject) PrefabUtility.InstantiatePrefab(prefab);

            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = new Vector3(0f, localY, 0f);
            instance.transform.localRotation = Quaternion.identity;

            // These prefabs were built to be spawned on impact, so they start themselves. Here
            // they are children that get Play() called on them instead.
            foreach (ParticleSystem system in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = system.main;
                main.playOnAwake = false;
            }

            instance.SetActive(active);
        }

        private static bool SwapWeapon(GameObject root)
        {
            Transform weapons = root.transform.Find("<Weapons>");

            if (weapons == null)
            {
                Debug.LogError($"{LOG} the source prefab has no <Weapons>");
                return false;
            }

            GameObject weaponAsset = AssetDatabase.LoadAssetAtPath<GameObject>(WEAPON);

            if (weaponAsset == null)
            {
                Debug.LogError($"{LOG} missing {WEAPON}");
                return false;
            }

            for (int i = weapons.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(weapons.GetChild(i).gameObject);

            // A nested instance, as it is on the zombie.
            GameObject weapon = (GameObject) PrefabUtility.InstantiatePrefab(weaponAsset);
            weapon.transform.SetParent(weapons, false);

            return true;
        }

        private static bool Rewire(GameObject root)
        {
            Transform skeleton = root.transform.Find("<Skeleton>");
            Transform vfx = root.transform.Find("<VFX>");
            Transform weapons = root.transform.Find("<Weapons>");

            CharacterAnimInstaller anim = root.GetComponentInChildren<CharacterAnimInstaller>(true);
            CharacterVfxInstaller effects = root.GetComponentInChildren<CharacterVfxInstaller>(true);
            EnemyCharacterCoreInstaller core = root.GetComponentInChildren<EnemyCharacterCoreInstaller>(true);

            if (anim == null || effects == null || core == null)
            {
                Debug.LogError($"{LOG} the source prefab is missing one of the enemy installers");
                return false;
            }

            SetField(anim, "_animator", skeleton.GetComponent<Animator>());
            SetField(anim, "_animationReceiver", skeleton.GetComponent<AnimationEventReceiver>());

            SetField(effects, "_bulletBlood", FindParticles(vfx, "BulletDamageBlood"));
            SetField(effects, "_meleeBlood", FindParticles(vfx, "MeleeDamageBlood"));
            SetField(effects, "_rootTransform", skeleton);

            if (!WireDeathBlood(root))
                return false;

            // The zombie's _groundPoint was its Spine_01 bone, which went out with the skeleton.
            Transform spine = skeleton.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(bone => bone.name == SPINE_BONE);

            if (spine == null)
                Debug.LogError($"{LOG} no bone named '{SPINE_BONE}' in the new skeleton");

            SetField(effects, "_groundPoint", spine);

            MonoEntity weapon = weapons.GetComponentInChildren<MonoEntity>(true);

            if (weapon == null)
            {
                Debug.LogError($"{LOG} the new weapon has no MonoEntity");
                return false;
            }

            SetField(core, "_weapon", weapon);
            SetField(core, "_weaponRoot", weapons);
            SetField(core, "_characterRoot", root.transform);

            // The zombie's 100 outlasted a tank shell landing a few metres off. See HEALTH.
            SerializedObject coreSettings = new(core);
            coreSettings.FindProperty("_health").intValue = HEALTH;
            coreSettings.ApplyModifiedPropertiesWithoutUndo();

            return true;
        }

        // ------------------------------------------------------------------ helpers

        private static ParticleSystem FindParticles(Transform parent, string name)
        {
            Transform child = parent.Find(name);

            if (child == null)
            {
                Debug.LogError($"{LOG} no effect named '{name}' under <VFX>");
                return null;
            }

            ParticleSystem system = child.GetComponent<ParticleSystem>();

            if (system == null)
                Debug.LogError($"{LOG} '{name}' has no ParticleSystem on its root");

            return system;
        }

        /// <summary>
        /// Writes a private serialized field. A plain reflection write would not mark the object
        /// dirty, so it would never reach the saved prefab.
        /// </summary>
        private static void SetField(Object target, string fieldName, Object value)
        {
            SerializedObject serialized = new(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogError($"{LOG} no serialized field '{fieldName}' on {target.GetType().Name}");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;

            foreach (Transform child in target.transform)
                SetLayerRecursively(child.gameObject, layer);
        }
    }
}
