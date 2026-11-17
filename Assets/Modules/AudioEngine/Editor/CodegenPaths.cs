using UnityEditor;

namespace AudioEngine
{
    /// <summary>
    /// Where the UI sound generators write: the module's Codegen folder (the one with the AudioEngine.Codegen
    /// assembly), wherever the module lies in the project.
    /// </summary>
    public static class CodegenPaths
    {
        public const string ASSEMBLY = "AudioEngine.Codegen";

        public static string Folder
        {
            get
            {
                foreach (string guid in AssetDatabase.FindAssets(ASSEMBLY + " t:AssemblyDefinitionAsset"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith("/" + ASSEMBLY + ".asmdef"))
                        return path.Substring(0, path.LastIndexOf('/'));
                }

                throw new System.InvalidOperationException($"No {ASSEMBLY}.asmdef in the project: nowhere to generate the code.");
            }
        }

        public static string UIFile(string fileName) => Folder + "/UI/" + fileName;
    }
}
