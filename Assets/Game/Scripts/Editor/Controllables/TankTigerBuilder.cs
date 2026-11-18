using System.Collections.Generic;
using Game.Voxels;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Draft: dresses Tank_01 in the Kawaii Tanks "Tiger I" (Assets/Game/Art/Tanks/TigerI). Every object the game
    /// drives stays where it is — installers, the turret pivot and its fire points, the machine gun, the suspension
    /// mounts and wheel visuals, the track rig and its links, the exhaust, the hull colliders — only the meshes under
    /// HullVisual change. VoxelizedModel then turns them into voxels at start, part by part as before.
    /// </summary>
    /// <remarks>
    /// The pack's meshes are all modelled in one model space (its prefab only adds pivots). A model point p goes to
    /// <c>SCALE · p + OFFSET</c> in HullVisual space: at 0.543 the road wheels sit exactly on our wheel track
    /// (x ±0.788) and their centres on our wheel visuals (y 0.225); the tank comes to about 2.0 × 1.6 × 3.7 m.
    /// Every position is set absolutely, so the builder can be run again on its own result.
    /// </remarks>
    public static class TankTigerBuilder
    {
        private const string TANK = "Assets/Game/Prefabs/Vehicles/Tank_01.prefab";
        private const string ART = "Assets/Game/Art/Tanks/TigerI/";
        private const string FBX = ART + "SM_TigerI.fbx";
        private const string MESH = "SD_Tiger-I_";

        private const float SCALE = 0.543f;
        private static readonly Vector3 Offset = new(0f, 0.651f, 0f);

        // Pack pivots, model space (from its SD_Tiger-I_2.0 prefab).
        private static readonly Vector3 TurretPivot = new(0f, 0.55f, 0f);
        private const float ROOF_Y = 1.695f;
        private static readonly Vector3 Sprocket = new(1.45f, -0.47f, 2.05f);
        private static readonly Vector3 Idler = new(1.45f, -0.68f, -1.975f);
        private const float BARREL_Y = 0.96f;
        private const float BARREL_START_Z = 2.2f;
        private const float BARREL_TIP_Z = 4.33f;
        private const float REAR_Z = -2.747f;
        private const float EXHAUST_Y = 0.45f;

        /// <summary>Sprocket (teeth) and idler radii, model space: they turn as fast as the track runs.</summary>
        private const float SPROCKET_RADIUS = 0.427f;
        private const float IDLER_RADIUS = 0.35f;
        private const float ROAD_WHEEL_RADIUS = 0.42f;

        /// <summary>Links a side, and one link's size (m): the pack's track width, a 5 cm plate, gaps between links.</summary>
        private const int TREADS = 64;
        private static readonly Vector3 TreadSize = new(0.41f, 0.05f, 0.075f);

        /// <summary>Barrel diameter in HullVisual metres: six 3 cm voxels.</summary>
        private const float BARREL_DIAMETER = 0.18f;

        /// <summary>Brightest channel below this is the atlas's dark background, not paint.</summary>
        private const float MIN_PAINT = 0.25f;

        /// <summary>
        /// The old turret's machine gun, as heights under the roof (relative to the turret pivot) — kept so the gun
        /// stands on the new roof the way it stood on the old one.
        /// </summary>
        private static readonly Dictionary<string, float> UnderRoof = new()
        {
            { "FirePoint", -0.075f }, { "MachineGunPost", -0.135f }, { "MachineGunBody", -0.09f }, { "MachineGunBarrel", -0.075f }
        };

        /// <summary>The casing point over the turret pivot (as on the old tank: 1.35 − 0.84).</summary>
        private const float CASING_OVER_TURRET = 0.51f;

        /// <summary>HullVisual children that stay: what the game drives or reads by name.</summary>
        private static readonly string[] KeepUnderHull =
            { "TurretVisual", "Wheel_", "TrackL_Front", "TrackR_Front", "TrackL_Rear", "TrackR_Rear", "TrackL_Link", "TrackR_Link", "ExhaustPipe", "ExhaustLeft", "ExhaustRight" };

        private static Vector3 Model(Vector3 p) => p * SCALE + Offset;

        [MenuItem("Tools/Tank/Use Tiger I Model")]
        public static void Build()
        {
            var meshes = new Dictionary<string, Mesh>();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(FBX))
                if (o is Mesh m)
                    meshes[m.name.Replace(MESH, "")] = m;

            Material body = FlatMaterial("MT_TigerI_Base", "T_TigerI_Base");
            Material wheel = FlatMaterial("MT_TigerI_Wheel", "T_TigerI_Wheel");
            Material belt = FlatMaterial("MT_TigerI_TrackBelt", "T_TigerI_TrackBelt");

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

                foreach (Transform t in hull)
                    if (t.name.StartsWith("Wheel_") || t.name.EndsWith("_Front") || t.name.EndsWith("_Rear"))
                        ClearChildren(t);

                for (int i = turret.childCount - 1; i >= 0; i--)
                {
                    Transform c = turret.GetChild(i);
                    if (c.name != "Gun" && !c.name.StartsWith("MachineGun"))
                        Object.DestroyImmediate(c.gameObject);
                }

                ClearChildren(gun);

                // ---------------------------------------------------------------- hull, belts, running gear
                Place(hull, "Body", meshes["Body"], body, Vector3.zero);

                // The tracks: a loop of plain links a side that TankTrackView runs round the sprocket, the top,
                // the idler and under the wheels (the pack's belt is one still mesh and could not move).
                var installer = new SerializedObject(root.GetComponent<TankInstaller>());
                for (int s = -1; s <= 1; s += 2)
                {
                    string side = s < 0 ? "L" : "R";
                    var treads = new Transform[TREADS];
                    for (int i = 0; i < TREADS; i++)
                    {
                        GameObject link = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        link.name = $"Track{side}_Tread{i:00}";
                        Object.DestroyImmediate(link.GetComponent<Collider>());
                        link.layer = hull.gameObject.layer;
                        link.transform.SetParent(hull, false);
                        link.transform.localPosition = Model(new Vector3(s * Sprocket.x, Sprocket.y, Sprocket.z));
                        link.transform.localScale = TreadSize;
                        link.GetComponent<MeshRenderer>().sharedMaterial = belt;
                        treads[i] = link.transform;
                    }

                    LayAtRest(hull, side, treads);

                    SerializedProperty rig = installer.FindProperty(s < 0 ? "_leftTrack" : "_rightTrack");
                    rig.FindPropertyRelative("FrontRadius").floatValue = SPROCKET_RADIUS * SCALE;
                    rig.FindPropertyRelative("RearRadius").floatValue = IDLER_RADIUS * SCALE;
                    SerializedProperty list = rig.FindPropertyRelative("Treads");
                    list.arraySize = TREADS;
                    for (int i = 0; i < TREADS; i++)
                        list.GetArrayElementAtIndex(i).objectReferenceValue = treads[i];
                }

                installer.ApplyModifiedPropertiesWithoutUndo();

                for (int s = -1; s <= 1; s += 2)
                {
                    string side = s < 0 ? "L" : "R";
                    Quaternion axle = Quaternion.Euler(0f, 0f, s < 0 ? 90f : 270f);
                    Quaternion parent = Quaternion.Euler(0f, 0f, 90f);

                    // Suspension arms and idler arm, as the pack lays them.
                    Matrix4x4 Arm(Vector3 parentAt, float along, float spread, Vector3 euler) =>
                        Matrix4x4.TRS(parentAt, parent, Vector3.one) *
                        Matrix4x4.TRS(new Vector3(0f, -s * spread, along), Quaternion.Euler(euler), Vector3.one);
                    for (int k = 0; k < 3; k++)
                    {
                        PlaceModel(hull, $"ArmIn{side}_{k + 1}", meshes[$"Sus_{side}"], wheel, Arm(new Vector3(0f, -0.66f, 1.175f), -1.05f * k, 1.0f, new Vector3(0f, 18f, 270f)));
                        PlaceModel(hull, $"ArmOut{side}_{k + 1}", meshes[$"Sus_{side}"], wheel, Arm(new Vector3(0f, -0.66f, 1.7f), -1.05f * k, 1.0f, new Vector3(0f, 18f, 270f)));
                    }

                    PlaceModel(hull, $"IdlerArm{side}", meshes[$"Idler_Arm_{side}"], wheel, Arm(new Vector3(0f, -0.55f, -1.9f), 0f, 1.1f, new Vector3(0f, 60f, 270f)));

                    // Interleaved road wheels on our eight wheel visuals (the suspension moves and spins those; the
                    // mesh carries the axle turn): outer row on the odd ones, inner row on the even ones.
                    for (int n = 1; n <= 8; n++)
                    {
                        Transform w = hull.Find($"Wheel_{side}{n}");
                        if (w != null)
                            Child(w, "RoadWheel", meshes[n % 2 == 1 ? "RoadWheel_Out" : "RoadWheel_In"], wheel, Vector3.zero, axle, SCALE);
                    }

                    // Sprocket at the front, idler at the rear: the track rig's ends move to them.
                    Transform front = hull.Find($"Track{side}_Front");
                    Transform rear = hull.Find($"Track{side}_Rear");
                    front.localPosition = Model(new Vector3(s * Sprocket.x, Sprocket.y, Sprocket.z));
                    rear.localPosition = Model(new Vector3(s * Idler.x, Idler.y, Idler.z));
                    Child(front, "Sprocket", meshes["SprocketWheel"], wheel, Vector3.zero, axle, SCALE);
                    Child(rear, "Idler", meshes["IdlerWheel"], wheel, Vector3.zero, axle, SCALE);
                }

                // The treads draw the whole track; the lower-run links still move (TankTrackView) but are not shown.
                foreach (Transform t in hull)
                    if (t.name.StartsWith("TrackL_Link") || t.name.StartsWith("TrackR_Link"))
                        foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
                            r.enabled = false;

                // ---------------------------------------------------------------- turret and gun
                Vector3 turretAt = Model(TurretPivot);
                turret.localPosition = turretAt;
                float roof = Model(new Vector3(0f, ROOF_Y, 0f)).y - turretAt.y;

                Place(turret, "Turret", meshes["Turret"], body, -turretAt);
                gun.localPosition = Vector3.zero;
                Place(gun, "Cannon", meshes["Cannon"], body, -turretAt);

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

                // Machine gun: the entity's fire point and the old small gun's meshes stand on the new roof.
                foreach (Transform t in turret)
                {
                    if (!t.name.StartsWith("MachineGun"))
                        continue;
                    if (UnderRoof.TryGetValue(t.name, out float under))
                        t.localPosition = new Vector3(t.localPosition.x, roof + under, t.localPosition.z);
                    Transform fp = t.Find("FirePoint");
                    if (fp != null)
                        fp.localPosition = new Vector3(fp.localPosition.x, roof + UnderRoof["FirePoint"], fp.localPosition.z);
                }

                // The cannon's fire point to the muzzle, the casing point over the turret.
                Transform cannon = root.transform.Find("TurretPivot/Cannon");
                cannon.Find("FirePoint").localPosition = Model(new Vector3(0f, BARREL_Y, BARREL_TIP_Z));
                Transform casing = cannon.Find("CasingPoint");
                if (casing != null)
                    casing.localPosition = new Vector3(casing.localPosition.x, turretAt.y + CASING_OVER_TURRET, casing.localPosition.z);

                // ---------------------------------------------------------------- exhaust on the rear plate
                foreach (Transform t in hull)
                {
                    if (!t.name.StartsWith("Exhaust"))
                        continue;
                    float x = Mathf.Sign(t.localPosition.x) * 0.41f;
                    float z = Model(new Vector3(0f, 0f, REAR_Z)).z - (t.GetComponent<ParticleSystem>() != null ? 0.06f : 0f);
                    t.localPosition = new Vector3(x, Model(new Vector3(0f, EXHAUST_Y, 0f)).y, z);
                }

                // ---------------------------------------------------------------- voxel skin: plain paint for now
                VoxelizedModel skin = root.GetComponent<VoxelizedModel>();
                var so = new SerializedObject(skin);
                skin.Configure(new[] { "HullVisual/Track?_Link*", "HullVisual/Track?_Tread*", "HullVisual/TurretVisual/MachineGun*" },
                    so.FindProperty("_voxelSize").floatValue, so.FindProperty("_colorNoise").floatValue,
                    default, System.Array.Empty<VoxelizedModel.Decal>());

                PrefabUtility.SaveAsPrefabAsset(root, TANK);
                Debug.Log("[TankTigerBuilder] Tank_01 now wears the Kawaii Tiger I.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// The links laid round the loop as the tank stands (TankTrackView relays them every frame in the game; a
        /// copy without it — the target dummy — keeps this): sprocket top, back over the top, round the idler, under
        /// the wheels where they rest, round the sprocket.
        /// </summary>
        private static void LayAtRest(Transform hull, string side, Transform[] treads)
        {
            int s = side == "L" ? -1 : 1;
            float half = TreadSize.y * 0.5f;
            Vector3 front = Model(new Vector3(s * Sprocket.x, Sprocket.y, Sprocket.z));
            Vector3 rear = Model(new Vector3(s * Idler.x, Idler.y, Idler.z));
            float frontR = SPROCKET_RADIUS * SCALE + half;
            float rearR = IDLER_RADIUS * SCALE + half;
            const int arc = 8;
            var points = new List<Vector3> { front + Vector3.up * frontR };
            for (int k = 0; k <= arc; k++)
            {
                float a = Mathf.PI * k / arc;
                points.Add(rear + new Vector3(0f, Mathf.Cos(a), -Mathf.Sin(a)) * rearR);
            }

            var wheels = new List<Transform>();
            foreach (Transform t in hull)
                if (t.name.StartsWith($"Wheel_{side}"))
                    wheels.Add(t);
            wheels.Sort((a, b) => a.localPosition.z.CompareTo(b.localPosition.z));
            foreach (Transform w in wheels)
                points.Add(w.localPosition + Vector3.down * (ROAD_WHEEL_RADIUS * SCALE - half));
            for (int k = 0; k < arc; k++)
            {
                float a = Mathf.PI * k / arc;
                points.Add(front + new Vector3(0f, -Mathf.Cos(a), Mathf.Sin(a)) * frontR);
            }

            float length = 0f;
            var along = new float[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                along[i] = length;
                length += Vector3.Distance(points[i], points[(i + 1) % points.Count]);
            }

            for (int i = 0; i < treads.Length; i++)
            {
                float d = length * i / treads.Length;
                int seg = points.Count - 1;
                for (int k = 0; k < points.Count - 1; k++)
                    if (d < along[k + 1]) { seg = k; break; }
                Vector3 a = points[seg], b = points[(seg + 1) % points.Count];
                float span = (seg + 1 < points.Count ? along[seg + 1] : length) - along[seg];
                Vector3 tangent = (b - a).normalized;
                treads[i].SetLocalPositionAndRotation(Vector3.Lerp(a, b, span > 1e-5f ? (d - along[seg]) / span : 0f),
                    Quaternion.LookRotation(tangent, Vector3.Cross(Vector3.right, tangent)));
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

        /// <summary>A mesh modelled in pack model space, placed so its points land where <see cref="Model"/> puts them.</summary>
        private static void Place(Transform parent, string name, Mesh mesh, Material material, Vector3 shift) =>
            Child(parent, name, mesh, material, Offset + shift, Quaternion.identity, SCALE);

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

        /// <summary>
        /// The pack's material on URP Lit, in one flat colour: the texture's paint. VoxelizedModel paints each voxel
        /// in its material's colour (it does not read textures), and the meshes themselves are hidden under the voxels.
        /// </summary>
        private static Material FlatMaterial(string material, string texture)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(ART + "Materials/" + material + ".mat");
            Color paint = Paint(AssetDatabase.LoadAssetAtPath<Texture2D>(ART + "Materials/" + texture + ".png"));
            m.shader = Shader.Find("Universal Render Pipeline/Lit");
            m.SetTexture("_BaseMap", null);
            m.SetColor("_BaseColor", paint);
            if (m.HasProperty("_Color"))
                m.SetColor("_Color", paint);
            m.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssetIfDirty(m);
            return m;
        }

        /// <summary>The most common bright colour of the texture (the atlas has dark gaps); a dark texture keeps its mean.</summary>
        private static Color Paint(Texture2D texture)
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
            Color paint = best.n > 0 ? best.sum / best.n : count > 0 ? all / count : Color.gray;
            paint.a = 1f;
            return paint;
        }
    }
}
