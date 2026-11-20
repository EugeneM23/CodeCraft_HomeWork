using System.Collections.Generic;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Loadout stats, stun, special module and smoke screen: what <see cref="CombatBehaviour"/> installs.
    ///
    /// The level gets a <see cref="SmokeScreen"/> (GameContext key SmokeScreen). The player's tank gets its
    /// CombatModifiers (made here if the tank has none, before the other areas attach), its TankStun, the loadout
    /// from the save (<see cref="TankStatsUseCase"/>; the standard modules at level 1 without a save or an app) applied
    /// through a <see cref="TankStatsApplier"/>, the screens' plates when ArmorSkirts is in, the smoke launchers as its
    /// SpecialModule and E to fire them. The loadout is applied again when a run starts (IRunService.RunStarted), if the
    /// level has a run.
    /// </summary>
    public static class TankModulesSetup
    {
        private static readonly List<ModuleLevel> Loadout = new(TankConfig.SLOT_COUNT);

        /// <summary>
        /// Each tank's smoke launchers, kept across leaving and re-entering it: a launcher made anew would be ready at
        /// once, a free reload. Forgotten when the level's combat is uninstalled.
        /// </summary>
        private static readonly Dictionary<IEntity, SmokeLauncherModule> Launchers = new();

        private static GameContext s_context;
        private static CombatConfig s_config;
        private static SmokeScreen s_smokeScreen;
        private static IRunService s_run;

        private static IEntity s_tank;
        private static TankStatsApplier s_applier;
        private static TankSkirtsView s_skirts;
        private static SpecialModuleInputBehaviour s_input;
        private static SmokeLauncherModule s_smoke;

        public static void InstallContext(GameContext context, CombatConfig config)
        {
            s_context = context;
            s_config = config;
            s_smokeScreen = new SmokeScreen();
            if (context.HasSmokeScreen())
                context.SetSmokeScreen(s_smokeScreen);
            else
                context.AddSmokeScreen(s_smokeScreen);

            SubscribeRun(context);
        }

        public static void UninstallContext(GameContext context)
        {
            UnsubscribeRun();

            if (context != null && context.TryGetSmokeScreen(out ISmokeScreen screen) &&
                ReferenceEquals(screen, s_smokeScreen))
                context.DelSmokeScreen();

            Launchers.Clear();
            s_smokeScreen = null;
            s_context = null;
            s_config = null;
        }

        public static void AttachTank(GameContext context, IEntity tank, CombatConfig config)
        {
            if (tank == null)
                return;

            s_context = context;
            s_config = config;
            s_tank = tank;
            SubscribeRun(context);

            if (!tank.TryGetCombatModifiers(out CombatModifiers modifiers) || modifiers == null)
            {
                modifiers = new CombatModifiers();
                if (tank.HasCombatModifiers())
                    tank.SetCombatModifiers(modifiers);
                else
                    tank.AddCombatModifiers(modifiers);
            }

            if (!tank.HasTankStun())
                tank.AddTankStun(new TankStun());

            TankCombatStats stats = ComputeStats(config);
            UpdateSpecial(tank, stats);
            UpdateSkirts(tank);
            s_applier = new TankStatsApplier(tank, modifiers, stats);

            PlayerContext player = null;
            if (context != null)
                context.TryGetPlayerContext(out player);
            s_input = new SpecialModuleInputBehaviour(player);
            tank.AddBehaviour(s_input);
        }

        public static void DetachTank(GameContext context, IEntity tank)
        {
            if (tank == null || !ReferenceEquals(tank, s_tank))
                return;

            s_applier?.Dispose();
            s_applier = null;

            if (s_input != null)
                tank.DelBehaviour(s_input);
            s_input = null;

            if (tank.TryGetSpecialModule(out ISpecialModule special) &&
                ((s_smoke != null && ReferenceEquals(special, s_smoke)) ||
                 (s_barrage != null && ReferenceEquals(special, s_barrage))))
                tank.DelSpecialModule();
            s_smoke = null;

            s_skirts?.Hide();
            s_skirts = null;

            s_tank = null;
        }

        /// <summary>Where the player points: the nearest thing under the pointer (not the tank itself), or the pointer
        /// on the tank's ground level.</summary>
        private static bool AimAtPointer(out Vector3 point)
        {
            point = default;
            if (s_context == null || !s_context.TryGetPlayerContext(out PlayerContext player) || player == null ||
                !player.TryGetPlayerInput(out IPlayerInput input) || input == null)
                return false;
            Camera camera = Camera.main;
            if (camera == null)
                return false;

            Vector2 screen = CursorUseCase.ScreenPoint(input.Pointer);
            Transform root = s_tank != null && s_tank.TryGetTransform(out Transform t) ? t : null;
            Ray ray = camera.ScreenPointToRay(screen);
            int count = Physics.RaycastNonAlloc(ray, AimHits, 500f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = AimHits[i];
                if (hit.distance >= best || (root != null && hit.transform.IsChildOf(root)))
                    continue;
                best = hit.distance;
                point = hit.point;
            }

            if (best < float.MaxValue)
                return true;
            return CursorUseCase.TryProjectOnPlane(camera, screen, root != null ? root.position.y : 0f, out point);
        }

        /// <summary>The selected tank's loadout from the save, its stats; the standard loadout without an app.</summary>
        private static TankCombatStats ComputeStats(CombatConfig config)
        {
            GameCatalog catalog = null;
            IMetaProgress progress = null;
            if (AppContext.TryGet(out AppContext app) && app != null)
            {
                app.TryGetGameCatalog(out catalog);
                app.TryGetMetaProgress(out progress);
            }

            TankId id = progress != null ? progress.SelectedTank : TankIds.Tiger;
            TankConfig tank = catalog != null ? catalog.FindTank(id) : null;
            TankStatsUseCase.ResolveLoadout(tank, progress, Loadout);
            return TankStatsUseCase.Compute(tank, Loadout, config != null ? config.Tiger : null,
                config != null ? config.Modules : null);
        }

        /// <summary>The special module on E for now: an air strike of 10 bombs on the aim point (the user's call),
        /// in place of the smoke launchers.</summary>
        private const bool AIR_STRIKE_ON_E = true;

        private static AirStrikeBarrageModule s_barrage;
        private static readonly RaycastHit[] AimHits = new RaycastHit[16];

        /// <summary>Smoke launchers as the SpecialModule when the loadout has them; none otherwise. With
        /// <see cref="AIR_STRIKE_ON_E"/>, the air strike barrage instead.</summary>
        private static void UpdateSpecial(IEntity tank, TankCombatStats stats)
        {
            if (AIR_STRIKE_ON_E)
            {
                s_barrage ??= new AirStrikeBarrageModule(AimAtPointer, stats.SpecialCooldown);
                s_barrage.Cooldown = stats.SpecialCooldown;
                if (tank.HasSpecialModule())
                    tank.SetSpecialModule(s_barrage);
                else
                    tank.AddSpecialModule(s_barrage);
                return;
            }

            bool smoke = TankStatsUseCase.Contains(Loadout, ModuleIds.SpecialSmoke);
            if (smoke && s_smoke == null && tank.TryGetTransform(out Transform transform))
            {
                s_smoke = GetOrCreateLauncher(tank, transform, s_smokeScreen, stats.SpecialDuration,
                    stats.SpecialCooldown);
                if (tank.HasSpecialModule())
                    tank.SetSpecialModule(s_smoke);
                else
                    tank.AddSpecialModule(s_smoke);
            }
            else if (!smoke && s_smoke != null)
            {
                if (tank.TryGetSpecialModule(out ISpecialModule special) && ReferenceEquals(special, s_smoke))
                    tank.DelSpecialModule();
                s_smoke = null;
            }
        }

        /// <summary>
        /// The tank's launcher kept from an earlier attach (its reload goes on; the loadout's duration and cooldown
        /// are written again), or a new one, remembered for the tank. One kept for another smoke screen (a reinstalled
        /// level) is replaced.
        /// </summary>
        public static SmokeLauncherModule GetOrCreateLauncher(IEntity tank, Transform transform, SmokeScreen screen,
            float duration, float cooldown)
        {
            if (Launchers.TryGetValue(tank, out SmokeLauncherModule launcher) && launcher != null &&
                ReferenceEquals(launcher.Screen, screen))
            {
                launcher.Duration = duration;
                launcher.Cooldown = cooldown;
                return launcher;
            }

            launcher = new SmokeLauncherModule(transform, screen, duration, cooldown);
            Launchers[tank] = launcher;
            return launcher;
        }

        /// <summary>The screens' plates on the hull while ArmorSkirts is in the loadout.</summary>
        private static void UpdateSkirts(IEntity tank)
        {
            bool skirts = TankStatsUseCase.Contains(Loadout, ModuleIds.ArmorSkirts);
            if (skirts)
            {
                s_skirts ??= new TankSkirtsView();
                s_skirts.Show(tank);
            }
            else
            {
                s_skirts?.Hide();
                s_skirts = null;
            }
        }

        /// <summary>A run began: its loadout may differ from the one the tank came with.</summary>
        private static void OnRunStarted()
        {
            if (s_tank == null || s_applier == null)
                return;

            TankCombatStats stats = ComputeStats(s_config);
            UpdateSpecial(s_tank, stats);
            UpdateSkirts(s_tank);
            s_applier.SetStats(stats);
        }

        private static void SubscribeRun(GameContext context)
        {
            if (s_run != null || context == null)
                return;
            if (context.TryGetRun(out IRunService run) && run != null)
            {
                s_run = run;
                s_run.RunStarted += OnRunStarted;
            }
        }

        private static void UnsubscribeRun()
        {
            if (s_run != null)
                s_run.RunStarted -= OnRunStarted;
            s_run = null;
        }
    }
}
