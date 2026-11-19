using Game.Horde;
using Game.Voxels;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Writes each horde art config's torn skins (HordeArtConfig.tornSkins): the colours of the head, torso, arms and legs
    /// of its crowd mesh under its material's atlas (SkinAtlas). In the editor, out of play: the baked crowd mesh is not
    /// readable at run time. Run it again after a bake (Tools/Horde/Bake Zombie VAT).
    /// </summary>
    public static class HordeTornSkinsSampler
    {
        [MenuItem("Tools/Horde/Sample Torn Skins")]
        public static void SampleAll()
        {
            int written = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:HordeArtConfig"))
            {
                var config = AssetDatabase.LoadAssetAtPath<HordeArtConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (config != null && Sample(config))
                    written++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[HordeTornSkins] {written} art config(s) sampled.");
        }

        /// <summary>Samples one config; false when it has no textured material or no mesh.</summary>
        public static bool Sample(HordeArtConfig config)
        {
            SkinAtlas atlas = SkinAtlas.Of(SkinAtlas.TextureOf(config.material), config.mesh);
            if (atlas == null)
                return false;
            Color32[][] parts = atlas.ChunkSkins();
            config.tornSkins = new HordeTornSkins { head = parts[0], torso = parts[1], arms = parts[2], legs = parts[4] };
            EditorUtility.SetDirty(config);
            return true;
        }
    }
}
