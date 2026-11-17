using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Interact while its object's scenario runs (step 90, C15; handbook §24.1 — the object owns the scenario, §20.1 —
    /// what the body was asked to show is ended by whoever asked): an object that goes in the middle (destroyed, pooled)
    /// fails the block, and the action it asked the body to show is dropped at once — the body does not keep loading a
    /// turret that is no more. A scenario that lasts longer than a claim's own life keeps its claim while it runs: nobody
    /// else is sent for the thing being taken.
    /// </summary>
    public sealed class WorldInteractTests
    {
        private const string OBJECT = "Object";
        private const string FOUND = "Found";
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

        [Test]
        public void Interact_TheObjectGoesInTheMiddleOfItsScenario_Failed_ItsActionIsEnded()
        {
            // A turret loaded with the box in the hands, destroyed half way through the loading
            var turret = Make<Turret>("Turret", new Vector3(10f, 0f, 0f));
            turret.transform.rotation = Quaternion.LookRotation(Vector3.right);
            turret.Weapon = Weapon();
            turret.Capacity = 10;
            turret.Ammo = 0;
            turret.LoadTime = 1f;
            turret.LoadAction = "Load";
            GraphRig loader = Rig(turret.SeatPosition);
            loader.Agent.Hold(Make<Transform>("Box", Vector3.zero));
            var watch = new CharacterActionsWatch(loader.Context);
            loader.Context.Blackboard.Set(OBJECT, turret.transform);
            var load = new InteractTask { Object = OBJECT, Interaction = Offer.Load };
            Assert.AreEqual(TaskStatus.Running, Run(loader, load, 0.5f), "Set-up: loading");
            Assert.AreEqual(new[] { "Load" }, watch.Played.ToArray(), "Set-up: the body shows the loading");

            Object.DestroyImmediate(turret.gameObject);
            loader.Context.Advance(DT);
            TaskStatus gone = load.Tick(loader.Context, DT);
            load.Interrupt(loader.Context);

            Assert.AreEqual(TaskStatus.Failure, gone, "The object went in the middle: Failed");
            Assert.AreEqual(ActionState.Kind.None, loader.Context.Actions.Current, "The loading is not shown any more");
            Assert.AreEqual(1, watch.Drops, "…dropped at once, once");

            // A thing being picked up, destroyed half way through the taking
            var thing = Make<PickableItem>("Box 2", new Vector3(0.5f, 0f, 0f));
            thing.TakeTime = 1f;
            thing.TakeAction = "Pick Up";
            GraphRig taker = Rig(Vector3.zero);
            var picking = new CharacterActionsWatch(taker.Context);
            taker.Context.Blackboard.Set(OBJECT, thing.transform);
            var take = new InteractTask { Object = OBJECT, Interaction = Offer.Take };
            Assert.AreEqual(TaskStatus.Running, Run(taker, take, 0.5f), "Set-up: taking");
            Assert.AreEqual(new[] { "Pick Up" }, picking.Played.ToArray());

            Object.DestroyImmediate(thing.gameObject);
            taker.Context.Advance(DT);

            Assert.AreEqual(TaskStatus.Failure, take.Tick(taker.Context, DT));
            Assert.AreEqual(ActionState.Kind.None, taker.Context.Actions.Current);
            Assert.AreEqual(1, picking.Drops);
            Assert.IsNull(taker.Agent.Item, "Nothing came into the hands");
        }

        [Test]
        public void Interact_TheObjectSwitchedOffInTheMiddle_Failed_ItsActionIsEnded()
        {
            var thing = Make<PickableItem>("Box", new Vector3(0.5f, 0f, 0f));
            thing.TakeTime = 1f;
            thing.TakeAction = "Pick Up";
            GraphRig taker = Rig(Vector3.zero);
            var watch = new CharacterActionsWatch(taker.Context);
            taker.Context.Blackboard.Set(OBJECT, thing.transform);
            var take = new InteractTask { Object = OBJECT, Interaction = Offer.Take };
            Assert.AreEqual(TaskStatus.Running, Run(taker, take, 0.5f), "Set-up: taking");

            thing.gameObject.SetActive(false);                              // pooled away
            TaskStatus answer = Run(taker, take, 1f);

            Assert.AreEqual(TaskStatus.Failure, answer, "Gone from the world: Failed, not taken");
            Assert.IsNull(taker.Agent.Item, "Nothing came into the hands");
            Assert.AreEqual(ActionState.Kind.None, taker.Context.Actions.Current);
            Assert.AreEqual(1, watch.Drops, "The taking was dropped at once");
        }

        [Test]
        public void Interact_ATakeLongerThanAClaimsLife_KeepsTheClaim()
        {
            var world = new AIWorld { Objects = new WorldObjects() };
            GraphRig taker = new GraphRig(trace: false, world: world);
            taker.WakeUpAt(Vector3.zero);
            GraphRig late = new GraphRig(trace: false, world: world);
            late.WakeUpAt(new Vector3(1f, 0f, 1f));
            var box = Make<PickableItem>("Box", new Vector3(0.5f, 0f, 0f));
            box.TakeTime = WorldObjects.CLAIM_TTL + 1f;
            world.Objects.Register(box);
            var what = new WhatValue { Offer = Offer.Take };
            Assert.AreEqual(TaskStatus.Success, new FindTask { What = what, StoreIn = FOUND }.Tick(taker.Context, DT),
                "Set-up: found and claimed");
            Assert.AreSame(taker.Agent, world.Objects.HolderOf(box), "Set-up: its claim");
            var take = new InteractTask { Object = FOUND, Interaction = Offer.Take };
            var lateFind = new FindTask { What = what, StoreIn = FOUND };

            TaskStatus answer = TaskStatus.Running;
            float took = 0f;
            float lookedAt = WorldObjects.CLAIM_TTL + 0.5f;
            bool looked = false;
            for (int i = 0; i < 400 && answer == TaskStatus.Running; i++)
            {
                world.BeginStep(DT);
                taker.Context.Advance(DT);
                answer = take.Tick(taker.Context, DT);
                took += DT;
                if (!looked && took >= lookedAt)
                {
                    looked = true;
                    Assert.AreSame(taker.Agent, world.Objects.HolderOf(box),
                        $"{lookedAt} s in — past a claim's own {WorldObjects.CLAIM_TTL} s: still the taker's");
                    Assert.AreEqual(TaskStatus.Failure, lateFind.Tick(late.Context, DT),
                        "Another one's search is not sent for it");
                }
            }

            Assert.IsTrue(looked, "Set-up: looked half way through");
            Assert.AreEqual(TaskStatus.Success, answer, "Taken");
            Assert.That(took, Is.EqualTo(box.TakeTime).Within(2f * DT), "…after its take time");
            Assert.AreSame(box.transform, taker.Agent.Item);
        }

        // ---------- helpers ----------

        private GraphRig Rig(Vector3 at)
        {
            var rig = new GraphRig(trace: false);
            rig.WakeUpAt(at);
            rig.UseObjects(new WorldObjects());
            rig.UseWorldRack();
            return rig;
        }

        private WeaponAsset Weapon()
        {
            WeaponAsset weapon = WeaponAsset.Create(new WeaponMove { Name = "Attack 1", Reach = 20f });
            _created.Add(weapon);
            return weapon;
        }

        private T Make<T>(string name, Vector3 at) where T : Component
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = at;
            return typeof(T) == typeof(Transform) ? go.transform as T : go.AddComponent<T>();
        }

        private static TaskStatus Run(GraphRig rig, AgentTask task, float seconds)
        {
            TaskStatus answer = TaskStatus.Running;
            int steps = Mathf.CeilToInt(seconds / DT);
            for (int i = 0; i < steps && answer == TaskStatus.Running; i++)
            {
                rig.Context.Advance(DT);
                answer = task.Tick(rig.Context, DT);
                rig.Actuator.Apply(DT);
            }

            return answer;
        }
    }
}
