using Atomic.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// Live zombies in the voxel sandbox (the user: «зомби в песочнице запрыгивали на танк»): when a sandbox scene loads,
    /// a <c>[SandboxHorde]</c> entity is made from <see cref="SandboxHordeConfig"/> (Resources) — the real VAT horde with
    /// its riders, plus <see cref="SandboxHordeBehaviour"/> (the group, its respawn, the grid over the map). The hand-placed
    /// Zombie.prefab statues stay as they are. The debug panel's «Враги» tab switches it and sets the group's size.
    /// A level that already has a horde (a test, a bench) gets none.
    /// </summary>
    public static class SandboxHorde
    {
        private static readonly string[] Scenes = { "VoxelSandbox", "VoxelSandbox_Level" };
        private static readonly string[] Buttons = { "Группа сейчас", "Убрать всех" };

        private static SandboxHordeBehaviour s_active;
        private static SandboxHordeConfig s_config;
        private static bool s_registered;
        private static int s_groupSize;

        /// <summary>Groups come (on: the user wants walking zombies in the sandbox; riders are off in its config).
        /// Off clears the sandbox horde without deaths.</summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>Crawlers in a group (the config's until set).</summary>
        public static int GroupSize
        {
            get => s_groupSize > 0 ? s_groupSize : s_config != null ? s_config.groupSize : 20;
            set => s_groupSize = Mathf.Clamp(value, 1, 60);
        }

        /// <summary>The sandbox horde of the loaded sandbox (null elsewhere).</summary>
        public static SandboxHordeBehaviour Active => s_active;

        internal static void SetActive(SandboxHordeBehaviour behaviour) => s_active = behaviour;

        internal static void ClearActive(SandboxHordeBehaviour behaviour)
        {
            if (ReferenceEquals(s_active, behaviour))
                s_active = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            s_active = null;
            s_config = null;
            s_groupSize = 0;
            s_registered = false;
            Enabled = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            RegisterDebug();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (System.Array.IndexOf(Scenes, scene.name) < 0)
                return;
            if (s_active != null || Object.FindFirstObjectByType<HordeInstaller>() != null)
                return;
            Create();
        }

        /// <summary>Makes the sandbox horde now (once per loaded sandbox). Null when the config or its art is missing.</summary>
        public static SandboxHordeBehaviour Create()
        {
            if (s_config == null)
                s_config = Resources.Load<SandboxHordeConfig>(SandboxHordeConfig.RESOURCE);
            if (s_config == null || s_config.art == null)
            {
                Debug.LogWarning("SandboxHorde: no Resources/SandboxHordeConfig with the horde art: no live zombies in the sandbox.");
                return null;
            }

            // A copy: the horde writes into its options at run time (damage scale, shadows), the asset must stay as it is.
            var options = JsonUtility.FromJson<HordeOptions>(JsonUtility.ToJson(s_config.horde ?? new HordeOptions()));
            HordeInstaller installer = HordeSpawn.CreateInScene(s_config.art, options);
            if (installer.Behaviour == null)
                return null;
            installer.gameObject.name = "[SandboxHorde]";
            var behaviour = new SandboxHordeBehaviour(s_config, installer.Behaviour);
            installer.GetComponent<MonoEntity>().AddBehaviour(behaviour);
            s_active = behaviour;
            return behaviour;
        }

        private static void RegisterDebug()
        {
            if (s_registered)
                return;
            s_registered = true;

            DebugEntry info = DebugPanelRegistry.AddInfo(WorldDebugCommands.ENEMIES, Status, 10);
            info.Visible = () => s_active != null;
            DebugEntry toggle = DebugPanelRegistry.AddToggle(WorldDebugCommands.ENEMIES, "Активные зомби в песочнице",
                () => Enabled, v => Enabled = v, 11);
            toggle.Visible = () => s_active != null;
            DebugEntry size = DebugPanelRegistry.AddSlider(WorldDebugCommands.ENEMIES, "Размер группы", 5f, 40f,
                () => GroupSize, v => GroupSize = Mathf.RoundToInt(v), 12);
            size.Visible = () => s_active != null;
            DebugEntry buttons = DebugPanelRegistry.AddButtons(WorldDebugCommands.ENEMIES, null, Buttons, Press, 13);
            buttons.Visible = () => s_active != null;
        }

        private static string Status()
        {
            SandboxHordeBehaviour h = s_active;
            if (h == null)
                return null;
            if (!Enabled)
                return "Песочная орда выключена.";
            float next = h.NextGroupIn;
            return next >= 0f
                ? $"Песочная орда: живых {h.Alive}, группа ×{GroupSize} через {next:0} с."
                : $"Песочная орда: живых {h.Alive}, группа ×{GroupSize}.";
        }

        private static void Press(int button)
        {
            SandboxHordeBehaviour h = s_active;
            if (h == null)
                return;
            if (button == 0)
                h.SpawnGroup();
            else
                h.Horde.KillAll();
        }
    }
}
