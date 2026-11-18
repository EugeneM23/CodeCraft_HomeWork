using System.Collections.Generic;
using Game.Voxels;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Draft: dresses Tank_01 in the Kawaii Tanks "Firefly" (Assets/Game/Art/Tanks/Firefly). Every object the game
    /// drives stays where it is — installers, the turret pivot and its fire points, the machine gun, the suspension
    /// mounts and wheel visuals, the track rig and its links, the exhaust, the hull colliders — only the meshes
    /// under HullVisual change. VoxelizedModel then turns them into voxels at start, part by part as before.
    /// </summary>
    /// <remarks>
    /// The Firefly's meshes are all modelled in one model space (the pack's prefab only adds pivots). A model point
    /// p goes to <c>SCALE · p + OFFSET</c> in HullVisual space: at 0.74 its road wheels sit exactly on our wheel
    /// track (x ±0.788) and their centres on our wheel visuals (y 0.225), and the tank comes to about 2.0 × 1.9 × 3.7 m.
    /// </remarks>
    public static class TankFireflyBuilder
    {
        private const string TANK = "Assets/Game/Prefabs/Vehicles/Tank_01.prefab";
        private const string ART = "Assets/Game/Art/Tanks/Firefly/";
        private const string FBX = ART + "SM_Firefly.fbx";

        private const float SCALE = 0.74f;
        private static readonly Vector3 Offset = new(0f, 0.76f, 0f);

        /// <summary>Firefly pivots, model space (from the pack's SD_Firefly_2.0 prefab).</summary>
        private static readonly Vector3 TurretPivot = new(0f, 0.82f, 0f);
        private static readonly Vector3 Sprocket = new(1.065f, -0.33f, 2.0f);
        private static readonly Vector3 Idler = new(1.065f, -0.35f, -2.086f);
        private static readonly Vector3[] SupportWheels = { new(1.065f, -0.17f, 0.4f), new(1.065f, -0.17f, -1.4f) };
        private const float BARREL_TIP_Z = 3.52f;
        private const float BARREL_START_Z = 1.1f;
        private const float BARREL_Y = 1.2f;

        /// <summary>Brightest channel below this is the atlas's dark background (≈0.13), not paint (olive ≈0.45).</summary>
        private const float MIN_PAINT = 0.25f;

        /// <summary>Barrel diameter in HullVisual metres: six 3 cm voxels.</summary>
        private const float BARREL_DIAMETER = 0.18f;

        /// <summary>Which of our eight wheel visuals a side carry the Firefly's four road wheels: the evenly spread ones.</summary>
        private static readonly string[] WheelsWithMesh = { "1", "3", "6", "8" };

        /// <summary>HullVisual children that stay: what the game drives or reads by name.</summary>
        private static readonly string[] KeepUnderHull =
            { "TurretVisual", "Wheel_", "TrackL_Front", "TrackR_Front", "TrackL_Rear", "TrackR_Rear", "TrackL_Link", "TrackR_Link", "ExhaustPipe", "ExhaustLeft", "ExhaustRight" };

        private static Vector3 Model(Vector3 p) => p * SCALE + Offset;

        [MenuItem("Tools/Tank/Use Firefly Model")]
        public static void Build()
        {
            Dictionary<string, Mesh> meshes = LoadMeshes();
            Material body = FlatMaterial("MT_Firefly_Base", "T_Firefly_Base");
            Material wheel = FlatMaterial("MT_Firefly_Wheel", "T_Firefly_Wheel");
            Material belt = FlatMaterial("MT_Firefly_TrackBelt", "T_Firefly_TrackBelt");

            GameObject root = PrefabUtility.LoadPrefabContents(TANK);
            try
            {
                Transform hull = root.transform.Find("HullVisual");
                Transform turret = hull.Find("TurretVisual");
                Transform gun = turret.Find("Gun");

                // ---------------------------------------------------------------- strip the old look
                for (int i = hull.childCount - 1; i >= 0; i--)
                {
                    Transform c = hull.GetChild(i);
                    if (!Keeps(c.name))
                        Object.DestroyImmediate(c.gameObject);
                }

                foreach (Transform w in hull)
                    if (w.name.StartsWith("Wheel_") || w.name.StartsWith("TrackL_Front") || w.name.StartsWith("TrackR_Front") ||
                        w.name.StartsWith("TrackL_Rear") || w.name.StartsWith("TrackR_Rear"))
                        ClearChildren(w);

                for (int i = turret.childCount - 1; i >= 0; i--)
                {
                    Transform c = turret.GetChild(i);
                    if (c.name == "Gun" || c.name.StartsWith("MachineGun"))
                        continue;
                    Object.DestroyImmediate(c.gameObject);
                }

                ClearChildren(gun);

                // ---------------------------------------------------------------- hull, belts, running gear
                Place(hull, "Body", meshes["SD_Firefly_Body"], body, Vector3.zero, Quaternion.identity);
                Place(hull, "TrackL_Belt", meshes["SD_Firefly_TrackBelt_L"], belt, Vector3.zero, Quaternion.identity);
                Place(hull, "TrackR_Belt", meshes["SD_Firefly_TrackBelt_R"], belt, Vector3.zero, Quaternion.identity);

                for (int s = -1; s <= 1; s += 2)
                {
                    string side = s < 0 ? "L" : "R";
                    Quaternion axle = Quaternion.Euler(0f, 0f, s < 0 ? 90f : 270f);

                    foreach (Vector3 at in SupportWheels)
                        Child(hull, $"SupportWheel{side}_{(at.z > 0 ? 1 : 2)}", meshes["SD_Firefly_SupportWheel"], wheel,
                            Model(new Vector3(s * at.x, at.y, at.z)), axle, SCALE);

                    // Suspension arms, as the pack lays them (parent turned 90° about z, the arm 352°/8° about y then 270° about z).
                    Quaternion parent = Quaternion.Euler(0f, 0f, 90f);
                    Vector3 local = new(0f, -s * 1.065f, 0f);
                    Matrix4x4 Arm(Vector3 parentAt, Vector3 offset, Vector3 euler) =>
                        Matrix4x4.TRS(parentAt, parent, Vector3.one) * Matrix4x4.TRS(local + offset, Quaternion.Euler(euler), Vector3.one);
                    PlaceModel(hull, $"ArmFront{side}_1", meshes[$"SD_Firefly_Sus_Front_{side}"], wheel, Arm(new Vector3(0f, -0.66f, 1.0f), Vector3.zero, new Vector3(0f, 352f, 270f)));
                    PlaceModel(hull, $"ArmFront{side}_2", meshes[$"SD_Firefly_Sus_Front_{side}"], wheel, Arm(new Vector3(0f, -0.66f, 1.0f), new Vector3(0f, 0f, -1.8f), new Vector3(0f, 352f, 270f)));
                    PlaceModel(hull, $"ArmRear{side}_1", meshes[$"SD_Firefly_Sus_Rear_{side}"], wheel, Arm(new Vector3(0f, -0.66f, 0.8f), Vector3.zero, new Vector3(0f, 8f, 270f)));
                    PlaceModel(hull, $"ArmRear{side}_2", meshes[$"SD_Firefly_Sus_Rear_{side}"], wheel, Arm(new Vector3(0f, -0.66f, 0.8f), new Vector3(0f, 0f, -1.8f), new Vector3(0f, 8f, 270f)));
                    PlaceModel(hull, $"IdlerArm{side}", meshes[$"SD_Firefly_Idler_Arm_{side}"], wheel,
                        Matrix4x4.TRS(new Vector3(0f, -0.4f, -2.086f), parent, Vector3.one) *
                        Matrix4x4.TRS(new Vector3(0f, -s * 0.92f, 0f), Quaternion.Euler(0f, 270f, 270f), Vector3.one));

                    // Road wheels on the wheel visuals (the suspension moves and spins those; the mesh carries the axle turn).
                    foreach (string n in WheelsWithMesh)
                    {
                        Transform w = hull.Find($"Wheel_{side}{n}");
                        if (w != null)
                            Child(w, "RoadWheel", meshes["SD_Firefly_RoadWheel"], wheel, Vector3.zero, axle, SCALE);
                    }

                    // Sprocket at the front, idler at the rear: the track rig's ends move to them.
                    Transform front = hull.Find($"Track{side}_Front");
                    Transform rear = hull.Find($"Track{side}_Rear");
                    front.localPosition = Model(new Vector3(s * Sprocket.x, Sprocket.y, Sprocket.z));
                    rear.localPosition = Model(new Vector3(s * Idler.x, Idler.y, Idler.z));
                    Child(front, "Sprocket", meshes["SD_Firefly_Sprocket"], wheel, Vector3.zero, axle, SCALE);
                    Child(rear, "Idler", meshes["SD_Firefly_IdlerWheel"], wheel, Vector3.zero, axle, SCALE);
                }

                // The belts draw the whole track now; the lower-run links still move (TankTrackView) but are not shown.
                foreach (Transform t in hull)
                    if (t.name.StartsWith("TrackL_Link") || t.name.StartsWith("TrackR_Link"))
                        foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
                            r.enabled = false;

                // ---------------------------------------------------------------- turret and gun
                float oldTurretY = turret.localPosition.y;
                Vector3 turretAt = Model(TurretPivot);
                turret.localPosition = turretAt;
                float lift = turretAt.y - oldTurretY;
                // The Firefly turret is taller than the old one: its roof is this much higher over the pivot.
                float roofLift = (Model(new Vector3(0f, 1.88f, 0f)).y - turretAt.y) - 0.57f;

                Place(turret, "Turret", meshes["SD_Firefly_Turret"], body, -turretAt, Quaternion.identity);
                gun.localPosition = Vector3.zero;
                Place(gun, "Cannon", meshes["SD_Firefly_Cannon"], body, -turretAt, Quaternion.identity);
                // The pack's barrel is a hollow tube; voxelized it comes out as loose strips. A solid cylinder
                // from the mantlet to the muzzle instead (round parts are cylinders in our voxel style).
                Vector3 breech = Model(new Vector3(0f, BARREL_Y, BARREL_START_Z)) - turretAt;
                Vector3 muzzle = Model(new Vector3(0f, BARREL_Y, BARREL_TIP_Z)) - turretAt;
                GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                barrel.name = "Barrel";
                Object.DestroyImmediate(barrel.GetComponent<Collider>());
                barrel.layer = gun.gameObject.layer;
                barrel.transform.SetParent(gun, false);
                barrel.transform.SetLocalPositionAndRotation((breech + muzzle) * 0.5f, Quaternion.Euler(90f, 0f, 0f));
                barrel.transform.localScale = new Vector3(BARREL_DIAMETER, (muzzle - breech).magnitude * 0.5f, BARREL_DIAMETER);
                barrel.GetComponent<MeshRenderer>().sharedMaterial = body;

                // Machine gun: the entity's fire point and the old small gun's meshes go up onto the new roof
                // (only on the first run: a rerun finds the turret already at the Firefly's height).
                bool firstRun = lift > 0.01f;
                if (!firstRun)
                    roofLift = 0f;
                foreach (Transform t in turret)
                    if (t.name.StartsWith("MachineGun"))
                    {
                        if (t.GetComponent<MeshRenderer>() != null)
                            t.localPosition += Vector3.up * roofLift;
                        Transform fp = t.Find("FirePoint");
                        if (fp != null)
                            fp.localPosition += Vector3.up * roofLift;
                    }

                // The cannon's fire point to the Firefly's muzzle, the casing point up with the turret.
                Transform cannon = root.transform.Find("TurretPivot/Cannon");
                Transform firePoint = cannon.Find("FirePoint");
                firePoint.localPosition = Model(new Vector3(0f, BARREL_Y, BARREL_TIP_Z));
                Transform casing = cannon.Find("CasingPoint");
                if (casing != null)
                    casing.localPosition += Vector3.up * lift;

                // ---------------------------------------------------------------- exhaust onto the Firefly's rear plate
                foreach (Transform t in hull)
                {
                    if (!t.name.StartsWith("Exhaust"))
                        continue;
                    float x = Mathf.Sign(t.localPosition.x) * 0.41f;
                    t.localPosition = new Vector3(x, Model(new Vector3(0f, 0.25f, 0f)).y, Model(new Vector3(0f, 0f, -2.62f)).z - (t.GetComponent<ParticleSystem>() != null ? 0.06f : 0f));
                }

                // ---------------------------------------------------------------- voxel skin: no Tiger camouflage or decals on the Firefly
                VoxelizedModel skin = root.GetComponent<VoxelizedModel>();
                var so = new SerializedObject(skin);
                float voxel = so.FindProperty("_voxelSize").floatValue;
                float noise = so.FindProperty("_colorNoise").floatValue;
                skin.Configure(new[] { "HullVisual/Track?_Link*", "HullVisual/TurretVisual/MachineGun*" }, voxel, noise,
                    default, System.Array.Empty<VoxelizedModel.Decal>());

                PrefabUtility.SaveAsPrefabAsset(root, TANK);
                Debug.Log("[TankFireflyBuilder] Tank_01 now wears the Firefly.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool Keeps(string name)
        {
            foreach (string k in KeepUnderHull)
                if (name.StartsWith(k))
                    return true;
            return false;
        }

/// <summary>Everything under a purely visual transform (wheel, sprocket, gun) goes.</summary>
        private static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(t.GetChild(i).gameObject);
        }

        /// <summary>A mesh modelled in Firefly model space, placed so its model points land where <see cref="Model"/> puts them.</summary>
        private static void Place(Transform parent, string name, Mesh mesh, Material material, Vector3 shift, Quaternion rotation) =>
            Child(parent, name, mesh, material, Offset + shift, rotation, SCALE);

        /// <summary>A mesh with its own pivot, at model-space transform <paramref name="m"/>.</summary>
        private static void PlaceModel(Transform parent, string name, Mesh mesh, Material material, Matrix4x4 m) =>
            Child(parent, name, mesh, material, Model(m.GetColumn(3)), m.rotation, SCALE);

        private static void Child(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Quaternion rotation, float scale)
        {
            var go = new GameObject(name) { layer = parent.gameObject.layer };
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(position, rotation);
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Dictionary<string, Mesh> LoadMeshes()
        {
            var meshes = new Dictionary<string, Mesh>();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(FBX))
                if (o is Mesh m)
                    meshes[m.name] = m;
            return meshes;
        }

        /// <summary>
        /// The pack's material on URP Lit, in one flat colour: the texture's average. VoxelizedModel paints each voxel
        /// in its material's colour (it does not read textures), and the meshes themselves are hidden under the voxels.
        /// </summary>
        private static Material FlatMaterial(string material, string texture)
        {
            string path = ART + "Materials/" + material + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ART + "Materials/" + texture + ".png");
            Color average = Average(tex);
            m.shader = Shader.Find("Universal Render Pipeline/Lit");
            m.SetTexture("_BaseMap", null);
            m.SetColor("_BaseColor", average);
            if (m.HasProperty("_Color"))
                m.SetColor("_Color", average);
            m.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssetIfDirty(m);
            return m;
        }

        private static Color Average(Texture2D texture)
        {
            if (texture == null)
                return Color.gray;
            RenderTexture rt = RenderTexture.GetTemporary(64, 64, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(texture, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var small = new Texture2D(64, 64, TextureFormat.RGBA32, false, false);
            small.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
            small.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            // The atlas has black gaps between its islands: the paint is the most common bright colour, not the mean.
            var bins = new Dictionary<int, (int n, Color sum)>();
            Color all = Color.clear;
            int count = 0;
            foreach (Color c in small.GetPixels())
            {
                all += c;
                count++;
                if (c.a < 0.5f || Mathf.Max(c.r, Mathf.Max(c.g, c.b)) < MIN_PAINT)
                    continue;
                int key = (int)(c.r * 15.99f) << 8 | (int)(c.g * 15.99f) << 4 | (int)(c.b * 15.99f);
                bins.TryGetValue(key, out var e);
                bins[key] = (e.n + 1, e.sum + c);
            }

            Object.DestroyImmediate(small);
            (int n, Color sum) best = (0, Color.gray);
            foreach (var e in bins.Values)
                if (e.n > best.n)
                    best = e;
            // A texture with no bright paint at all (the track belt) keeps its own mean.
            Color dominant = best.n > 0 ? best.sum / best.n : count > 0 ? all / count : Color.gray;
            dominant.a = 1f;
            return dominant;
        }
    }
}
