using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The character's weapon: the Attack node strikes with the move picked in its Move field — that move's reach,
    /// wind-up and damage; without a weapon, or with no move of that name, it does not strike. A graph saved while the
    /// node still had numbers of its own stays editable.
    /// </summary>
    public sealed class WeaponTests
    {
        private const string VICTIM = "Victim";

        [TearDown]
        public void TearDown() => GraphRig.DestroyAll();

        // The Strike as the one block of a decision's chain. The graph is started in its Brain at once, so the Brain
        // takes the decision on the first step and the Strike runs from that step on — the timings below count from it
        private static void Strike(GraphRig rig, AttackTask attack)
        {
            rig.SetGraph(GraphRig.OneState(attack));
            rig.Instance.Start(rig.Context);
        }

        [Test]
        public void AttackTask_MoveChosen_StrikesWithThatMovesNumbers()
        {
            var heavy = new WeaponMove { Name = "Heavy", Reach = 3f, WindUp = 0.6f, Recovery = 0.4f, Damage = 5f };
            var weapon = WeaponAsset.Create<WeaponAsset>(new WeaponMove { Name = "Normal", Reach = 1f, Damage = 1f }, heavy);
            var rig = new GraphRig();
            rig.GiveWeapon(weapon);
            HitProbe victim = Victim(rig, new Vector3(0f, 0f, heavy.Reach - 0.1f));      // in reach of Heavy only
            Strike(rig, new AttackTask { Target = VICTIM, Move = "Heavy" });

            rig.StepSeconds(heavy.WindUp - 0.05f);
            Assert.That(victim.Hits, Is.Zero, "the blow comes after the wind-up");
            rig.StepSeconds(0.1f);
            Assert.That(victim.Hits, Is.EqualTo(1));
            Assert.That(victim.LastDamage, Is.EqualTo(heavy.Damage));
        }

        [Test]
        public void AttackTask_NoWeaponOrNoSuchMove_DoesNotStrike()
        {
            var rig = new GraphRig();
            HitProbe victim = Victim(rig, new Vector3(0f, 0f, 0.5f));
            Strike(rig, new AttackTask { Target = VICTIM, Move = "Heavy" });
            rig.StepSeconds(2f);
            Assert.That(victim.Hits, Is.Zero, "no weapon");

            rig.GiveWeapon(WeaponAsset.Create<WeaponAsset>(new WeaponMove { Name = "Normal" }));
            rig.StepSeconds(2f);
            Assert.That(victim.Hits, Is.Zero, "no such move");
        }

        [Test]
        public void AttackTask_EmptyMove_StrikesWithTheFirstMove()
        {
            var first = new WeaponMove { Name = "Normal", Reach = 2f, WindUp = 0.2f, Recovery = 0.2f, Damage = 4f };
            var rig = new GraphRig();
            rig.GiveWeapon(WeaponAsset.Create<WeaponAsset>(first, new WeaponMove { Name = "Heavy", Damage = 9f }));
            HitProbe victim = Victim(rig, new Vector3(0f, 0f, first.Reach - 0.5f));
            Strike(rig, new AttackTask { Target = VICTIM });

            rig.StepSeconds(first.WindUp + 0.1f);

            Assert.That(victim.Hits, Is.EqualTo(1));
            Assert.That(victim.LastDamage, Is.EqualTo(first.Damage));
        }

        [Test]
        public void AICharacter_WeaponSetBeforeItWakes_ItsContextHoldsIt()
        {
            var go = new GameObject("Character") { hideFlags = HideFlags.HideAndDontSave };
            WeaponAsset weapon = WeaponAsset.Create<WeaponAsset>(new WeaponMove());
            try
            {
                go.SetActive(false);
                var character = go.AddComponent<AICharacter>();
                character.Body = new SimpleBody();
                character.Weapon = weapon;

                character.Initialize();

                Assert.That(character.Context.Weapon, Is.SameAs(weapon));
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(weapon);
            }
        }

        [Test]
        public void AttackTask_NeedsSight_ComesFromThePickedMove()
        {
            WeaponAsset bow = WeaponAsset.Create<WeaponAsset>(new WeaponMove { Name = "Jab" },
                new WeaponMove { Name = "Shot", NeedsSight = true });
            try
            {
                Assert.That(new AttackTask { Move = "Shot" }.NeedsSightWith(bow), Is.True);
                Assert.That(new AttackTask { Move = "Jab" }.NeedsSightWith(bow), Is.False);
                Assert.That(new AttackTask { Move = "Shot" }.NeedsSightWith(null), Is.False, "no weapon — nothing known");
            }
            finally
            {
                Object.DestroyImmediate(bow);
            }
        }

        [Test]
        public void GraphSerializer_OldAttackNumbers_GraphStaysEditable()
        {
            // A record written before this change: the node's JSON still holds Range / Windup / Recovery / Damage
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            try
            {
                GraphSerializer.Write(GraphRig.OneState(new AttackTask { Target = VICTIM }), asset);
                GraphRecord record = asset.Records.First(r => r.Type == "aikit.task.attack");
                record.Data = record.Data.Substring(0, record.Data.LastIndexOf('}')) +
                              ",\"Range\":{\"_constant\":2.6},\"Windup\":{\"_constant\":1.0}," +
                              "\"Damage\":{\"_constant\":3.0},\"NeedsSight\":true}";

                var issues = new List<GraphIssue>();
                BehaviorGraph read = GraphSerializer.Read(asset, issues);

                Assert.That(read, Is.Not.Null);
                Assert.That(issues.Exists(i => i.Code == GraphIssueCode.UnknownField), Is.False);
                var attack = new List<AgentNode>();
                read.VisitNodes(attack.Add);
                Assert.That(attack.OfType<AttackTask>().Single().Target, Is.EqualTo(VICTIM), "the fields that stay are read");
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void AttackTask_MoveActionTheBodyDoesNotKnow_SwingsInstead()
        {
            var heavy = new WeaponMove { Name = "Heavy", Action = "Heavy Attack", Reach = 2f, WindUp = 0.5f, Recovery = 0.5f };
            var rig = new GraphRig();
            rig.GiveWeapon(WeaponAsset.Create<WeaponAsset>(heavy));
            var body = new ActionLog(rig.Context) { Knows = new HashSet<string>() };   // knows no actions
            Victim(rig, new Vector3(0f, 0f, heavy.Reach - 0.5f));
            Strike(rig, new AttackTask { Target = VICTIM, Move = heavy.Name });

            rig.Step();

            Assert.That(body.Played, Is.EqualTo(new[] { heavy.Action }), "the move's action is asked for first");
            Assert.That(body.Swings, Is.EqualTo(1), "nothing shown for it: the plain swing warns the target");
        }

        [Test]
        public void AttackTask_MoveActionTheBodyKnows_PlaysItWithoutASwing()
        {
            var heavy = new WeaponMove { Name = "Heavy", Action = "Heavy Attack", Reach = 2f, WindUp = 0.5f, Recovery = 0.5f };
            var rig = new GraphRig();
            rig.GiveWeapon(WeaponAsset.Create<WeaponAsset>(heavy));
            var body = new ActionLog(rig.Context, heavy.Action);
            Victim(rig, new Vector3(0f, 0f, heavy.Reach - 0.5f));
            Strike(rig, new AttackTask { Target = VICTIM, Move = heavy.Name });

            rig.Step();

            Assert.That(body.Played, Is.EqualTo(new[] { heavy.Action }));
            Assert.That(body.Swings, Is.Zero);
        }

        [Test]
        public void AttackTask_PreviousActionStillPlaying_PlaysTheMovesActionWithoutASwing()
        {
            var heavy = new WeaponMove { Name = "Heavy", Action = "Heavy Attack", Reach = 2f, WindUp = 0.5f, Recovery = 0.5f };
            var rig = new GraphRig();
            rig.GiveWeapon(WeaponAsset.Create<WeaponAsset>(heavy));
            var body = new ActionLog(rig.Context, heavy.Action);
            body.IsPlaying = true;                                          // the last blow's action is still on
            Victim(rig, new Vector3(0f, 0f, heavy.Reach - 0.5f));
            Strike(rig, new AttackTask { Target = VICTIM, Move = heavy.Name });

            rig.Step();

            Assert.That(body.Played, Is.EqualTo(new[] { heavy.Action }));
            Assert.That(body.Swings, Is.Zero, "the new blow's action shows: no plain swing on top of it");
        }

        [Test]
        public void AttackTask_LeftInTheMiddleOfTheBlow_DropsTheMovesAction()
        {
            var heavy = new WeaponMove { Name = "Heavy", Action = "Heavy Attack", Reach = 2f, WindUp = 0.5f, Recovery = 0.5f };
            var rig = new GraphRig();
            rig.GiveWeapon(WeaponAsset.Create<WeaponAsset>(heavy));
            var body = new ActionLog(rig.Context, heavy.Action);
            Victim(rig, new Vector3(0f, 0f, heavy.Reach - 0.5f));
            var attack = new AttackTask { Target = VICTIM, Move = heavy.Name };
            Strike(rig, attack);
            rig.Step();

            attack.Interrupt(rig.Context);                                  // taken away while it swings

            Assert.That(body.Drops, Is.EqualTo(1), "the action is dropped at once, as a cancelled ability's");
            Assert.That(body.IsPlaying, Is.False);
        }

        [Test]
        public void AttackTask_BlowOver_LeavesTheActionAlone()
        {
            var heavy = new WeaponMove { Name = "Heavy", Action = "Heavy Attack", Reach = 2f, WindUp = 0.2f, Recovery = 0.2f };
            var rig = new GraphRig();
            rig.GiveWeapon(WeaponAsset.Create<WeaponAsset>(heavy));
            var body = new ActionLog(rig.Context, heavy.Action);
            Victim(rig, new Vector3(0f, 0f, heavy.Reach - 0.5f));
            Strike(rig, new AttackTask { Target = VICTIM, Move = heavy.Name });

            Assert.That(rig.StepUntil(() => body.Played.Count >= 2, (heavy.WindUp + heavy.Recovery) * 3f), Is.True,
                "the first blow ended and the next one started");

            Assert.That(body.Drops, Is.Zero, "a blow that ran to its end lets its action finish on its own");
        }


        [Test]
        public void AttackTask_TheBodyStepsWithTheBlow_TheReachIsCountedFromWhereTheSwingStarted()
        {
            // A blow that steps forward with its clip (root motion of actions): the move's Reach is how far from where
            // the swing started the blade gets, the step included — counted from the stepped body it would be a step too far
            var move = new WeaponMove { Name = "Normal", Reach = 3f, WindUp = 0.6f, Recovery = 0.4f, Damage = 1f };
            var rig = new GraphRig();
            rig.GiveWeapon(WeaponAsset.Create<WeaponAsset>(move));
            HitProbe victim = Victim(rig, new Vector3(0f, 0f, move.Reach - 0.2f));
            Strike(rig, new AttackTask { Target = VICTIM });

            rig.StepSeconds(0.1f);
            rig.Actuator.Position += new Vector3(0f, 0f, 1f);                      // the clip stepped a metre
            victim.transform.position = new Vector3(0f, 0f, move.Reach + 0.5f);    // and the target stepped back
            rig.StepSeconds(move.WindUp);

            Assert.That(victim.Hits, Is.Zero, "Out of the blade's reach from where the swing started: missed");

            victim.transform.position = new Vector3(0f, 0f, rig.Actuator.Position.z + move.Reach - 0.2f);
            rig.StepSeconds(move.Recovery + 0.1f);                                 // the next blow starts from here
            rig.StepSeconds(move.WindUp + 0.1f);
            Assert.That(victim.Hits, Is.EqualTo(1), "In reach from where this swing started: it lands");
        }

        [Test]
        public void AttackTask_ABodyWhoseClipsCarryIt_TheBlowIsKeptOffTheTarget_AnotherBodyIsNotAsked()
        {
            var move = new WeaponMove { Name = "Normal", Reach = 3f, WindUp = 0.6f, Recovery = 0.4f };
            var rig = new GraphRig();
            rig.GiveWeapon(WeaponAsset.Create<WeaponAsset>(move));
            HitProbe victim = Victim(rig, new Vector3(0f, 0f, 2f));
            Strike(rig, new AttackTask { Target = VICTIM });
            rig.StepSeconds(0.1f);
            Assert.IsNull(rig.Actuator.ClipKeptOff, "Clips do not carry this body: nothing to keep off");

            var carried = new GraphRig();
            carried.Actuator.ActionsByClip = true;
            carried.GiveWeapon(WeaponAsset.Create<WeaponAsset>(move));
            HitProbe target = Victim(carried, new Vector3(0f, 0f, 2f));
            Strike(carried, new AttackTask { Target = VICTIM });
            carried.StepSeconds(0.1f);

            Assert.AreSame(target.transform, carried.Actuator.ClipKeptOff, "The blow's step stops short of its target");
        }

        // The object to strike, under the variable the node names; it counts the hits it takes
        private static HitProbe Victim(GraphRig rig, Vector3 at)
        {
            Transform victim = rig.CreateTargetAt(at);
            rig.Context.Blackboard.Set(VICTIM, victim);
            return victim.gameObject.AddComponent<HitProbe>();
        }
    }
}
