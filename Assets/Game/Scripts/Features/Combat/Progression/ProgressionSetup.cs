using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Experience orbs and run skills: what <see cref="CombatBehaviour"/> installs. The run itself (experience, levels,
    /// the skill offer and its pause) is the core's IRunService; where the level has none (the sandbox, this branch)
    /// a <see cref="LocalRunService"/> stands in, with its debug skill cards.
    /// </summary>
    public static class ProgressionSetup
    {
        public static void InstallContext(GameContext context, CombatConfig config)
        {
            if (!context.HasEnemyDeaths())
                context.AddEnemyDeaths(new EnemyDeaths());

            if (!context.HasRun())
            {
                var run = new LocalRunService(context.GetEnemyDeaths());
                context.AddRun(run);
                run.ShowDebugView();
            }

            // A context restarted in the same scene keeps the field it was given the first time.
            if (!context.TryGetBehaviour(out XpOrbField _))
                context.AddBehaviour(new XpOrbField(config.Xp));
        }

        public static void UninstallContext(GameContext context)
        {
            // XpOrbField is left to the context: this runs inside the context's own dispose pass, which disposes it
            // next; deleting a behaviour in the middle of that pass would shift the ones after it.
            if (context.TryGetRun(out IRunService run) && run is LocalRunService local)
            {
                local.Dispose();
                context.DelRun();
            }
        }

        public static void AttachTank(GameContext context, IEntity tank, CombatConfig config)
        {
            // The modules' setup may have added it already (it runs first)
            if (!tank.TryGetCombatModifiers(out CombatModifiers modifiers) || modifiers == null)
            {
                tank.DelCombatModifiers();
                modifiers = new CombatModifiers();
                tank.AddCombatModifiers(modifiers);
            }

            context.TryGetRun(out IRunService run);
            if (run != null)
            {
                tank.AddBehaviour(new SkillEffectsBehaviour(run, new SkillEffectTable(config.Skills)));
                tank.AddBehaviour(new TankDefeatBehaviour(run));
            }

            if (context.TryGetBehaviour(out XpOrbField field))
                field.SetTank(TransformOf(tank), modifiers);
        }

        public static void DetachTank(GameContext context, IEntity tank)
        {
            tank.DelBehaviour<SkillEffectsBehaviour>();
            tank.DelBehaviour<TankDefeatBehaviour>();

            if (context.TryGetBehaviour(out XpOrbField field))
                field.SetTank(null, null);
        }

        private static Transform TransformOf(IEntity tank)
        {
            if (tank is Component component && component != null)
                return component.transform;

            return tank.TryGetTransform(out Transform transform) ? transform : null;
        }
    }
}
