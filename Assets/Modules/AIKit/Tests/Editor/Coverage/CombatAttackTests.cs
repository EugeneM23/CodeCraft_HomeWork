using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The Attack node's move as the character carries it out (AttackRunner), at its corners (the step 90 recheck of the
    /// fight): a place to fight from that Take Attack Slot gave in the group a weapon shoots from stays the agent's —
    /// the shot gives back only a place it asked for itself; the phase of the blow is the agent's status from the
    /// wind-up to the end; a bow that finds no place to shoot from does not draw and gives up its request; a weapon
    /// taken out of the hand, or a target gone, in the middle of a swing lands no blow; a draw cut short gives its place
    /// back at once.
    /// </summary>
    public sealed class CombatAttackTests
    {
        private const string VICTIM = "Victim";
        private const string RANGED = "Ranged";
        private const float DT = 0.02f;

        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _created.Clear();
            GraphRig.DestroyAll();
        }

        private static WeaponMove Shot() =>
            new() { Name = "Shot", Reach = 14f, Damage = 2f, WindUp = 0.8f, Recovery = 0.5f, NeedsSight = true };

        private WeaponAsset Bow(string pool)
        {
            WeaponAsset bow = WeaponAsset.Create(Shot());
            bow.Shoots = true;
            bow.Shot.Pool = pool;
            _created.Add(bow);
            return bow;
        }

        private Transform Target(Vector3 at, int rangedPlaces)
        {
            var go = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = at;
            go.AddComponent<AttackSlotCapacity>().SetPool(RANGED, rangedPlaces);
            go.AddComponent<HitProbe>();
            return go.transform;
        }

        // An archer of the world, awake, its victim bound, no launcher (the shot lands at once)
        private static GraphRig Archer(AIWorld world, WeaponAsset bow, Vector3 at, Transform target)
        {
            var rig = new GraphRig(trace: false, world: world) { ThinkInterval = 0f };
            rig.Agent.gameObject.SetActive(true);
            rig.Actuator.Position = at;
            rig.Agent.transform.position = at;
            rig.GiveWeapon(bow);
            rig.Context.Launcher = null;
            rig.Agent.Blackboard.Set(VICTIM, target);
            return rig;
        }

        // ---------- a place to fight from in the shots' group ----------

        [Test]
        public void Bow_APlaceTakenWithTakeAttackSlotInTheShotsGroup_IsStillHeldAfterTheShot()
        {
            WeaponMove shot = Shot();
            var world = new AIWorld();
            Transform target = Target(new Vector3(0f, 0f, 8f), rangedPlaces: 1);
            GraphRig archer = Archer(world, Bow(RANGED), Vector3.zero, target);
            var take = new TakeAttackSlotTask { Pool = RANGED, WaitTime = 0f, Target = VICTIM };
            take.Tick(archer.Context, DT);
            world.BeginStep(DT);
            Engagement engagement = world.Roster.FindEngagement(target);
            Assert.IsTrue(engagement.Tokens.Holds(archer.Agent, RANGED), "Set-up: Take Attack Slot gave it its place");
            var attack = new AttackTask { Target = VICTIM, Move = shot.Name };
            archer.SetGraph(GraphRig.OneState(attack));

            bool alwaysHeld = true;
            int hits = 0;
            HitProbe victim = target.GetComponent<HitProbe>();
            for (int i = 0; i < Mathf.CeilToInt((shot.WindUp + shot.Recovery + 0.2f) / DT); i++)
            {
                world.BeginStep(DT);
                archer.Step(DT);
                alwaysHeld &= engagement.Tokens.Holds(archer.Agent, RANGED);
                hits = victim.Hits;
            }

            Assert.AreEqual(1, hits, "Set-up: one shot was made");
            Assert.IsTrue(alwaysHeld,
                "The place Take Attack Slot gave is not the shot's to give back: it stays the archer's through the move");
        }

        // ---------- the phase of the blow ----------

        // A character with one plain move, its victim bound at this distance, the blows it takes counted
        private static GraphRig Striker(float reach, float windUp, float recovery, float victimAt, out HitProbe hits)
        {
            var rig = new GraphRig(trace: false);
            rig.GiveWeapon(reach, windUp, recovery);
            Transform victim = rig.CreateTargetAt(new Vector3(0f, 0f, victimAt));
            rig.Agent.Blackboard.Set(VICTIM, victim);
            hits = victim.gameObject.AddComponent<HitProbe>();
            return rig;
        }

        private static bool State(GraphRig rig, CharacterState state)
        {
            Assert.IsTrue(rig.Agent.TryGetState(state, out bool value), $"{state}: the character says");
            return value;
        }

        [Test]
        public void AttackTask_WeaponMove_StatusWindingUpThenRecovering_NoneWhenStopped()
        {
            const float WIND_UP = 0.4f, RECOVERY = 0.5f;
            GraphRig rig = Striker(2f, WIND_UP, RECOVERY, 1f, out HitProbe hits);
            rig.SetGraph(GraphRig.OneState(new AttackTask { Target = VICTIM }));
            rig.Instance.Start(rig.Context);

            rig.StepSeconds(WIND_UP * 0.5f);
            Assert.AreEqual(AbilityPhase.WindUp, rig.Context.AttackPhase, "In the swing: the wind-up");
            Assert.IsTrue(State(rig, CharacterState.WindingUp));
            Assert.AreEqual(0, hits.Hits);

            rig.StepSeconds(WIND_UP);
            Assert.AreEqual(AbilityPhase.Recovery, rig.Context.AttackPhase, "After the blow: the recovery");
            Assert.IsTrue(State(rig, CharacterState.Recovering));
            Assert.AreEqual(1, hits.Hits);

            rig.Instance.Stop(rig.Context);
            Assert.AreEqual(AbilityPhase.None, rig.Context.AttackPhase, "Stopped: no blow");
            Assert.IsFalse(State(rig, CharacterState.Attacking));
        }

        // ---------- a bow and its place ----------

        [Test]
        public void AttackTask_BowWaitingForAPlace_NotWindingUp_FailsAfterAskFor_RequestGone()
        {
            const float ASK_FOR = 0.3f;
            var world = new AIWorld();
            Transform target = Target(new Vector3(0f, 0f, 8f), rangedPlaces: 1);
            WeaponAsset bow = Bow(RANGED);
            bow.Shot.AskFor = ASK_FOR;
            GraphRig archer = Archer(world, bow, Vector3.zero, target);
            var launcher = new CombatCaptureLauncher();
            archer.Context.Launcher = launcher;
            var other = new GraphRig(trace: false, world: world);
            other.Agent.gameObject.SetActive(true);
            AttackTokens tokens = world.Roster.EngagementOf(target).Tokens;
            tokens.HoldFirmly(RANGED);
            tokens.Request(other.Agent, RANGED, 1, 0f, 0f);
            world.BeginStep(DT);
            Assume.That(tokens.Holds(other.Agent, RANGED), "Set-up: the one place to shoot from is another's");
            var attack = new AttackTask { Target = VICTIM, Move = "Shot" };

            float failedAt = -1f;
            bool drew = false;
            for (int i = 1; i <= 50 && failedAt < 0f; i++)
            {
                world.BeginStep(DT);
                archer.Context.Advance(DT);
                TaskStatus status = attack.Tick(archer.Context, DT);
                drew |= archer.Context.AttackPhase != AbilityPhase.None;
                if (status == TaskStatus.Failure)
                    failedAt = i * DT;
            }

            Assert.IsFalse(drew, "No place: it never drew (no wind-up the target could read)");
            Assert.That(failedAt, Is.InRange(ASK_FOR - 0.02f, ASK_FOR + 0.04f), "It gave up after its Ask For");
            Assert.IsEmpty(launcher.Launches, "Nothing was shot");
            tokens.Release(other.Agent, RANGED);
            world.BeginStep(DT);
            Assert.IsFalse(tokens.Holds(archer.Agent, RANGED), "Its request went with it: the freed place is not given to it");
        }

        [Test]
        public void AttackTask_BowCutShortMidDraw_ItsPlaceToShootFromGoesBack()
        {
            var world = new AIWorld();
            Transform target = Target(new Vector3(0f, 0f, 8f), rangedPlaces: 1);
            WeaponAsset bow = Bow(RANGED);
            bow.Find("Shot").Action = "Draw";
            GraphRig archer = Archer(world, bow, Vector3.zero, target);
            var launcher = new CombatCaptureLauncher();
            archer.Context.Launcher = launcher;
            new ActionLog(archer.Context);                          // a presentation that shows every action
            archer.SetGraph(GraphRig.OneState(new AttackTask { Target = VICTIM, Move = "Shot" }));
            for (int i = 0; i < Mathf.CeilToInt(0.3f / DT); i++)
            {
                world.BeginStep(DT);
                archer.Step(DT);
            }

            Engagement engagement = world.Roster.FindEngagement(target);
            Assert.IsTrue(engagement.Tokens.Holds(archer.Agent, RANGED), "Set-up: it draws from its place");
            Assert.AreEqual(ActionState.Kind.Action, archer.Context.Actions.Current, "Set-up: its draw is shown");

            archer.Instance.Stop(archer.Context);

            Assert.IsFalse(engagement.Tokens.Holds(archer.Agent, RANGED), "Cut short: its place goes back at once");
            Assert.AreEqual(ActionState.Kind.None, archer.Context.Actions.Current, "Its draw is dropped");
            Assert.IsFalse(archer.Context.Actions.EndsGently, "…at once");
            Assert.IsEmpty(launcher.Launches, "Nothing was shot");
        }

        // ---------- the swing interrupted ----------

        [Test]
        public void AttackTask_TheWeaponInTheHandChangesMidSwing_NoBlowLands()
        {
            const float WIND_UP = 0.4f, RECOVERY = 0.4f;
            GraphRig rig = Striker(2f, WIND_UP, RECOVERY, 1f, out HitProbe hits);
            var attack = new AttackTask { Target = VICTIM };
            rig.SetGraph(GraphRig.OneState(attack));
            rig.Instance.Start(rig.Context);
            rig.StepSeconds(WIND_UP * 0.5f);

            rig.GiveWeapon(2f, WIND_UP, RECOVERY);                  // the hand takes another weapon mid-swing
            rig.StepSeconds(WIND_UP * 0.75f);
            Assert.AreEqual(0, hits.Hits, "The swing's weapon left the hand: no blow lands");
            rig.StepSeconds(0.2f);
            Assert.AreEqual(TaskStatus.Running, attack.StatusOf(rig.Context), "The move still runs its time out");
            Assert.AreEqual(0, hits.Hits);

            rig.StepSeconds(WIND_UP + RECOVERY - 0.2f);
            Assert.AreEqual(1, hits.Hits, "The next move is made with the weapon now in the hand");
        }

        [Test]
        public void AttackTask_TheTargetVariableClearedMidSwing_NoBlow_FailsAtTheEndOfTheMove()
        {
            const float WIND_UP = 0.4f, RECOVERY = 0.4f;
            GraphRig rig = Striker(2f, WIND_UP, RECOVERY, 1f, out HitProbe hits);
            var attack = new AttackTask { Target = VICTIM };

            float endedAt = -1f;
            TaskStatus status = TaskStatus.Running;
            for (int i = 1; i <= 100 && endedAt < 0f; i++)
            {
                if (i == Mathf.RoundToInt(WIND_UP * 0.5f / DT))
                    rig.Agent.Blackboard.Set(VICTIM, (Transform)null);  // the variable is emptied mid-swing
                rig.Context.Advance(DT);
                status = attack.Tick(rig.Context, DT);
                if (status != TaskStatus.Running)
                    endedAt = i * DT;
            }

            Assert.AreEqual(0, hits.Hits, "Nobody to strike at the end of the wind-up");
            Assert.AreEqual(TaskStatus.Failure, status, "Out Of Reach: nobody is");
            Assert.AreEqual(WIND_UP + RECOVERY, endedAt, 2f * DT, "…at the end of the move, which runs its time out");
        }
    }
}
