using NUnit.Framework;

namespace AIKit.Tests
{
    /// <summary>
    /// The character's say about its blow (the step 90 recheck of the fight): Winding Up, Recovering and Attacking follow
    /// whatever plays the blow — a weapon's move, an ability asset, or an ability a game's own runner plays.
    /// </summary>
    public sealed class CombatStatusTests
    {
        private const float DT = 0.02f;

        [TearDown]
        public void TearDown() => GraphRig.DestroyAll();

        private static bool State(GraphRig rig, CharacterState state)
        {
            Assert.IsTrue(rig.Agent.TryGetState(state, out bool value), $"{state}: the character says");
            return value;
        }

        private static float TickUntil(GraphRig rig, float now, float until)
        {
            while (now < until - 1e-4f)
            {
                rig.Tick(DT);
                now += DT;
            }

            return now;
        }

        [Test]
        public void Status_WindingUpAndRecovering_AnAbilityOfAGamesRunner_FollowItsPhase()
        {
            const float WIND_UP = 0.5f, ACTIVE = 0.1f, RECOVERY = 0.5f;
            var rig = new GraphRig(trace: false);
            var swipe = new AbilityDefinition
            {
                Id = "Swipe", WindUp = WIND_UP, Recovery = RECOVERY, Cooldown = 0f, RequiresTarget = false,
                RequiresGround = false
            };
            rig.Abilities.Add(swipe);
            rig.Abilities.Register(new TimedAbilityRunner(swipe, ACTIVE));
            Assert.IsTrue(rig.Abilities.TryUse("Swipe", rig.Context), "Set-up: the game's runner plays it");

            float now = TickUntil(rig, 0f, WIND_UP * 0.4f);
            Assert.IsTrue(State(rig, CharacterState.WindingUp), "In its wind-up: winding up");
            Assert.IsTrue(State(rig, CharacterState.Attacking));
            Assert.IsFalse(State(rig, CharacterState.Recovering));

            now = TickUntil(rig, now, WIND_UP + ACTIVE + RECOVERY * 0.2f);
            Assert.IsFalse(State(rig, CharacterState.WindingUp));
            Assert.IsTrue(State(rig, CharacterState.Recovering), "After its active part: recovering — the window to punish it");
            Assert.IsTrue(State(rig, CharacterState.Attacking));

            TickUntil(rig, now, WIND_UP + ACTIVE + RECOVERY + 0.1f);
            Assert.IsFalse(State(rig, CharacterState.UsingAbility), "Set-up: over");
            Assert.IsFalse(State(rig, CharacterState.Attacking));
            Assert.IsFalse(State(rig, CharacterState.WindingUp));
            Assert.IsFalse(State(rig, CharacterState.Recovering));
        }
    }
}
