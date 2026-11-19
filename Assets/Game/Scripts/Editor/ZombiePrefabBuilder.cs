using System.Collections.Generic;
using System.Linq;
using System.Text;
using Atomic.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools/Zombie/Build Zombie Prefab: Zombie.prefab as a variant of Regax.prefab with its AI, health, bite and
    /// Atomic setup, and the low-poly zombie's look in place of the Regax's: the Synty skin (SM_Chr_Zombie_Male_01 of
    /// SK_Zombie_Characters.fbx, URP Lit on the T_Zombie_03_A atlas) on its own humanoid skeleton, the Zombie_* clips
    /// through an override of AC_Rapax (same states and parameters, so the Regax's animation code drives it), a 1.9 m
    /// body and colliders, and the voxel death cut along the zombie's bones. The Regax's skin and bones come off.
    /// Re-running rebuilds the variant in place (its GUID stays).
    /// </summary>
    public static class ZombiePrefabBuilder
    {
        private const string LOG = "[ZOMBIE PREFAB]";
        public const string ZOMBIE_PREFAB = "Assets/Game/Prefabs/Characters/Zombie.prefab";
        private const string REGAX_PREFAB = "Assets/Game/Prefabs/Characters/Regax.prefab";
        private const string MODEL = "Assets/Game/Models/Zombie/SK_Zombie_Characters.fbx";
        private const string SKIN = "SM_Chr_Zombie_Male_01";
        /// <summary>The vivid atlas (Tools/Zombie/Make Vivid Atlas, Game.Horde.EditorTools.ZombieAtlas).</summary>
        private const string ALBEDO = "Assets/Game/Textures/Zombie/T_Zombie_03_A_Vivid.png";
        private const string MATERIAL = "Assets/Game/Materials/Zombie/MT_Zombie.mat";
        private const string BASE_CONTROLLER = "Assets/Game/Animations/Rapax/AC_Rapax.controller";
        private const string CONTROLLER = "Assets/Game/Animations/Zombie/AC_Zombie.overrideController";
        private const string CLIPS = "Assets/Game/Animations/Zombie/";

        /// <summary>Height of the zombie, m: the horde's crawler (render scale 0.4 of the 7.125 m bake), half as big again as the first 1.9 m.</summary>
        private const float HEIGHT = 2.85f;

        private const float COLLIDER_RADIUS = 0.5f;

        /// <summary>Reach (m) of the bite (Regax_Hand's damage radius, 0.9 on the Regax), grown with the body.</summary>
        private const float BITE_RADIUS = 1.35f;

        /// <summary>Grey-green of the atlas' skin, for the skin voxels of the voxel death.</summary>
        private static readonly Color SkinColor = new(0.36f, 0.56f, 0.26f, 1f);

        [MenuItem("Tools/Zombie/Build Zombie Prefab")]
        public static void BuildFromMenu() => Debug.Log(Build());

        /// <summary>Builds or rebuilds Zombie.prefab; returns a short report.</summary>
        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return $"{LOG} leave Play first";

            var report = new StringBuilder(LOG).Append(' ');
            MakeModelReadable(report);
            AnimatorOverrideController controller = BuildController(report);
            Material material = BuildMaterial();

            var regax = AssetDatabase.LoadAssetAtPath<GameObject>(REGAX_PREFAB);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(MODEL);
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(MODEL).OfType<Avatar>().FirstOrDefault();
            if (regax == null || model == null || avatar == null || controller == null)
                return $"{LOG} missing {REGAX_PREFAB}, {MODEL}, its avatar or the controller";

            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(regax, scene);
                Transform skeleton = root.transform.Find("<Skeleton>");
                Animator animator = skeleton != null ? skeleton.GetComponent<Animator>() : null;
                if (animator == null)
                    return $"{LOG} {REGAX_PREFAB} has no <Skeleton> with an Animator";

                // ---------------------------------------------------------- the Regax's look comes off
                var regaxLook = new List<GameObject>();
                foreach (Transform child in skeleton)
                    regaxLook.Add(child.gameObject);
                foreach (GameObject old in regaxLook)
                    Object.DestroyImmediate(old);

                // ---------------------------------------------------------- the zombie's skin and bones go in
                var source = (GameObject)Object.Instantiate(model);
                SceneManager.MoveGameObjectToScene(source, scene);
                SkinnedMeshRenderer skin = source.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == SKIN);
                Transform bones = source.transform.Find("Root");
                if (skin == null || bones == null)
                {
                    Object.DestroyImmediate(source);
                    return $"{LOG} no {SKIN} or Root in {MODEL}";
                }

                bones.SetParent(skeleton, false);
                skin.transform.SetParent(skeleton, false);
                skin.gameObject.name = "<Skin>";
                skin.gameObject.SetActive(true);
                Object.DestroyImmediate(source);

                skin.sharedMaterial = material;
                skin.updateWhenOffscreen = false;
                SetLayer(skeleton, skeleton.gameObject.layer);

                // Humanoid: the avatar finds the bones by their paths under the Animator, Root/Hips/... as in the model.
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;

                float modelHeight = skin.sharedMesh.bounds.size.y * skin.transform.lossyScale.y;
                float scale = HEIGHT / Mathf.Max(modelHeight, 0.01f);
                skeleton.localScale = Vector3.one * scale;
                report.Append($"skin {skin.sharedMesh.vertexCount} verts, model {modelHeight:F2} m x {scale:F3} = {HEIGHT} m; ");

                // ---------------------------------------------------------- the bite's weapon switches to the zombie's controller
                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null)
                        continue;
                    var serialized = new SerializedObject(behaviour);
                    SerializedProperty animController = serialized.FindProperty("_animController");
                    if (animController == null)
                        continue;
                    animController.objectReferenceValue = controller;
                    // The bite reaches as far as the bigger body's arms.
                    SerializedProperty damageRadius = serialized.FindProperty("_damageRadius");
                    if (damageRadius != null)
                        damageRadius.floatValue = BITE_RADIUS;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                // ---------------------------------------------------------- colliders to the body
                foreach (CapsuleCollider capsule in root.GetComponentsInChildren<CapsuleCollider>(true))
                {
                    capsule.radius = COLLIDER_RADIUS;
                    capsule.height = HEIGHT;
                    capsule.center = new Vector3(0f, HEIGHT * 0.5f, 0f);
                }

                // ---------------------------------------------------------- what pointed at the Regax's bones
                Transform hips = FindDeep(skeleton, "Hips");
                Transform chest = FindDeep(skeleton, "Spine_03");
                foreach (CharacterVfxInstaller effects in root.GetComponentsInChildren<CharacterVfxInstaller>(true))
                    SetReference(effects, "_groundPoint", hips);
                foreach (VoxelBloodInstaller blood in root.GetComponentsInChildren<VoxelBloodInstaller>(true))
                    SetReference(blood, "_chest", chest);
                foreach (CharacterVoxelGibsInstaller gibs in root.GetComponentsInChildren<CharacterVoxelGibsInstaller>(true))
                    WireGibs(gibs, skin, skeleton);

                // ---------------------------------------------------------- save as the variant
                root.name = "Zombie";
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, ZOMBIE_PREFAB, out bool ok);
                if (!ok || saved == null)
                    return $"{LOG} could not save {ZOMBIE_PREFAB}";
                report.Append($"saved {ZOMBIE_PREFAB} (variant of {PrefabUtility.GetCorrespondingObjectFromSource(saved)?.name})");
                return report.ToString();
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        /// <summary>The voxel death bakes the skin on the CPU: a build keeps a mesh's vertices only when it is readable.</summary>
        private static void MakeModelReadable(StringBuilder report)
        {
            if (AssetImporter.GetAtPath(MODEL) is not ModelImporter importer || importer.isReadable)
                return;
            importer.isReadable = true;
            importer.SaveAndReimport();
            report.Append("model Read/Write on; ");
        }

        /// <summary>AC_Rapax with the zombie's clips: Idle, Move, Attack, TakeDamage, Death on the same parameters.</summary>
        private static AnimatorOverrideController BuildController(StringBuilder report)
        {
            var baseController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(BASE_CONTROLLER);
            if (baseController == null)
                return null;

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(CONTROLLER);
            if (controller == null)
            {
                controller = new AnimatorOverrideController(baseController) { name = "AC_Zombie" };
                AssetDatabase.CreateAsset(controller, CONTROLLER);
            }

            controller.runtimeAnimatorController = baseController;
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(controller.overridesCount);
            controller.GetOverrides(overrides);
            for (int i = 0; i < overrides.Count; i++)
            {
                AnimationClip original = overrides[i].Key;
                string zombie = ZombieClipFor(original.name);
                AnimationClip clip = zombie != null ? AssetDatabase.LoadAssetAtPath<AnimationClip>(CLIPS + zombie + ".anim") : null;
                if (clip == null)
                    report.Append($"no zombie clip for {original.name}; ");
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, clip);
            }

            controller.ApplyOverrides(overrides);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        /// <summary>The zombie's clip for a Regax clip of AC_Rapax, by what it shows.</summary>
        private static string ZombieClipFor(string regaxClip)
        {
            string name = regaxClip.ToLowerInvariant();
            if (name.Contains("death"))
                return "Zombie_Death_Handgun";
            if (name.Contains("attack"))
                return "Zombie_Attack";
            if (name.Contains("hit"))
                return "Zombie_TakeDamage_01";
            if (name.Contains("run") || name.Contains("walk"))
                return "Zombie_Move";
            if (name.Contains("idle"))
                return "Zombie_Idle";
            return null;
        }

        private static Material BuildMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL);
            if (material == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Game/Materials/Zombie"))
                    AssetDatabase.CreateFolder("Assets/Game/Materials", "Zombie");
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, MATERIAL);
            }

            material.shader = Shader.Find("Universal Render Pipeline/Lit");
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ALBEDO));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.15f);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        /// <summary>The voxel death on the zombie's skin and bones: the Regax's seven parts and seams, its speeds and flesh.</summary>
        private static void WireGibs(CharacterVoxelGibsInstaller gibs, SkinnedMeshRenderer skin, Transform skeleton)
        {
            var serialized = new SerializedObject(gibs);
            serialized.FindProperty("_skin").objectReferenceValue = skin;
            serialized.FindProperty("_skeleton").objectReferenceValue = skeleton;
            SerializedProperty hide = serialized.FindProperty("_hide");
            hide.arraySize = 1;
            hide.GetArrayElementAtIndex(0).objectReferenceValue = skin;

            SerializedProperty config = serialized.FindProperty("_config");
            config.FindPropertyRelative("spineFrom").stringValue = "Hips";
            config.FindPropertyRelative("spineTo").stringValue = "Spine_03";
            config.FindPropertyRelative("leftMarker").stringValue = "Shoulder_L";
            config.FindPropertyRelative("skinColor").colorValue = SkinColor;

            // Fewer, bigger voxels (the user): 10 cm instead of the Regax's 5 cm, boxes of two (20 cm); fewer, bigger drops.
            config.FindPropertyRelative("voxelSize").floatValue = 0.1f;
            config.FindPropertyRelative("colliderCell").intValue = 2;
            config.FindPropertyRelative("seamDrops").intValue = 60;
            config.FindPropertyRelative("hitDrops").intValue = 70;
            config.FindPropertyRelative("gooSpeed").floatValue = 7.5f;
            // The heavy drop after the sluggish rise lands at ~8 m/s: the parts keep whole (for the tracks to burst).
            config.FindPropertyRelative("fractureSpeed").floatValue = 12f;
            config.FindPropertyRelative("blood").FindPropertyRelative("dropSize").floatValue = 0.08f;

            // Sticky, not peas: the pieces go up more than out, scatter and spin little (VoxelGibs damps the rise).
            SerializedProperty speeds = config.FindPropertyRelative("speeds");
            speeds.FindPropertyRelative("minSpeed").floatValue = 2f;
            speeds.FindPropertyRelative("maxSpeed").floatValue = 3.5f;
            speeds.FindPropertyRelative("lift").floatValue = 1.2f;
            speeds.FindPropertyRelative("scatter").floatValue = 0.25f;
            speeds.FindPropertyRelative("spin").floatValue = 1.2f;
            speeds.FindPropertyRelative("blastLift").floatValue = 1.4f;
            speeds.FindPropertyRelative("blastSpin").floatValue = 2f;

            // Same order as the Regax's (the seams name the parts by index).
            var parts = new (string name, string[] bones, int side)[]
            {
                ("Head", new[] { "Neck", "Head", "Jaw" }, 0),
                ("Arm_L", new[] { "Shoulder_L", "Elbow_L", "Hand_L" }, 0),
                ("Arm_R", new[] { "Shoulder_R", "Elbow_R", "Hand_R" }, 0),
                ("Leg_L", new[] { "UpperLeg_L", "LowerLeg_L", "Ankle_L", "Ball_L", "Toes_L" }, 0),
                ("Leg_R", new[] { "UpperLeg_R", "LowerLeg_R", "Ankle_R", "Ball_R", "Toes_R" }, 0),
                ("Torso_L", new[] { "Hips", "Spine_01", "Spine_02", "Spine_03", "Clavicle_L", "Clavicle_R" }, 1),
                ("Torso_R", new[] { "Hips", "Spine_01", "Spine_02", "Spine_03", "Clavicle_L", "Clavicle_R" }, -1)
            };

            SerializedProperty list = config.FindPropertyRelative("parts");
            list.arraySize = parts.Length;
            for (int i = 0; i < parts.Length; i++)
            {
                SerializedProperty part = list.GetArrayElementAtIndex(i);
                part.FindPropertyRelative("name").stringValue = parts[i].name;
                part.FindPropertyRelative("side").intValue = parts[i].side;
                SerializedProperty names = part.FindPropertyRelative("bones");
                names.arraySize = parts[i].bones.Length;
                for (int b = 0; b < parts[i].bones.Length; b++)
                    names.GetArrayElementAtIndex(b).stringValue = parts[i].bones[b];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
                return;
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetLayer(Transform root, int layer)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t;
            return null;
        }
    }
}
