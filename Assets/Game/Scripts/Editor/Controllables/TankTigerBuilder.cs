using System.Collections.Generic;
using Game.Voxels;
using Game.Voxels.EditorTools;
using Unity.Mathematics;
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

        /// <summary>Edge of a voxel: Tank_01's VoxelizedModel voxel (3 cm).</summary>
        private const float VOXEL = 0.03f;

        /// <summary>
        /// Hull and turret are pre-voxelized at twice the voxel (each cell a 2×2×2 block of voxels): fewer, plainer
        /// steps on the curves and no detail under 12 cm.
        /// </summary>
        private const float COARSE = 0.06f;

        /// <summary>Inner edge of the tracks (HullVisual x): the hull beyond it is the fender/sponson, made straight.</summary>
        private const float TRACK_INNER_X = 0.58f;

        /// <summary>Muzzle brake (HullVisual metres): two baffles on the barrel end, the front one at the muzzle.</summary>
        private static readonly Vector3 BrakeBaffle = new(0.3f, 0.24f, 0.09f);
        private const float BRAKE_LENGTH = 0.24f;

        /// <summary>The old sand paint (MT_Tank_Body), under the camouflage.</summary>
        private const string SAND = "Assets/Game/Materials/MT_Tank_Body.mat";
        private static readonly Vector3 Offset = new(0f, 0.651f, 0f);

        // Pack pivots, model space (from its SD_Tiger-I_2.0 prefab).
        private static readonly Vector3 TurretPivot = new(0f, 0.55f, 0f);
        private const float ROOF_Y = 1.695f;
        private static readonly Vector3 Sprocket = new(1.45f, -0.47f, 2.05f);
        private static readonly Vector3 Idler = new(1.45f, -0.68f, -1.975f);
        private const float BARREL_Y = 0.96f;
        private const float BARREL_START_Z = 2.2f;
        /// <summary>The muzzle: the pack's barrel (start to 4.33) made 25% longer.</summary>
        private const float BARREL_TIP_Z = BARREL_START_Z + (4.33f - BARREL_START_Z) * 1.25f;

        /// <summary>The gun pitches about its trunnion: barrel height, at the back of the mantlet (model space).</summary>
        private const float TRUNNION_Z = 1.75f;
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
            Material sand = AssetDatabase.LoadAssetAtPath<Material>(SAND);
            if (sand != null)
            {
                body.SetColor("_BaseColor", sand.color);
                if (body.HasProperty("_Color"))
                    body.SetColor("_Color", sand.color);
                EditorUtility.SetDirty(body);
                AssetDatabase.SaveAssetIfDirty(body);
            }
            Material wheel = FlatMaterial("MT_TigerI_Wheel", "T_TigerI_Wheel");
            if (sand != null)
                Tint(wheel, sand.color);
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
                Blocks(hull, Place(hull, "Body", meshes["Body"], body, Vector3.zero), "Body", COARSE, true);

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

                Blocks(turret, Place(turret, "Turret", meshes["Turret"], body, -turretAt), "Turret", COARSE, false);

                // The gun group turns about the trunnion (TankGunSwayBehaviour pitches it), so its pivot is there.
                Vector3 gunAt = Model(new Vector3(0f, BARREL_Y, TRUNNION_Z)) - turretAt;
                gun.SetLocalPositionAndRotation(gunAt, Quaternion.identity);
                Blocks(gun, Place(gun, "Cannon", meshes["Cannon"], body, -turretAt - gunAt), "Cannon", VOXEL, false);

                // The pack's barrel is a hollow tube; voxelized it comes out as loose strips. A solid cylinder
                // from the mantlet to the muzzle instead (round parts are cylinders in our voxel style).
                Vector3 breech = Model(new Vector3(0f, BARREL_Y, BARREL_START_Z)) - turretAt - gunAt;
                Vector3 muzzle = Model(new Vector3(0f, BARREL_Y, BARREL_TIP_Z)) - turretAt - gunAt + Vector3.forward * BRAKE_LENGTH;
                GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                barrel.name = "Barrel";
                Object.DestroyImmediate(barrel.GetComponent<Collider>());
                barrel.layer = gun.gameObject.layer;
                barrel.transform.SetParent(gun, false);
                barrel.transform.SetLocalPositionAndRotation((breech + muzzle) * 0.5f, Quaternion.Euler(90f, 0f, 0f));
                barrel.transform.localScale = new Vector3(BARREL_DIAMETER, (muzzle - breech).magnitude * 0.5f, BARREL_DIAMETER);
                barrel.GetComponent<MeshRenderer>().sharedMaterial = body;

                // Muzzle brake: two square baffles round the barrel end, a gap between them.
                for (int k = 0; k < 2; k++)
                {
                    GameObject baffle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    baffle.name = k == 0 ? "BrakeFront" : "BrakeRear";
                    Object.DestroyImmediate(baffle.GetComponent<Collider>());
                    baffle.layer = gun.gameObject.layer;
                    baffle.transform.SetParent(gun, false);
                    float back = k == 0 ? BrakeBaffle.z * 0.5f : BRAKE_LENGTH - BrakeBaffle.z * 0.5f;
                    baffle.transform.localPosition = muzzle - Vector3.forward * back;
                    baffle.transform.localScale = BrakeBaffle;
                    baffle.GetComponent<MeshRenderer>().sharedMaterial = body;
                }

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
                cannon.Find("FirePoint").localPosition = Model(new Vector3(0f, BARREL_Y, BARREL_TIP_Z)) + Vector3.forward * BRAKE_LENGTH;
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

                // ---------------------------------------------------------------- voxel skin: the old paint
                // Sand with grey patches, the number and the cross on both turret sides — as the tank wore it.
                VoxelizedModel skin = root.GetComponent<VoxelizedModel>();
                var so = new SerializedObject(skin);
                var camouflage = new VoxelizedModel.Camouflage
                {
                    Paint = body,
                    Patch = new Color(0.46f, 0.45f, 0.43f),
                    Dark = new Color(0.36f, 0.355f, 0.345f),
                    Light = new Color(0.8f, 0.66f, 0.42f),
                    Frequency = 1.5f,
                    Threshold = 0.2f
                };
                skin.Configure(new[] { "HullVisual/Track?_Link*", "HullVisual/Track?_Tread*", "HullVisual/TurretVisual/MachineGun*" },
                    VOXEL, so.FindProperty("_colorNoise").floatValue, camouflage, Decals());

                PrefabUtility.SaveAsPrefabAsset(root, TANK);
                Debug.Log("[TankTigerBuilder] Tank_01 now wears the Kawaii Tiger I.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>The number «131» and the cross, white on black, on each turret side (as on the tank before).</summary>
        private static VoxelizedModel.Decal[] Decals()
        {
            string[] number = { ".#.####..#", "##....#.##", ".#....#..#", ".#..###..#", ".#....#..#", ".#....#..#", ".#.####..#" };
            string[] cross = { "...WKKW...", "...WKKW...", "...WKKW...", "WWWWKKWWWW", "KKKKKKKKKK", "KKKKKKKKKK", "WWWWKKWWWW", "...WKKW...", "...WKKW...", "...WKKW..." };
            var white = new Color(0.93f, 0.93f, 0.9f);
            var black = new Color(0.05f, 0.05f, 0.055f);
            var decals = new List<VoxelizedModel.Decal>();
            for (int side = -1; side <= 1; side += 2)
            {
                decals.Add(new VoxelizedModel.Decal { Part = "HullVisual/TurretVisual", Side = side, Center = new Vector2(-0.06f, 0.225f), Rows = number, Keys = "#", Colors = new[] { white } });
                decals.Add(new VoxelizedModel.Decal { Part = "HullVisual/TurretVisual", Side = side, Center = new Vector2(-0.42f, 0.24f), Rows = cross, Keys = "WK", Colors = new[] { white, black } });
            }

            return decals.ToArray();
        }

        /// <summary>
        /// Swaps a pack mesh for its voxels as a closed block mesh, in the part's own space: the new voxelizer
        /// (straight faces on the grid, fill from outside, details under two voxels cut) gives the shape, and
        /// VoxelizedModel, filling closed meshes by parity at the same voxel size, gets exactly those voxels in
        /// the game — no holes where the pack's mesh is open, no specks of small detail.
        /// </summary>
        private static void Blocks(Transform part, Transform child, string name, float cellSize, bool straightSides)
        {
            MeshRenderer renderer = child.GetComponent<MeshRenderer>();
            using VoxelizerTriangles triangles = VoxelizerSource.Collect(part, new List<Renderer> { renderer });
            VoxelizerStraighten.SnapPlanes(triangles, cellSize);
            using VoxelizerPartGrid grid = VoxelizerSurface.Sample(triangles, cellSize, name);
            VoxelizerSurface.Fill(grid);
            if (straightSides)
            {
                StraightSide(grid, -1);
                StraightSide(grid, 1);
            }
            VoxelizerStraighten.Open(grid, 2);

            var vertices = new List<Vector3>();
            var indices = new List<int>();
            int3 size = grid.Size;
            int3[] dirs = { new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0), new(0, 0, 1), new(0, 0, -1) };
            bool Solid(int3 c) => math.all(c >= 0) && math.all(c < size) && grid.Cells[c.x + size.x * (c.y + size.y * c.z)].a != 0;
            for (int z = 0; z < size.z; z++)
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
            {
                var cell = new int3(x, y, z);
                if (!Solid(cell))
                    continue;
                foreach (int3 d in dirs)
                {
                    if (Solid(cell + d))
                        continue;
                    Vector3 n = new(d.x, d.y, d.z);
                    Vector3 a = d.x != 0 ? Vector3.up : Vector3.right;
                    Vector3 b = Vector3.Cross(n, a);
                    Vector3 c = (Vector3)(float3)grid.Origin + (new Vector3(x, y, z) + Vector3.one * 0.5f + n * 0.5f) * cellSize;
                    int s = vertices.Count;
                    vertices.Add(c + (-a - b) * 0.5f * cellSize);
                    vertices.Add(c + (a - b) * 0.5f * cellSize);
                    vertices.Add(c + (a + b) * 0.5f * cellSize);
                    vertices.Add(c + (-a + b) * 0.5f * cellSize);
                    indices.Add(s); indices.Add(s + 1); indices.Add(s + 2);
                    indices.Add(s); indices.Add(s + 2); indices.Add(s + 3);
                }
            }

            string path = ART + "Blocks/SM_TigerI_" + name + "_Blocks.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = mesh == null;
            if (created)
                mesh = new Mesh { name = "SM_TigerI_" + name + "_Blocks" };
            mesh.Clear();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (created)
            {
                if (!AssetDatabase.IsValidFolder(ART + "Blocks"))
                    AssetDatabase.CreateFolder(ART.TrimEnd('/'), "Blocks");
                AssetDatabase.CreateAsset(mesh, path);
            }
            else
                EditorUtility.SetDirty(mesh);

            child.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            child.localScale = Vector3.one;
            child.GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        /// <summary>
        /// The hull over one track as plain lines along it: every row of cells (one x, one y) beyond the track's
        /// inner edge is filled solid from its first cell to its last; rows much shorter than the longest (brackets,
        /// boxes, mudguard bends) go; row ends close to the common end line up on it. A lone row gets the cell row
        /// under it too, so the opening after this keeps the fender.
        /// </summary>
        private static void StraightSide(VoxelizerPartGrid grid, int side)
        {
            int3 size = grid.Size;
            float cell = grid.VoxelSize;
            int Index(int x, int y, int z) => x + size.x * (y + size.y * z);
            bool Beyond(int x) => side * (grid.Origin.x + (x + 0.5f) * cell) > TRACK_INNER_X;

            var first = new int[size.x, size.y];
            var last = new int[size.x, size.y];
            var count = new int[size.x, size.y];
            var colour = new Color32[size.x, size.y];
            int longest = 0;
            for (int x = 0; x < size.x; x++)
            {
                if (!Beyond(x))
                    continue;
                for (int y = 0; y < size.y; y++)
                {
                    first[x, y] = -1;
                    for (int z = 0; z < size.z; z++)
                    {
                        Color32 c = grid.Cells[Index(x, y, z)];
                        if (c.a == 0)
                            continue;
                        if (first[x, y] < 0)
                        {
                            first[x, y] = z;
                            colour[x, y] = c;
                        }
                        last[x, y] = z;
                        count[x, y]++;
                    }
                    longest = Mathf.Max(longest, count[x, y]);
                }
            }

            if (longest == 0)
                return;

            var keep = new bool[size.x, size.y];
            var starts = new Dictionary<int, int>();
            var ends = new Dictionary<int, int>();
            for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
            {
                keep[x, y] = Beyond(x) && count[x, y] * 2 >= longest;
                if (!keep[x, y])
                    continue;
                starts.TryGetValue(first[x, y], out int a);
                starts[first[x, y]] = a + 1;
                ends.TryGetValue(last[x, y], out int b);
                ends[last[x, y]] = b + 1;
            }

            int start = Mode(starts), end = Mode(ends);
            for (int x = 0; x < size.x; x++)
            for (int y = 1; y < size.y; y++)
                if (keep[x, y] && (y + 1 >= size.y || !keep[x, y + 1]) && !keep[x, y - 1])
                {
                    keep[x, y - 1] = true;
                    first[x, y - 1] = first[x, y];
                    last[x, y - 1] = last[x, y];
                    colour[x, y - 1] = colour[x, y];
                }

            for (int x = 0; x < size.x; x++)
            {
                if (!Beyond(x))
                    continue;
                for (int y = 0; y < size.y; y++)
                {
                    int from = Mathf.Abs(first[x, y] - start) <= 3 ? start : first[x, y];
                    int to = Mathf.Abs(last[x, y] - end) <= 3 ? end : last[x, y];
                    for (int z = 0; z < size.z; z++)
                        grid.Cells[Index(x, y, z)] = keep[x, y] && z >= from && z <= to ? colour[x, y] : default;
                }
            }
        }

        private static int Mode(Dictionary<int, int> counts)
        {
            int best = 0, most = -1;
            foreach (KeyValuePair<int, int> e in counts)
                if (e.Value > most)
                {
                    best = e.Key;
                    most = e.Value;
                }
            return best;
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
        private static Transform Place(Transform parent, string name, Mesh mesh, Material material, Vector3 shift) =>
            Child(parent, name, mesh, material, Offset + shift, Quaternion.identity, SCALE);

        /// <summary>A mesh with its own pivot, at model-space transform <paramref name="m"/>.</summary>
        private static Transform PlaceModel(Transform parent, string name, Mesh mesh, Material material, Matrix4x4 m) =>
            Child(parent, name, mesh, material, Model(m.GetColumn(3)), m.rotation, SCALE);

        private static Transform Child(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Quaternion rotation, float scale)
        {
            var go = new GameObject(name) { layer = parent.gameObject.layer };
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(position, rotation);
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
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

        private static void Tint(Material m, Color colour)
        {
            m.SetColor("_BaseColor", colour);
            if (m.HasProperty("_Color"))
                m.SetColor("_Color", colour);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssetIfDirty(m);
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
