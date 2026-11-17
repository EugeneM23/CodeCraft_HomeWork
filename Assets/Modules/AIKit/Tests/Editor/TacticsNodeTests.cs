using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AIKit.Tests
{
    /// <summary>
    /// The blocks of the new fighters that stay after step 90, each on a real agent with a test body: Drop puts down what
    /// the hands hold, hands that hold something do not strike, the weapon goes on the back while the hands carry;
    /// Charge gathers its strength turning slowly, rushes along the line it aimed (short of a wall) and stands for a
    /// while after. (Taking, covering and manning a turret are the objects' own scenarios now: FindTests, InteractTests.)
    /// </summary>
    public sealed class TacticsNodeTests
    {
        private const string TARGET = "Target";
        private const string ROCK = "Rock";
        private const float DT = 0.02f;
        private const float EPSILON = 1e-3f;

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

        // ---------- Drop ----------

        [Test]
        public void Drop_PutsTheThingDownInFrontOfTheFeet_FreeForAnyone_TheHandsAreEmpty()
        {
            GraphRig rig = NewRig(new Vector3(2f, 0f, 0f), out ListFinder finder);
            Transform rock = Rock(finder, Vector3.zero);
            Assert.IsTrue(rock.GetComponent<PickableItem>().GiveTo(rig.Context), "Set-up: in the hands");

            Assert.AreEqual(TaskStatus.Success, new DropTask().Tick(rig.Context, DT));

            Assert.IsNull(rig.Agent.Item, "The hands are free");
            Assert.IsNull(rock.parent);
            Assert.IsNull(rock.GetComponent<PickableItem>().Holder, "Anyone may pick it up again");
            Assert.That(Flat(rock.position - (rig.Actuator.Position + Walking.Forward(rig.Context) * DropTask.AHEAD)).magnitude,
                Is.LessThan(EPSILON), "At the feet, a little in front");
            Assert.IsTrue(new DropTask().IsInstant);
            Assert.AreEqual(TaskStatus.Success, new DropTask().Tick(rig.Context, DT), "Nothing held: nothing to do");
        }

        [Test]
        public void Hands_CarryingSomething_TheWeaponGoesOnTheBack_PutDown_BackInTheHand()
        {
            GraphRig rig = NewRig(Vector3.zero, out ListFinder finder);
            GameObject inHand = NewObject("Sword In Hand", Vector3.zero).gameObject;
            GameObject onBack = NewObject("Sword On Back", Vector3.zero).gameObject;
            WeaponAsset sword = WeaponAsset.Create(new WeaponMove { Name = "Normal" });
            rig.Carry(sword, inHand, onBack);
            rig.Agent.Weapon = sword;
            Assert.IsTrue(inHand.activeSelf && !onBack.activeSelf, "Set-up: the sword is in the hand");

            Transform box = Rock(finder, Vector3.zero);
            Assert.IsTrue(box.GetComponent<PickableItem>().GiveTo(rig.Context), "Set-up: in the hands");
            Assert.IsTrue(!inHand.activeSelf && onBack.activeSelf, "Both hands on the box: the sword on the back");
            Assert.AreSame(sword, rig.Agent.Weapon, "…still the weapon it fights with");

            new DropTask().Tick(rig.Context, DT);
            Assert.IsTrue(inHand.activeSelf && !onBack.activeSelf, "The box put down: the sword in the hand again");
        }

        // ---------- no blow with full hands (the throw is an Attack with a rock: WorldWeaponTests) ----------

        [Test]
        public void Strike_WithSomethingInTheHands_DoesNotStrike_PutDown_ItDoes()
        {
            GraphRig rig = NewRig(Vector3.zero, out ListFinder finder);
            Target(rig, new Vector3(1f, 0f, 0f), out HitProbe hits);
            rig.GiveWeapon(2f, 0.1f, 0.1f);
            Transform box = Rock(finder, Vector3.zero);
            Assert.IsTrue(box.GetComponent<PickableItem>().GiveTo(rig.Context), "Set-up: in the hands");

            Assert.AreEqual(TaskStatus.Failure, new AttackTask { Target = TARGET }.Tick(rig.Context, DT),
                "A box in the hands: no blow (Out Of Reach — the chain puts it down first)");
            Assert.AreEqual(0, hits.Hits);

            new DropTask().Tick(rig.Context, DT);
            var strike = new AttackTask { Target = TARGET };
            Run(rig, strike, 0.5f);
            Assert.AreEqual(1, hits.Hits, "The hands free: the blow lands");
        }

        // ---------- Charge ----------

        [Test]
        public void Charge_GathersStrengthStandingStill_TurningToTheTargetNoFasterThanAimSpeed()
        {
            GraphRig rig = NewRig(Vector3.zero, out _);
            Target(rig, new Vector3(0f, 0f, 10f), out _);           // to the side: the body looks along world forward…
            rig.Agent.transform.rotation = Quaternion.LookRotation(Vector3.right);   // …no: along +x
            var charge = new ChargeTask { Target = TARGET, ChargeTime = 2f, AimSpeed = 30f };
            Vector3 start = rig.Actuator.Position;

            Run(rig, charge, 1f);

            Assert.AreEqual(ChargeTask.Phase.Gathering, charge.PhaseOf(rig.Context));
            Assert.AreEqual(start, rig.Actuator.Position, "It stands while it gathers its strength");
            float turned = Vector3.Angle(Vector3.right, charge.AimOf(rig.Context));
            Assert.That(turned, Is.EqualTo(30f).Within(1f), "Not at once: Aim Speed degrees a second");
            Assert.Less(Vector3.Angle(rig.Actuator.Facing, charge.AimOf(rig.Context)), 1f, "The body looks along the aim");
            Assert.IsTrue(charge.CommitsFor(rig.Context), "A charge is not taken away by the Brain");
        }

        [Test]
        public void Charge_RushesDistanceAlongTheAim_PassesATargetThatSteppedAside_ThenStandsForRecovery()
        {
            GraphRig rig = NewRig(Vector3.zero, out _);
            Transform target = Target(rig, new Vector3(6f, 0f, 0f), out _);
            rig.Agent.transform.rotation = Quaternion.LookRotation(Vector3.right);
            var charge = new ChargeTask
            {
                Target = TARGET, ChargeTime = 1f, AimSpeed = 90f, Distance = 12f, Speed = 12f, Recovery = 2f
            };
            float distance = charge.Distance.Get(rig.Context);
            float rushTime = distance / charge.Speed.Get(rig.Context);

            Run(rig, charge, 1f + DT);
            Assert.AreEqual(ChargeTask.Phase.Rushing, charge.PhaseOf(rig.Context), "Gathered: off it goes");
            target.position = new Vector3(6f, 0f, 3f);                  // the target dodges

            float rushed = 0f;
            Run(rig, charge, rushTime + 0.2f, () => rushed += DT,
                () => charge.PhaseOf(rig.Context) == ChargeTask.Phase.Recovering);
            Assert.AreEqual(ChargeTask.Phase.Recovering, charge.PhaseOf(rig.Context));
            Assert.That(rushed, Is.EqualTo(rushTime).Within(3f * DT), "As long as Distance takes at Speed");
            Assert.That(rig.Actuator.Position.x, Is.EqualTo(distance).Within(0.05f), "Distance metres along the aim");
            Assert.That(Mathf.Abs(rig.Actuator.Position.z), Is.LessThan(EPSILON), "It does not turn after a dodging target");

            Vector3 standing = rig.Actuator.Position;
            float recovered = 0f;
            TaskStatus answer = Run(rig, charge, 3f, () => recovered += DT);
            Assert.AreEqual(TaskStatus.Success, answer, "Done");
            Assert.That(recovered, Is.EqualTo(charge.Recovery.Get(rig.Context)).Within(2f * DT), "It stands for Recovery");
            Assert.AreEqual(standing, rig.Actuator.Position, "…on the spot: the window to strike its back");
        }

        [Test]
        public void Charge_StopsShortOfAWall_NoTarget_CantCharge()
        {
            GraphRig rig = NewRig(Vector3.zero, out _);
            var yard = BoxYard.Square(5f);
            rig.Context.Walk = yard;
            rig.Actuator.Confine = yard.Confine;
            Target(rig, new Vector3(4f, 0f, 0f), out _);
            rig.Agent.transform.rotation = Quaternion.LookRotation(Vector3.right);
            var charge = new ChargeTask { Target = TARGET, ChargeTime = 0.2f, Distance = 12f, Speed = 12f, Recovery = 0f };

            Assert.AreEqual(TaskStatus.Success, Run(rig, charge, 3f));
            Assert.That(rig.Actuator.Position.x, Is.InRange(4.5f, 5f - 0.29f + EPSILON),
                "It stops short of the wall (5 m), not 12 m on");

            GraphRig alone = NewRig(Vector3.zero, out _);
            Assert.AreEqual(TaskStatus.Failure, new ChargeTask { Target = TARGET }.Tick(alone.Context, DT),
                "Nothing to charge at");
        }

        // ---------- the rig ----------

        private GraphRig NewRig(Vector3 at, out ListFinder finder, IAIWorld world = null)
        {
            var rig = new GraphRig(trace: false, world: world);
            if (world != null)
                rig.Agent.gameObject.SetActive(true);       // the world's groups keep the requests of awake agents only
            rig.Actuator.Position = at;
            rig.Agent.transform.position = at;
            finder = new ListFinder();
            rig.Context.Objects = finder;
            return rig;
        }

        private Transform NewObject(string name, Vector3 at)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = at;
            return go.transform;
        }

        private Transform Rock(ListFinder finder, Vector3 at)
        {
            Transform rock = NewObject(ROCK, at);
            rock.gameObject.AddComponent<PickableItem>();
            finder.Add(ROCK, rock);
            return rock;
        }

        private Transform Target(GraphRig rig, Vector3 at, out HitProbe hits)
        {
            Transform target = NewObject(TARGET, at);
            hits = target.gameObject.AddComponent<HitProbe>();
            rig.Agent.Blackboard.Set(TARGET, target);
            return target;
        }

        // The task ticked the way its state ticks it, the body moving as told in between, until it ends or time is out
        private static TaskStatus Run(GraphRig rig, AgentTask task, float seconds, Action eachTick = null,
            Func<bool> until = null)
        {
            TaskStatus answer = TaskStatus.Running;
            int steps = Mathf.CeilToInt(seconds / DT - 1e-4f);
            for (int i = 0; i < steps && answer == TaskStatus.Running; i++)
            {
                rig.Context.Advance(DT);
                answer = task.Tick(rig.Context, DT);
                eachTick?.Invoke();
                rig.Actuator.Apply(DT);
                if (until != null && until())
                    break;
            }

            return answer;
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
