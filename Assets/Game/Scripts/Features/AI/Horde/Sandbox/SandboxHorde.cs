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

        /// <summary>Groups come (on: the user wants walking zombies in the sandbox; no leaping onto the tank while HordeRiderOptions.LeapOntoTank is off).
        /// Off clears the sandbox horde without deaths.</summary>
        // Off for now (the user, 2026-11-21): no zombies in the sandbox at all until the panel's toggle turns them on.
        private const bool ON_AT_START = false;

        public static bool Enabled { get; set; } = ON_AT_START;

        /// <summary>A group comes on its own (first after SandboxHordeConfig.firstDelay, the next after the last is wiped
        /// out). Off by default (the user): a group comes on the H hotkey or the panel's «Группа сейчас».</summary>
        public static bool AutoGroups { get; set; }

        /// <summary>Crawlers in a group (the config's until set).</summary>
        public static int GroupSize
        {
            get => s_groupSize > 0 ? s_groupSize : s_config != null ? s_config.groupSize : 20;
            set => s_groupSize = Mathf.Clamp(value, 1, 32768);
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
            Enabled = ON_AT_START;
            AutoGroups = false;
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
            installer.gameObject.AddComponent<SandboxHordeCounter>();
            installer.gameObject.AddComponent<EnemyTankSpawner>().Init(s_config.enemyTank, s_config.mapMin, s_config.mapMax);
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
            DebugEntry houses = DebugPanelRegistry.AddToggle(WorldDebugCommands.ENEMIES, "Зомби в домах",
                () => HouseZombies.Enabled, v => HouseZombies.Enabled = v, 11);
            houses.Visible = () => s_active != null;
            DebugEntry stock = DebugPanelRegistry.AddInfo(WorldDebugCommands.ENEMIES, HousesStatus, 11);
            stock.Visible = () => s_active != null;
            DebugEntry auto = DebugPanelRegistry.AddToggle(WorldDebugCommands.ENEMIES, "Группы сами (иначе по H)",
                () => AutoGroups, v => AutoGroups = v, 11);
            auto.Visible = () => s_active != null;
            DebugEntry size = DebugPanelRegistry.AddSlider(WorldDebugCommands.ENEMIES, "Размер группы", 5f, 20000f,
                () => GroupSize, v => GroupSize = Mathf.RoundToInt(v), 12);
            size.Visible = () => s_active != null;
            DebugEntry buttons = DebugPanelRegistry.AddButtons(WorldDebugCommands.ENEMIES, null, Buttons, Press, 13);
            buttons.Visible = () => s_active != null;
            DebugEntry tank = DebugPanelRegistry.AddButton(WorldDebugCommands.ENEMIES, "Вражеский танк (J)",
                () => EnemyTankSpawner.Active?.Spawn(), 14);
            tank.Visible = () => EnemyTankSpawner.Active != null;
            DebugEntry ally = DebugPanelRegistry.AddButton(WorldDebugCommands.ENEMIES, "Свой танк (K)",
                () => EnemyTankSpawner.Active?.SpawnAlly(), 14);
            ally.Visible = () => EnemyTankSpawner.Active != null;
        }

        private static string HousesStatus()
        {
            HouseZombies houses = s_active?.Houses;
            return houses == null ? "" : $"В домах: {houses.ZombiesLeft} в {houses.RoomsLeft} комнатах (по {HouseZombies.PerRoom}), потоков {houses.Streams}";
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
