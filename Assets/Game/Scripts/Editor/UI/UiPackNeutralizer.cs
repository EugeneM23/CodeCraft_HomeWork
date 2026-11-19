using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Grey copies of the SCI-FI UI Pack's cyan sprites: the pack has no orange set, and tinting cyan art orange
    /// gives mud. Each copy keeps the alpha and the brightness of the original (luminance, stretched a little so
    /// the brightest pixel of the art is near white), so an Image colour tints it cleanly: graphite panels,
    /// orange accents. The import settings follow the original: sprite mode, 9-slice border, pixels per unit,
    /// filter, wrap and compression. Copies live in Assets/Game/Art/UI/PackNeutral/ under the pack's own path.
    /// </summary>
    public static class UiPackNeutralizer
    {
        public const string FOLDER = "Assets/Game/Art/UI/PackNeutral/";

        /// <summary>The neutral copy's path for a pack texture path; the path itself when it is not the pack's.</summary>
        public static string NeutralPath(string packPath)
        {
            if (string.IsNullOrEmpty(packPath) || !packPath.StartsWith(UiPackSprites.ROOT))
                return packPath;
            return FOLDER + packPath.Substring(UiPackSprites.ROOT.Length).Replace("(", "").Replace(")", "").Replace(' ', '_');
        }

        /// <summary>Makes the grey copy when it is missing or older than the original; returns its path.</summary>
        public static string Ensure(string packPath)
        {
            string target = NeutralPath(packPath);
            if (target == packPath)
                return packPath;

            string sourceFile = Path.GetFullPath(packPath);
            string targetFile = Path.GetFullPath(target);
            if (!File.Exists(sourceFile))
                return packPath;
            if (File.Exists(targetFile) && File.GetLastWriteTimeUtc(targetFile) >= File.GetLastWriteTimeUtc(sourceFile))
                return target;

            Texture2D source = new(2, 2, TextureFormat.RGBA32, false);
            source.LoadImage(File.ReadAllBytes(sourceFile));
            Color32[] pixels = source.GetPixels32();

            // Brightest visible pixel: stretch so it lands at 0.95, keeping the art's contrast.
            float max = 0.05f;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a < 8)
                    continue;
                float l = Luminance(pixels[i]);
                if (l > max)
                    max = l;
            }
            float gain = Mathf.Min(0.95f / max, 2.5f);

            for (int i = 0; i < pixels.Length; i++)
            {
                byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(Luminance(pixels[i]) * gain * 255f), 0, 255);
                pixels[i] = new Color32(g, g, g, pixels[i].a);
            }

            Texture2D grey = new(source.width, source.height, TextureFormat.RGBA32, false);
            grey.SetPixels32(pixels);
            grey.Apply();

            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.WriteAllBytes(targetFile, grey.EncodeToPNG());
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(grey);

            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
            CopyImport(packPath, target);
            return target;
        }

        private static float Luminance(Color32 c) => (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) / 255f;

        private static void CopyImport(string from, string to)
        {
            if (AssetImporter.GetAtPath(from) is not TextureImporter src || AssetImporter.GetAtPath(to) is not TextureImporter dst)
                return;

            dst.textureType = src.textureType;
            dst.spriteImportMode = src.spriteImportMode;
            dst.spriteBorder = src.spriteBorder;
            dst.spritePivot = src.spritePivot;
            dst.spritePixelsPerUnit = src.spritePixelsPerUnit;
            dst.mipmapEnabled = src.mipmapEnabled;
            dst.filterMode = src.filterMode;
            dst.wrapMode = src.wrapMode;
            dst.alphaIsTransparency = src.alphaIsTransparency;
            dst.textureCompression = src.textureCompression;
            dst.maxTextureSize = src.maxTextureSize;
            dst.sRGBTexture = src.sRGBTexture;

            TextureImporterSettings settings = new();
            src.ReadTextureSettings(settings);
            dst.SetTextureSettings(settings);
            dst.SaveAndReimport();
        }
    }
}
