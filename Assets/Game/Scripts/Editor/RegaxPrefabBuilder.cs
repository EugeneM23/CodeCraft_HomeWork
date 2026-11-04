using System.Linq;
using Atomic.Entities;
using UnityEditor;
using UnityEditor.Animations;
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
        private const string CONTROLLER = "Assets/Game/Animations/Rapax/AC_Rapax.overrideController";
        private const string WEAPON = "Assets/Game/Prefabs/Weapons/Regax_Hand.prefab";

        private const string ATTACK_MODEL = "Assets/3rdParty/Rapax/FBX Files/Rapax@RightClawsAttack.fbx";
        private const string ATTACK_CLIP = "Assets/Game/Animations/Rapax/Rapax_Attack.anim";

        private const string HIT_EFFECT =
            "Assets/3rdParty/RPG_VFXPack/Prefab/HitImpact_VFX_Pref/HitImpact_VFX_Zombie.prefab";

        private const string DEATH_EFFECT = "Assets/3rdParty/VolumetricBloodFX/Prefabs/Blood7_02.prefab";

        /// <summary>The clip AC_Zombie plays in its Attack state, and so the override's key.</summary>
        private const string ATTACK_STATE_CLIP = "Zombie_Attack";

        /// <summary>
        /// The bone the blood is anchored to, standing in for the zombie's Spine_01.
        /// </summary>
        private const string SPINE_BONE = "Rapax_ Spine";

        private const string DAMAGE_CAST_EVENT = "damage_cast_event";

        /// <summary>
        /// Where in the attack clip the melee damage goes out. Zombie_Attack fires
        /// damage_cast_event 0.8s into 2.5s, so the creature uses the same fraction of its own,
        /// shorter swing. Tune by eye if the claws and the hit drift apart.
        /// </summary>
        private const float DAMAGE_CAST_FRACTION = 0.8f / 2.5f;

        /// <summary>
        /// The creature's capsule height over the zombie's, used to lift the blood to the same
        /// place on a body more than twice as tall.
        /// </summary>
        private const float HEIGHT_SCALE = 4.28f / 1.8f;

        [MenuItem("Tools/Regax/Build")]
        public static void Build()
        {
            if (!BuildAttackClip())
                return;

            BuildPrefab();
        }

        // ------------------------------------------------------------------ attack clip

        /// <summary>
        /// Copies the Rapax swing into a standalone clip carrying damage_cast_event, and points
        /// AC_Rapax's attack override at it.
        /// </summary>
        /// <remarks>
        /// The enemy's melee damage does not come from its animator at all: DamageCastBehaviour
        /// opens a 0.1s damage window when it hears damage_cast_event, and that event lives in
        /// Zombie_Attack.anim. Swap the clip for one of Rapax's and the creature swings without
        /// ever dealing damage. The event cannot be added to the clip inside the FBX, so the clip
        /// is copied out as an asset of its own and the event added there.
        /// </remarks>
        private static bool BuildAttackClip()
        {
            AnimatorOverrideController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(CONTROLLER);

            if (controller == null)
            {
                Debug.LogError($"{LOG} missing {CONTROLLER}");
                return false;
            }

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

            AnimationClip original = controller.animationClips
                .FirstOrDefault(c => c != null && c.name == ATTACK_STATE_CLIP);

            if (original == null)
            {
                // animationClips reports the clips in use, so once the override is in place the
                // original is no longer among them -- read the base controller instead.
                original = (controller.runtimeAnimatorController as AnimatorController)
                    ?.animationClips.FirstOrDefault(c => c != null && c.name == ATTACK_STATE_CLIP);
            }

            if (original == null)
            {
                Debug.LogError($"{LOG} {ATTACK_STATE_CLIP} is not a clip of AC_Rapax's base controller");
                return false;
            }

            controller[original] = clip;
            EditorUtility.SetDirty(controller);

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
            AnimatorOverrideController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(CONTROLLER);
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(MODEL).OfType<Avatar>().FirstOrDefault();

            if (modelAsset == null || material == null || controller == null || avatar == null)
            {
                Debug.LogError($"{LOG} missing model, material, controller or avatar for the creature");
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

            int skinIndex = skin.GetSiblingIndex();
            renderer.sharedMaterial = material;

            Object.DestroyImmediate(skin.gameObject);

            bones.SetParent(skeleton, false);
            renderer.transform.SetParent(root.transform, false);
            renderer.gameObject.name = "<Skin>";
            renderer.transform.SetSiblingIndex(skinIndex);

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

            // The creature stands about 4.3 units tall against the zombie's 1.8. Both capsules --
            // the one on <Collider> and the one on the root, which DeathBehaviour switches off --
            // take the figures from the source project's own enemy.
            foreach (CapsuleCollider capsule in new[]
                     {
                         collider.GetComponent<CapsuleCollider>(),
                         root.GetComponent<CapsuleCollider>()
                     })
            {
                if (capsule == null)
                    continue;

                capsule.radius = 0.71f;
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
            GameObject death = AssetDatabase.LoadAssetAtPath<GameObject>(DEATH_EFFECT);

            if (hit == null || death == null)
            {
                Debug.LogError($"{LOG} missing the hit or death effect prefab");
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
            // leaving its transform alone would play the blood off to one side of the creature.
            AddEffect(hit, vfx, "BulletDamageBlood", 1f * HEIGHT_SCALE, active: true);
            AddEffect(hit, vfx, "MeleeDamageBlood", 1.4f * HEIGHT_SCALE, active: true);

            // The volumetric blood has no ParticleSystem -- it is a mesh animated from its own
            // BFX scripts, and it plays the moment it is enabled. So it goes in switched off, and
            // DeathEffectBehaviour switches it on when the creature dies. Its own scale of 4 is
            // left alone: the other project used this very effect on this very creature.
            AddEffect(death, vfx, "DeathBlood", 1.4f * HEIGHT_SCALE, active: false);

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

            EnemyAnimInstaller anim = root.GetComponentInChildren<EnemyAnimInstaller>(true);
            EnemyVfxInstaller effects = root.GetComponentInChildren<EnemyVfxInstaller>(true);
            EnemyCoreInstaller core = root.GetComponentInChildren<EnemyCoreInstaller>(true);

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

            Transform deathEffect = vfx.Find("DeathBlood");

            if (deathEffect == null)
                Debug.LogError($"{LOG} no DeathBlood under <VFX>");

            SetField(effects, "_deathEffect", deathEffect == null ? null : deathEffect.gameObject);

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
