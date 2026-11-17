using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Saving.Installation
{
    /// <summary>
    /// Makes the module ready the moment it lands in a project, from the Package Manager or a .unitypackage.
    /// Every other assembly of the module compiles only with SAVE_SYSTEM_JSON (Newtonsoft JSON is there), the slot menu,
    /// demo and tests also with SAVE_SYSTEM_UGUI (uGUI is there), so a project without them gets no errors, just this
    /// assembly, which has no references: it adds the missing packages (when a Newtonsoft.Json.dll already sits in
    /// Assets it sets the define instead of adding a second copy). Once the module has compiled, the first run in a
    /// project without save settings creates them and opens the welcome window.
    /// </summary>
    [InitializeOnLoad]
    public static class SaveSystemInstaller
    {
        public const string JSON_PACKAGE = "com.unity.nuget.newtonsoft-json";
        public const string UGUI_PACKAGE = "com.unity.ugui";
        public const string JSON_DEFINE = "SAVE_SYSTEM_JSON";

        /// <summary>Per project and user (UserSettings): the first-run setup is done.</summary>
        public const string SET_UP_KEY = "SaveSystem.Installer.SetUp";

        /// <summary>Per editor session: packages were requested (a failed add is not retried in a loop).</summary>
        public const string REQUESTED_KEY = "SaveSystem.Installer.Requested";

        private const string SETUP_TYPE = "Saving.Editor.SaveSystemSetup, SaveSystem.Editor";

        private static AddAndRemoveRequest _add;

        static SaveSystemInstaller()
        {
            // After the domain reload: the asset database is ready and the package list is known
            EditorApplication.delayCall += Run;
        }

        public enum JsonState { Package, Dll, Missing }

        /// <summary>Where Newtonsoft JSON comes from in this project, if anywhere.</summary>
        public static JsonState Json
        {
            get
            {
                if (HasPackage(JSON_PACKAGE))
                    return JsonState.Package;

                return FindJsonDll() != null ? JsonState.Dll : JsonState.Missing;
            }
        }

        public static bool HasUgui => HasPackage(UGUI_PACKAGE);

        /// <summary>The packages the module still waits for.</summary>
        public static string[] MissingPackages
        {
            get
            {
                var missing = new List<string>();
                if (Json == JsonState.Missing)
                    missing.Add(JSON_PACKAGE);
                if (!HasUgui)
                    missing.Add(UGUI_PACKAGE);
                return missing.ToArray();
            }
        }

        /// <summary>The module has compiled: its editor assembly is loaded.</summary>
        public static bool ModuleCompiled => Type.GetType(SETUP_TYPE) != null;

        public static bool IsInstalling => _add != null && !_add.IsCompleted;

        /// <summary>The folder the module sits in, wherever it was copied or installed.</summary>
        public static string ModuleRoot
        {
            get
            {
                const string self = "/Editor/Installer/SaveSystemInstaller.cs";
                string script = AssetDatabase.FindAssets("t:MonoScript SaveSystemInstaller")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .FirstOrDefault(path => path.EndsWith(self, StringComparison.Ordinal));
                return script?.Substring(0, script.Length - self.Length);
            }
        }

        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (Json == JsonState.Dll)
                EnsureDefine();

            if (MissingPackages.Length > 0)
            {
                AddMissingPackages();
                return;
            }

            if (ModuleCompiled && EditorUserSettings.GetConfigValue(SET_UP_KEY) != "1")
                FirstRun();
        }

        /// <summary>Adds what the module waits for, once per editor session.</summary>
        public static void AddMissingPackages()
        {
            string[] missing = MissingPackages;
            if (missing.Length == 0 || IsInstalling || SessionState.GetBool(REQUESTED_KEY, false))
                return;

            SessionState.SetBool(REQUESTED_KEY, true);
            Debug.Log($"Save System: adding {string.Join(", ", missing)}; the module compiles once they are in.");
            _add = Client.AddAndRemove(missing);
            EditorApplication.update += WaitForAdd;
        }

        private static void WaitForAdd()
        {
            if (!_add.IsCompleted)
                return;

            EditorApplication.update -= WaitForAdd;
            if (_add.Status == StatusCode.Success)
                Debug.Log("Save System: packages added.");
            else
                Debug.LogError($"Save System could not add {string.Join(", ", MissingPackages)}: {_add.Error?.message}. "
                    + "Add them in Window → Package Manager (+ → Add package by name); the module compiles after that.");
        }

        private static bool HasPackage(string name) =>
            PackageInfo.GetAllRegisteredPackages().Any(package => package.name == name);

        private static void FirstRun()
        {
            EditorUserSettings.SetConfigValue(SET_UP_KEY, "1");

            // A project that already has its settings has used the module before: nothing to set up, nothing to greet
            if (AssetDatabase.FindAssets("t:SaveSystemSettings").Length > 0)
                return;

            // The settings the module would create with the first Saveable: a scene without a SaveSystem finds them in play
            Type.GetType(SETUP_TYPE)?.GetMethod("FindOrCreateSettings", BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, null);

            Debug.Log("Save System is ready: add Saveable to an object and press Play. Tools → Save System → Welcome.");
            if (!Application.isBatchMode)
                SaveSystemWelcomeWindow.Open();
        }

        /// <summary>The Newtonsoft dll a project brought itself; adding the package next to it would clash.</summary>
        public static string FindJsonDll() =>
            AssetDatabase.FindAssets("Newtonsoft.Json", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(path => Path.GetFileName(path).Equals("Newtonsoft.Json.dll", StringComparison.OrdinalIgnoreCase));

        /// <summary>SAVE_SYSTEM_JSON for the selected build target: the module compiles against the project's own dll.</summary>
        private static void EnsureDefine()
        {
            var target = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            PlayerSettings.GetScriptingDefineSymbols(target, out string[] defines);
            if (defines.Contains(JSON_DEFINE))
                return;

            PlayerSettings.SetScriptingDefineSymbols(target, defines.Append(JSON_DEFINE).ToArray());
            Debug.Log($"Save System: {FindJsonDll()} found, {JSON_DEFINE} set for {target.TargetName}. "
                + "Other build targets need it too (Player Settings → Scripting Define Symbols).");
        }
    }
}
