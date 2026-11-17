using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The registry of the objects of the world (step 90; handbook §24 — one gateway instead of scanning): objects are
    /// in it because they registered themselves; a query finds them by component, by AI Object kind, by tag, within a
    /// radius; whose they are follows §24's chain (mine → public → anyone; an author's list never falls through); a claim
    /// has one holder, runs out on the world's clock unless renewed, and goes when the holder's decision leaves or the
    /// holder is gone; the unreachable journal keeps an object from a character for a while.
    /// </summary>
    public sealed class ObjectTests
    {
        private const float DT = 0.02f;

        private readonly List<Object> _created = new();
        private readonly List<WorldObject> _found = new();

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

        // ---------- registering and finding ----------

        [Test]
        public void Registry_AnObjectRegistersItselfWhenOn_LeavesWhenOff()
        {
            var turnedOn = Make<SwitchedObject>("Switched", Vector3.zero);

            turnedOn.TurnOn();
            CollectionAssert.Contains(WorldObjects.Scene.All, turnedOn, "On: in the scene's registry");

            turnedOn.TurnOff();
            CollectionAssert.DoesNotContain(WorldObjects.Scene.All, turnedOn, "Off: gone from it");
        }

        [Test]
        public void Registry_FindsByComponentKindAndTag_WithinTheRadius_OnlyWhatRegistered()
        {
            var registry = new WorldObjects();
            var cover = Make<Cover>("Crate", new Vector3(3f, 0f, 0f));
            var well = Make<AIObject>("Well", new Vector3(5f, 0f, 0f));
            well.Kind = "Well";
            var home = Make<AIObject>("Home", new Vector3(40f, 0f, 0f));
            home.Kind = "Home";
            var rock = Make<PickableItem>("Rock", new Vector3(2f, 0f, 2f));
            rock.gameObject.tag = "Player";                        // a tag every project has
            var stray = Make<Cover>("Stray Crate", new Vector3(1f, 0f, 0f));   // never registered
            foreach (WorldObject registered in new WorldObject[] { cover, well, home, rock })
                registry.Register(registered);

            Assert.That(Find(registry, new ObjectQuery { Component = typeof(Cover), Radius = 10f }),
                Is.EquivalentTo(new WorldObject[] { cover }), "By component; the crate that did not register is not seen");
            Assert.That(Find(registry, new ObjectQuery { Kind = "Home" }), Is.EquivalentTo(new WorldObject[] { home }),
                "By kind, anywhere (no radius)");
            Assert.That(Find(registry, new ObjectQuery { Kind = "Home", Radius = 10f }), Is.Empty, "…but not out of the radius");
            Assert.That(Find(registry, new ObjectQuery { Tag = "Player", Radius = 10f }),
                Is.EquivalentTo(new WorldObject[] { rock }), "By tag");
            Assert.That(Find(registry, new ObjectQuery { Component = typeof(AIObject), Radius = 10f }),
                Is.EquivalentTo(new WorldObject[] { well }), "AI Objects within 10 m");
            Assert.That(stray, Is.Not.Null);

            well.gameObject.SetActive(false);
            Assert.That(Find(registry, new ObjectQuery { Kind = "Well" }), Is.Empty, "An object switched off is not found");
        }

        [Test]
        public void Registry_Whose_MineFirst_PublicWhenIOwnNone_AnAuthorsListNeverFallsThrough()
        {
            GraphRig me = new GraphRig(trace: false);
            GraphRig other = new GraphRig(trace: false);
            var mine = Make<AIObject>("Mine", new Vector3(30f, 0f, 0f));
            var common = Make<AIObject>("Common", new Vector3(2f, 0f, 0f));
            var theirs = Make<AIObject>("Theirs", new Vector3(1f, 0f, 0f));
            mine.Owners.Add(me.Agent);
            theirs.Owners.Add(other.Agent);
            var candidates = new List<WorldObject> { mine, common, theirs };

            Assert.AreEqual(0, WorldObjects.TierOf(mine, me.Agent));
            Assert.AreEqual(1, WorldObjects.TierOf(common, me.Agent));
            Assert.AreEqual(2, WorldObjects.TierOf(theirs, me.Agent));

            Assert.AreEqual(0, WorldObjects.AllowedTier(Ownership.MineOrPublic, candidates, me.Agent),
                "I own one of the kind: only mine — even if it is far or taken");
            Assert.AreEqual(1, WorldObjects.AllowedTier(Ownership.MineOrPublic, new List<WorldObject> { common, theirs }, me.Agent),
                "I own none: public ones too, never another's");
            Assert.AreEqual(0, WorldObjects.AllowedTier(Ownership.Mine, new List<WorldObject> { common }, me.Agent));
            Assert.AreEqual(2, WorldObjects.AllowedTier(Ownership.Anyone, candidates, me.Agent), "Anyone: anybody's");
        }

        // ---------- claims ----------

        [Test]
        public void Claim_OneHolder_RunsOutOnTheWorldsClock_UnlessRenewed()
        {
            var world = new AIWorld { Objects = new WorldObjects() };
            WorldObjects registry = world.Objects;
            GraphRig first = new GraphRig(trace: false, world: world);
            GraphRig second = new GraphRig(trace: false, world: world);
            var rock = Make<PickableItem>("Rock", Vector3.zero);
            registry.Register(rock);

            Assert.IsTrue(registry.TryClaim(rock, first.Agent, 1f), "The first goes for it");
            Assert.IsFalse(registry.TryClaim(rock, second.Agent, 1f), "One holder: the second looks for another");
            Assert.IsTrue(registry.IsClaimedByAnother(rock, second.Agent));
            Assert.IsFalse(registry.IsClaimedByAnother(rock, first.Agent));

            Steps(world, 0.6f);
            Assert.IsTrue(registry.Renew(rock, first.Agent, 1f), "Still used: renewed");
            Steps(world, 0.6f);
            Assert.AreSame(first.Agent, registry.HolderOf(rock), "1.2 s after the claim — the renewal keeps it");

            Steps(world, 1.1f);
            Assert.IsNull(registry.HolderOf(rock), "Not renewed for longer than its TTL: nobody's");
            Assert.IsTrue(registry.TryClaim(rock, second.Agent), "The second may have it now");
            Assert.IsFalse(registry.Renew(rock, first.Agent), "The first holds it no more");
        }

        [Test]
        public void Claim_TheHoldersDecisionLeaves_EveryClaimOfItGoes_AHolderThatIsGoneHoldsNothing()
        {
            var registry = new WorldObjects();
            GraphRig rig = new GraphRig(trace: false) { ThinkInterval = 0f };
            rig.UseObjects(registry);
            var rock = Make<PickableItem>("Rock", new Vector3(1f, 0f, 0f));
            var crate = Make<Cover>("Crate", new Vector3(2f, 0f, 0f));
            registry.Register(rock);
            registry.Register(crate);

            // A decision that holds once, then another takes over: its leaving lets the claims go
            var flag = new FlagCondition();
            var g = new BrainGraphBuilder();
            AgentState search = g.Decision(new DailyRoutineReasoner { Condition = flag }, 1);
            g.Do(search, g.Block(new WaitTask(100f)));
            g.Do(g.Decision(new PatrolReasoner(), 2), g.Block(new WaitTask(100f)));
            rig.SetGraph(g.Build());
            flag.Value = true;
            rig.Step(DT, 2);                                        // the first think starts the graph, the next decides
            Assert.AreSame(search, rig.Instance.Decision, "Set-up: the first decision is in charge");
            Assert.IsTrue(registry.TryClaim(rock, rig.Agent) && registry.TryClaim(crate, rig.Agent));

            flag.Value = false;
            rig.StepUntil(() => rig.Instance.Decision != search, 1f, DT);

            Assert.AreNotSame(search, rig.Instance.Decision, "Set-up: the decision left");
            Assert.IsNull(registry.HolderOf(rock), "Its claims went with it");
            Assert.IsNull(registry.HolderOf(crate));

            flag.Value = true;
            Assert.IsTrue(rig.StepUntil(() => rig.Instance.Decision == search, 1f, DT), "Set-up: the first decision again");
            Assert.IsTrue(registry.TryClaim(rock, rig.Agent));
            rig.Instance.Stop(rig.Context);                         // it dies: its character goes off, its graph stops
            Assert.IsNull(registry.HolderOf(rock), "Its claims go with its death");

            GraphRig dead = new GraphRig(trace: false);
            Assert.IsTrue(registry.TryClaim(rock, dead.Agent));
            Object.DestroyImmediate(dead.Agent.gameObject);
            Assert.IsNull(registry.HolderOf(rock), "A holder that is gone holds nothing");
        }

        // ---------- the unreachable journal ----------

        [Test]
        public void Unreachable_AnObjectWithNoWayToIt_IsNotOfferedToThatCharacterForAWhile()
        {
            var world = new AIWorld { Objects = new WorldObjects() };
            WorldObjects registry = world.Objects;
            GraphRig me = new GraphRig(trace: false, world: world);
            GraphRig other = new GraphRig(trace: false, world: world);
            var rock = Make<PickableItem>("Rock", Vector3.zero);
            registry.Register(rock);
            registry.TryClaim(rock, me.Agent);

            registry.ReportUnreachable(me.Agent, rock, 2f);

            Assert.IsTrue(registry.IsUnreachableFor(me.Agent, rock));
            Assert.IsFalse(registry.IsUnreachableFor(other.Agent, rock), "Only for the one that found no way");
            Assert.IsNull(registry.HolderOf(rock), "Its claim is let go");

            Steps(world, 1.9f);
            Assert.IsTrue(registry.IsUnreachableFor(me.Agent, rock));
            Steps(world, 0.2f);
            Assert.IsFalse(registry.IsUnreachableFor(me.Agent, rock), "The entry ran out: tried again");
        }

        [Test]
        public void World_StepsItsObjectsClock()
        {
            var world = new AIWorld { Objects = new WorldObjects() };
            float before = world.Objects.Clock;

            world.BeginStep(0.5f);

            Assert.AreEqual(before + 0.5f, world.Objects.Clock, 1e-5f, "Claims count by the world's steps");
            Assert.AreSame(WorldObjects.Scene, new AIWorld().Objects, "A world uses the scene's registry unless given one");
        }

        // ---------- helpers ----------

        private sealed class FlagCondition : AgentCondition
        {
            public bool Value;

            protected override bool Evaluate(AgentContext context) => Value;
        }

        private List<WorldObject> Find(WorldObjects registry, ObjectQuery query)
        {
            registry.Find(query, _found);
            return new List<WorldObject>(_found);
        }

        private static void Steps(AIWorld world, float seconds)
        {
            for (float passed = 0f; passed < seconds - 1e-4f; passed += DT)
                world.BeginStep(DT);
        }

        private T Make<T>(string name, Vector3 at) where T : Component
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = at;
            return go.AddComponent<T>();
        }
    }
}
