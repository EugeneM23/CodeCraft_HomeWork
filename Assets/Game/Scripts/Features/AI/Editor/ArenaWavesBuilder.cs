using Game.AIKit.Adapters;
using UnityEditor;
using UnityEngine;

namespace Game.AIKit.Adapters.EditorTools
{
    /// <summary>
    /// Writes the five wave tables of milestone 1 (GDD §10: arenas 1–2 Crawlers only, 25–35 each; 3 the first Spitters;
    /// 4 more Spitters; 5 the elite and a small Crawler horde) and the table of them by arena number, so they can be made
    /// again from code. Batch: <c>-executeMethod Game.AIKit.Adapters.EditorTools.ArenaWavesBuilder.Build</c>.
    /// </summary>
    public static class ArenaWavesBuilder
    {
        public const string FOLDER = "Assets/Game/Configs/AI/Arenas";
        public const string TABLE_PATH = FOLDER + "/ArenaWavesTable.asset";
        public const string ELITE_PREFAB = "Assets/Game/Prefabs/Characters/Regax_Elite.prefab";
        private const int LIVE_LIMIT = 40;

        [MenuItem("Tools/AI/Build Arena Waves")]
        public static void Build()
        {
            EnsureFolder(FOLDER);
            var elite = AssetDatabase.LoadAssetAtPath<GameObject>(ELITE_PREFAB);
            if (elite == null)
                Debug.LogWarning("ArenaWavesBuilder: no " + ELITE_PREFAB + "; arena 5 has no elite prefab.");

            ArenaWaves[] arenas =
            {
                Write(1, null,
                    Wave(0f, 0.5f, (EnemyKind.Crawler, 10)),
                    Wave(15f, 0.35f, (EnemyKind.Crawler, 15))),
                Write(2, null,
                    Wave(0f, 0.45f, (EnemyKind.Crawler, 10)),
                    Wave(12f, 0.35f, (EnemyKind.Crawler, 12)),
                    Wave(26f, 0.3f, (EnemyKind.Crawler, 10))),
                Write(3, null,
                    Wave(0f, 0.4f, (EnemyKind.Crawler, 10)),
                    Wave(12f, 0.35f, (EnemyKind.Crawler, 12), (EnemyKind.Spitter, 2)),
                    Wave(26f, 0.3f, (EnemyKind.Crawler, 8), (EnemyKind.Spitter, 1))),
                Write(4, null,
                    Wave(0f, 0.4f, (EnemyKind.Crawler, 10), (EnemyKind.Spitter, 2)),
                    Wave(12f, 0.35f, (EnemyKind.Crawler, 14), (EnemyKind.Spitter, 3)),
                    Wave(28f, 0.3f, (EnemyKind.Crawler, 12), (EnemyKind.Spitter, 3))),
                Write(5, elite,
                    Wave(0f, 0.4f, (EnemyKind.Crawler, 8)),
                    Wave(6f, 0.5f, (EnemyKind.EliteRegax, 1)),
                    Wave(20f, 0.35f, (EnemyKind.Crawler, 10)))
            };

            var table = AssetDatabase.LoadAssetAtPath<ArenaWavesTable>(TABLE_PATH);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<ArenaWavesTable>();
                AssetDatabase.CreateAsset(table, TABLE_PATH);
            }

            table.arenas = arenas;
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            Debug.Log("ArenaWavesBuilder: wrote " + arenas.Length + " arenas and " + TABLE_PATH);
        }

        private static ArenaWaves Write(int arena, GameObject elitePrefab, params ArenaWave[] waves)
        {
            string path = FOLDER + "/Arena_" + arena.ToString("00") + "_Waves.asset";
            var asset = AssetDatabase.LoadAssetAtPath<ArenaWaves>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ArenaWaves>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.waves = waves;
            asset.liveLimit = LIVE_LIMIT;
            asset.chapterMultiplier = 1f;
            asset.elitePrefab = elitePrefab;
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static ArenaWave Wave(float delay, float interval, params (EnemyKind kind, int count)[] entries)
        {
            var wave = new ArenaWave { delay = delay, spawnInterval = interval, entries = new ArenaWaveEntry[entries.Length] };
            for (int i = 0; i < entries.Length; i++)
                wave.entries[i] = new ArenaWaveEntry(entries[i].kind, entries[i].count);
            return wave;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
