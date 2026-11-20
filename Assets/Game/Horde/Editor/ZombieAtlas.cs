using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Horde.EditorTools
{
    /// <summary>
    /// Tools/Zombie/Make Vivid Atlas: T_Zombie_03_A_Vivid.png, the zombie's atlas less pale (the user: brighter, more
    /// colour): every pixel's saturation and value raised in HSV. Made from the original each time, with its import
    /// settings. The horde's VAT material (HordeVatBaker) and the skinned zombie's MT_Zombie (ZombiePrefabBuilder) use it.
    /// </summary>
    public static class ZombieAtlas
    {
        public const string SOURCE = "Assets/Game/Textures/Zombie/T_Zombie_03_A.png";
        public const string VIVID = "Assets/Game/Textures/Zombie/T_Zombie_03_A_Vivid.png";

        /// <summary>Saturation times this plus <see cref="SATURATION_ADD"/>, value times <see cref="BRIGHTNESS"/> (both clamped to 1).</summary>
        private const float SATURATION = 2.6f; // was 1.8: more colour
        private const float SATURATION_ADD = 0.12f;
        private const float BRIGHTNESS = 1.6f; // was 1.2: brighter so they do not melt into the grass

        [MenuItem("Tools/Zombie/Make Vivid Atlas")]
        public static void MakeFromMenu() => Debug.Log(Make());

        /// <summary>Writes and imports the vivid atlas; returns a short report.</summary>
        public static string Make()
        {
            string full = Path.GetFullPath(SOURCE);
            if (!File.Exists(full))
                return "ZombieAtlas: no " + SOURCE;

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(full), false))
                    return "ZombieAtlas: cannot read " + SOURCE;

                Color32[] pixels = texture.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 p = pixels[i];
                    Color.RGBToHSV(p, out float h, out float s, out float v);
                    s = Mathf.Min(1f, s * SATURATION + (s > 0.02f ? SATURATION_ADD : 0f));
                    v = Mathf.Min(1f, v * BRIGHTNESS);
                    Color32 c = Color.HSVToRGB(h, s, v);
                    c.a = p.a;
                    pixels[i] = c;
                }

                texture.SetPixels32(pixels);
                File.WriteAllBytes(Path.GetFullPath(VIVID), texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(VIVID, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(SOURCE) is TextureImporter source && AssetImporter.GetAtPath(VIVID) is TextureImporter vivid)
            {
                var settings = new TextureImporterSettings();
                source.ReadTextureSettings(settings);
                vivid.SetTextureSettings(settings);
                vivid.maxTextureSize = source.maxTextureSize;
                vivid.textureCompression = source.textureCompression;
                vivid.SetPlatformTextureSettings(source.GetPlatformTextureSettings("Standalone"));
                vivid.SaveAndReimport();
            }

            return "ZombieAtlas: wrote " + VIVID;
        }
    }
}
