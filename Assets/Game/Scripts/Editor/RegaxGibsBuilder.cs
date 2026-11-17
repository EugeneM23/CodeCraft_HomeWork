using System.Collections.Generic;
using System.Linq;
using Atomic.Entities;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>
    /// Turns the seven pieces cut from the Regax in Maya into a ragdolled gibs prefab, and gives
    /// Regax.prefab the installer that swaps its body for them on the killing hit.
    ///
    /// SK_Rapax_Gibs.fbx holds the pieces as Gib_* roots, each a skinned mesh with its own
    /// skeleton, the cut holes capped. Their bones keep Regax's names, which is how the
    /// gibs take Regax's pose when it dies.
    ///
    /// Every piece gets a few bodies on its main bones, with a box fitted around the vertices
    /// those bones carry, joined to each other by CharacterJoints. Fingers, toes, the jaw and the
    /// tail's tip ride along with the body above them.
    /// </summary>
    public static class RegaxGibsBuilder
    {
        private const string LOG = "[REGAX GIBS]";

        private const string MODEL = "Assets/Game/Models/Regax/SK_Rapax_Gibs.fbx";
        private const string GIBS_PREFAB = "Assets/Game/Prefabs/Characters/Regax_Gibs.prefab";
        private const string REGAX_PREFAB = "Assets/Game/Prefabs/Characters/Regax.prefab";

        /// <summary>kg per cubic metre of box; Regax comes out a few hundred kilograms.</summary>
        private const float DENSITY = 400f;

        /// <summary>
        /// The bones that get a body, per piece, parents first. Every other bone of the piece
        /// moves with the nearest body above it.
        /// </summary>
        private static readonly Dictionary<string, string[]> BODIES = new()
        {
            ["Gib_Head"] = new[] { "Rapax_ Neck" },
            ["Gib_Arm_L"] = new[] { "Rapax_ L UpperArm", "Rapax_ L Forearm", "Rapax_ L Hand" },
            ["Gib_Arm_R"] = new[] { "Rapax_ R UpperArm", "Rapax_ R Forearm", "Rapax_ R Hand" },
            ["Gib_Leg_L"] = new[] { "Rapax_ L Thigh", "Rapax_ L Calf", "Rapax_ L Foot" },
            ["Gib_Leg_R"] = new[] { "Rapax_ R Thigh", "Rapax_ R Calf", "Rapax_ R Foot" },
            ["Gib_Torso_L"] = new[] { "Rapax_ Pelvis", "Rapax_ Spine2", "Rapax_ Tail" },
            ["Gib_Torso_R"] = new[] { "Rapax_ Pelvis", "Rapax_ Spine2", "Rapax_ Tail" },
        };

        /// <summary>
        /// Where the pieces meet: piece and body on one side, piece and body on the other, whether
        /// the seam is welded, and how often it holds. Rolled anew on every death, so the body
        /// breaks into different clusters each time. Chest with both arms: both arm seams and the
        /// spine hold. One half with its arm and leg: those two hold, the spine gives.
        /// </summary>
        private static readonly (string pieceA, string boneA, string pieceB, string boneB, bool rigid, float hold)[] SEAMS =
        {
            ("Gib_Torso_L", "Rapax_ Spine2", "Gib_Torso_R", "Rapax_ Spine2", true, 0.5f),
            ("Gib_Torso_L", "Rapax_ Spine2", "Gib_Arm_L", "Rapax_ L UpperArm", false, 0.4f),
            ("Gib_Torso_R", "Rapax_ Spine2", "Gib_Arm_R", "Rapax_ R UpperArm", false, 0.4f),
            ("Gib_Torso_L", "Rapax_ Pelvis", "Gib_Leg_L", "Rapax_ L Thigh", false, 0.35f),
            ("Gib_Torso_R", "Rapax_ Pelvis", "Gib_Leg_R", "Rapax_ R Thigh", false, 0.35f),
            ("Gib_Torso_L", "Rapax_ Spine2", "Gib_Head", "Rapax_ Neck", false, 0.2f),
            ("Gib_Torso_R", "Rapax_ Spine2", "Gib_Head", "Rapax_ Neck", false, 0.2f),
        };

        [MenuItem("Tools/Regax/Build Gibs")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"{LOG} stop Play first: the build rewrites Regax.prefab");
                return;
            }

            if (!ConfigureImport())
                return;

            Gibs gibs = BuildGibsPrefab();
            if (gibs == null)
                return;

            WireRegax(gibs);
        }

        // ------------------------------------------------------------------ import

        private static bool ConfigureImport()
        {
            if (AssetImporter.GetAtPath(MODEL) is not ModelImporter importer)
            {
                Debug.LogError($"{LOG} missing {MODEL}");
                return false;
            }

            // Plain skinned meshes: no avatar, no clips, and Regax's own material put on
            // by the build rather than an imported copy.
            bool dirty = importer.animationType != ModelImporterAnimationType.Generic ||
                         importer.avatarSetup != ModelImporterAvatarSetup.NoAvatar ||
                         importer.importAnimation ||
                         importer.materialImportMode != ModelImporterMaterialImportMode.None;

            if (dirty)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
            }

            return true;
        }

        // ------------------------------------------------------------------ gibs prefab

        private static Gibs BuildGibsPrefab()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(MODEL);
            Material material = RegaxMaterial();

            if (model == null || material == null)
            {
                Debug.LogError($"{LOG} missing {MODEL}, or a material on {REGAX_PREFAB}'s skin");
                return null;
            }

            GameObject root = (GameObject) PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "Regax_Gibs";
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var pieces = new List<Gibs.Piece>();

            foreach (var (pieceName, bodyBones) in BODIES)
            {
                Transform piece = root.transform.Find(pieceName);
                SkinnedMeshRenderer skin = piece != null ? piece.GetComponentInChildren<SkinnedMeshRenderer>() : null;

                if (skin == null)
                {
                    Debug.LogError($"{LOG} {MODEL} has no skinned {pieceName}");
                    Object.DestroyImmediate(root);
                    return null;
                }

                skin.sharedMaterial = material;
                // Flying pieces leave the bounds baked for the bind pose behind.
                skin.updateWhenOffscreen = true;

                Rigidbody[] bodies = Rig(piece, skin, bodyBones);
                if (bodies == null)
                {
                    Object.DestroyImmediate(root);
                    return null;
                }

                pieces.Add(new Gibs.Piece { root = piece, bodies = bodies });
            }

            SetLayerRecursively(root, PhysicsLayers.Debris);

            Gibs gibs = root.AddComponent<Gibs>();
            SerializedObject serialized = new(gibs);
            SerializedProperty list = serialized.FindProperty("_pieces");
            list.arraySize = pieces.Count;
            for (int i = 0; i < pieces.Count; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("root").objectReferenceValue = pieces[i].root;
                SerializedProperty bodies = element.FindPropertyRelative("bodies");
                bodies.arraySize = pieces[i].bodies.Length;
                for (int b = 0; b < pieces[i].bodies.Length; b++)
                    bodies.GetArrayElementAtIndex(b).objectReferenceValue = pieces[i].bodies[b];
            }

            if (!WriteSeams(serialized.FindProperty("_seams"), pieces))
            {
                Object.DestroyImmediate(root);
                return null;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            string summary = string.Join(", ", pieces.Select(p => $"{p.root.name} {p.bodies.Length}"));

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, GIBS_PREFAB);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            Debug.Log($"{LOG} built {GIBS_PREFAB}: {summary}");

            return saved.GetComponent<Gibs>();
        }

        private static bool WriteSeams(SerializedProperty list, List<Gibs.Piece> pieces)
        {
            int IndexOf(string name) => pieces.FindIndex(p => p.root.name == name);

            Rigidbody BodyOf(int piece, string bone) =>
                piece < 0 ? null : pieces[piece].bodies.FirstOrDefault(b => b.name == bone);

            list.arraySize = SEAMS.Length;
            for (int i = 0; i < SEAMS.Length; i++)
            {
                var (pieceA, boneA, pieceB, boneB, rigid, hold) = SEAMS[i];
                int a = IndexOf(pieceA), b = IndexOf(pieceB);
                Rigidbody bodyA = BodyOf(a, boneA), bodyB = BodyOf(b, boneB);

                if (bodyA == null || bodyB == null)
                {
                    Debug.LogError($"{LOG} no seam between {pieceA}/{boneA} and {pieceB}/{boneB}");
                    return false;
                }

                SerializedProperty seam = list.GetArrayElementAtIndex(i);
                seam.FindPropertyRelative("pieceA").intValue = a;
                seam.FindPropertyRelative("pieceB").intValue = b;
                seam.FindPropertyRelative("bodyA").objectReferenceValue = bodyA;
                seam.FindPropertyRelative("bodyB").objectReferenceValue = bodyB;
                seam.FindPropertyRelative("rigid").boolValue = rigid;
                seam.FindPropertyRelative("holdChance").floatValue = hold;
            }

            return true;
        }

        /// <summary>
        /// Bodies, boxes and joints on one piece; the model is still in its bind pose here, so
        /// the mesh's vertices sit where the bones expect them.
        /// </summary>
        private static Rigidbody[] Rig(Transform piece, SkinnedMeshRenderer skin, string[] bodyBones)
        {
            Transform[] bones = skin.bones;
            var bodyOf = new Transform[bones.Length];
            var bodyTransforms = new Transform[bodyBones.Length];

            for (int i = 0; i < bodyBones.Length; i++)
            {
                bodyTransforms[i] = FindDeep(piece, bodyBones[i]);
                if (bodyTransforms[i] == null)
                {
                    Debug.LogError($"{LOG} {piece.name} has no bone {bodyBones[i]}");
                    return null;
                }
            }

            // Each skin bone belongs to the nearest body at or above it; a bone above every body
            // (none in these pieces) would go to the first.
            for (int i = 0; i < bones.Length; i++)
            {
                Transform t = bones[i];
                while (t != null && t != piece && !bodyTransforms.Contains(t))
                    t = t.parent;
                bodyOf[i] = t != null && t != piece ? t : bodyTransforms[0];
            }

            // Fit a box around the vertices each body carries, in that body's own space.
            Mesh mesh = skin.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            BoneWeight[] weights = mesh.boneWeights;
            Matrix4x4 meshToWorld = skin.transform.localToWorldMatrix;
            var bounds = new Dictionary<Transform, Bounds>();

            for (int v = 0; v < vertices.Length; v++)
            {
                Transform body = bodyOf[weights[v].boneIndex0];
                Vector3 local = body.InverseTransformPoint(meshToWorld.MultiplyPoint3x4(vertices[v]));

                if (bounds.TryGetValue(body, out Bounds b))
                {
                    b.Encapsulate(local);
                    bounds[body] = b;
                }
                else
                {
                    bounds[body] = new Bounds(local, Vector3.zero);
                }
            }

            var result = new Rigidbody[bodyTransforms.Length];

            for (int i = 0; i < bodyTransforms.Length; i++)
            {
                Transform bone = bodyTransforms[i];
                Bounds box = bounds.TryGetValue(bone, out Bounds found) ? found : new Bounds(Vector3.zero, Vector3.one * 0.1f);

                // Spikes and claws stick out of the boxes; a box fitted to them floats the piece
                // above the floor, so it is trimmed a little.
                Vector3 size = Vector3.Max(box.size * 0.85f, Vector3.one * 0.06f);

                BoxCollider collider = bone.gameObject.AddComponent<BoxCollider>();
                collider.center = box.center;
                collider.size = size;

                Rigidbody body = bone.gameObject.AddComponent<Rigidbody>();
                Vector3 world = Vector3.Scale(size, bone.lossyScale);
                body.mass = Mathf.Max(2f, world.x * world.y * world.z * DENSITY);
                body.linearDamping = 0.05f;
                body.angularDamping = 0.5f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                body.isKinematic = true;
                result[i] = body;

                if (i == 0)
                    continue;

                Transform parentBone = bone.parent;
                while (parentBone != null && !bodyTransforms.Contains(parentBone))
                    parentBone = parentBone.parent;

                CharacterJoint joint = bone.gameObject.AddComponent<CharacterJoint>();
                joint.connectedBody = parentBone != null ? parentBone.GetComponent<Rigidbody>() : result[0];
                joint.axis = Vector3.right;
                joint.swingAxis = Vector3.forward;
                joint.lowTwistLimit = new SoftJointLimit { limit = -30f };
                joint.highTwistLimit = new SoftJointLimit { limit = 30f };
                joint.swing1Limit = new SoftJointLimit { limit = 40f };
                joint.swing2Limit = new SoftJointLimit { limit = 25f };
                joint.enableProjection = true;
                joint.enablePreprocessing = false;
            }

            return result;
        }

        // ------------------------------------------------------------------ Regax

        /// <summary>
        /// Gives Regax.prefab the gibs already built, for when Regax itself has been
        /// rebuilt (Tools/Regax/Build starts from the zombie, which has none).
        /// </summary>
        public static void WireExistingGibs()
        {
            Gibs gibs = AssetDatabase.LoadAssetAtPath<Gibs>(GIBS_PREFAB);
            if (gibs == null)
            {
                Debug.LogWarning($"{LOG} no {GIBS_PREFAB} yet; run Tools/Regax/Build Gibs");
                return;
            }

            WireRegax(gibs);
        }

        private static void WireRegax(Gibs gibs)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(REGAX_PREFAB);

            try
            {
                Transform skeleton = root.transform.Find("<Skeleton>");
                Transform skin = skeleton != null ? skeleton.Find("<Skin>") : null;
                MonoEntity entity = root.GetComponent<MonoEntity>();

                if (skeleton == null || skin == null || entity == null)
                {
                    Debug.LogError($"{LOG} {REGAX_PREFAB} has no <Skeleton>, <Skin> or MonoEntity");
                    return;
                }

                CharacterGibsInstaller installer = root.GetComponent<CharacterGibsInstaller>();
                if (installer == null)
                    installer = root.AddComponent<CharacterGibsInstaller>();

                SerializedObject settings = new(installer);
                settings.FindProperty("_gibs").objectReferenceValue = gibs;
                settings.FindProperty("_skeleton").objectReferenceValue = skeleton;
                SerializedProperty body = settings.FindProperty("_body");
                body.arraySize = 1;
                body.GetArrayElementAtIndex(0).objectReferenceValue = skin.GetComponent<SkinnedMeshRenderer>();
                settings.ApplyModifiedPropertiesWithoutUndo();

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

                PrefabUtility.SaveAsPrefabAsset(root, REGAX_PREFAB);
                Debug.Log($"{LOG} {REGAX_PREFAB} now breaks into {GIBS_PREFAB} on death");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// Whatever the living Regax wears, so the pieces match it. The Rapax pack has two
        /// tints and the prefab need not wear the one named after the model.
        /// </summary>
        private static Material RegaxMaterial()
        {
            GameObject regax = AssetDatabase.LoadAssetAtPath<GameObject>(REGAX_PREFAB);
            SkinnedMeshRenderer skin = regax != null ? regax.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
            return skin != null ? skin.sharedMaterial : null;
        }

        internal static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t;
            return null;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursively(child.gameObject, layer);
        }
    }
}
