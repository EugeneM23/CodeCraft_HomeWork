using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Saving.Objects
{
    /// <summary>
    /// The hidden registry: every live Saveable by id and the original set of every loaded scene. Saveables register
    /// in Awake; on sceneLoaded (before Start) the scene is scanned, disabled objects included, which is why no
    /// registry object is needed in a scene. Static state resets when Play Mode starts, with or without a domain
    /// reload.
    /// </summary>
    internal static class SaveRegistry
    {
        internal sealed class SceneRecord
        {
            public SceneRecord(Scene scene)
            {
                Scene = scene;
                ScanFrame = Time.frameCount;
            }

            public Scene Scene { get; }
            public HashSet<string> Originals { get; } = new();
            public int ScanFrame { get; }
            public bool AnyStarted { get; set; }
            public bool Applied { get; set; }

            /// <summary>Scanned this frame and untouched: a load may apply to it in place instead of reloading it.</summary>
            public bool IsFresh => ScanFrame == Time.frameCount && !AnyStarted && !Applied;

            /// <summary>
            /// In its first frames and not restored yet: a Load On Start of a system in the scene may apply in place —
            /// the Starts that ran are the scene's own setup, and Saveables apply their data after them anyway.
            /// </summary>
            public bool IsStarting => Time.frameCount - ScanFrame <= 1 && !Applied;
        }

        private static readonly Dictionary<string, Saveable> ById = new();
        private static readonly Dictionary<Scene, SceneRecord> Scenes = new();

        public static event Action<Scene> SceneScanned;
        public static event Action<Saveable> Ready;

        /// <summary>A new Saveable joined: its Awake, or the scan of its scene (disabled ones too); not on Rekey.</summary>
        public static event Action<Saveable> Registered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            ById.Clear();
            Scenes.Clear();
            SceneScanned = null;
            Ready = null;
            Registered = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        public static string NewId() => Guid.NewGuid().ToString("N");

        public static void Register(Saveable saveable)
        {
            if (saveable.IsRegistered)
                return;

            saveable.IsRegistered = true;
            string id = saveable.Id;
            if (string.IsNullOrEmpty(id))
            {
                Add(saveable, NewId(), spawned: true);
            }
            else if (ById.TryGetValue(id, out Saveable other) && other != null && !ReferenceEquals(other, saveable))
            {
                // A copy made in play is expected; the same id twice in a scene being loaded is not
                if (!Scenes.ContainsKey(saveable.gameObject.scene))
                    Debug.LogError($"'{saveable.name}' has the id {id} of '{other.name}'; it gets a new id and counts as " +
                                   "spawned. Run Tools → Save System → Validate Scenes.", saveable);

                Add(saveable, NewId(), spawned: true);
            }
            else
            {
                Add(saveable, id, spawned: false);
            }

            Registered?.Invoke(saveable);
        }

        public static void Unregister(Saveable saveable)
        {
            if (!saveable.IsRegistered)
                return;

            saveable.IsRegistered = false;
            if (!string.IsNullOrEmpty(saveable.Id) && ById.TryGetValue(saveable.Id, out Saveable current)
                && ReferenceEquals(current, saveable))
                ById.Remove(saveable.Id);
        }

        /// <summary>Gives a restored spawned object the id it was saved with.</summary>
        public static void Rekey(Saveable saveable, string id)
        {
            if (!string.IsNullOrEmpty(saveable.Id) && ById.TryGetValue(saveable.Id, out Saveable current)
                && ReferenceEquals(current, saveable))
                ById.Remove(saveable.Id);

            if (ById.TryGetValue(id, out Saveable other) && other != null && !ReferenceEquals(other, saveable))
            {
                Debug.LogError($"The saved id {id} is taken by '{other.name}'; '{saveable.name}' keeps a new one.", saveable);
                id = NewId();
            }

            saveable.IsRegistered = true;
            Add(saveable, id, spawned: true);
        }

        public static bool TryGet(string id, out Saveable saveable)
        {
            saveable = null;
            return !string.IsNullOrEmpty(id) && ById.TryGetValue(id, out saveable) && saveable != null;
        }

        public static List<Saveable> All() => ById.Values.Where(saveable => saveable != null).ToList();

        public static List<Saveable> InScene(Scene scene) =>
            ById.Values.Where(saveable => saveable != null && saveable.gameObject.scene == scene).ToList();

        public static List<SceneRecord> Records() => Scenes.Values.ToList();

        public static bool TryGetRecord(Scene scene, out SceneRecord record) => Scenes.TryGetValue(scene, out record);

        public static void NotifyReady(Saveable saveable, bool started)
        {
            if (started && Scenes.TryGetValue(saveable.gameObject.scene, out SceneRecord record))
                record.AnyStarted = true;

            Ready?.Invoke(saveable);
        }

        private static void Add(Saveable saveable, string id, bool spawned)
        {
            saveable.SetId(id);
            saveable.IsSpawned = spawned;
            ById[id] = saveable;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // The time of the frame, before the scene's scripts start inside its fixed step
            SaveClock.Mark();
            // A scene whose save system is set to Whole Scene: its objects get their hidden Saveables first
            WholeScene.Adopt(scene);
            var record = new SceneRecord(scene);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Saveable saveable in root.GetComponentsInChildren<Saveable>(true))
                {
                    Register(saveable);
                    if (!saveable.IsSpawned)
                        record.Originals.Add(saveable.Id);
                }
            }

            Scenes[scene] = record;
            SceneScanned?.Invoke(scene);
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            Scenes.Remove(scene);
            WholeScene.Forget(scene);
            // Objects that were never active get no OnDestroy: drop what died with the scene
            foreach (string id in ById.Where(pair => pair.Value == null).Select(pair => pair.Key).ToList())
                ById.Remove(id);
        }
    }
}
