using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Enters Zone (step 90, the user's order of 13.11 — "someone enters a zone"): the Brain's target steps into a zone of
    /// the world (an AI Object of a kind, with an area) and the reaction is taken out of turn; it holds while the target is
    /// in, with slack on the edge, and lets go when it leaves.
    /// </summary>
    public sealed class EntersZoneTests
    {
        private const string TARGET = "Target";
        private const float STEP = 0.05f;
        private const float RADIUS = 3f;

        private readonly List<Object> _created = new();
        private GraphRig _rig;
        private GameObject _target;
        private WorldObjects _registry;
        private AIObject _yard;

        [SetUp]
        public void SetUp()
        {
            _rig = new GraphRig(3, trace: false) { ThinkInterval = 0f };
            _rig.Context.ExplainsDecisions = true;
            _registry = new WorldObjects();
            _rig.Context.WorldObjects = _registry;

            var yard = new GameObject("Yard") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(yard);
            yard.transform.position = new Vector3(10f, 0f, 0f);
            _yard = yard.AddComponent<AIObject>();
            _yard.Kind = "Yard";
            _yard.ZoneRadius = RADIUS;
            _registry.Register(_yard);

            _target = new GameObject("Player") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(_target);
            _target.transform.position = new Vector3(20f, 0f, 0f);         // well out of the yard
        }

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
        public void EntersZone_TheTargetStepsIntoTheYard_TakenAtOnce_LetGoWhenItLeaves()
        {
            var reaction = new EntersZoneReaction { Zone = "Yard", ReactionTime = 0f, ReactionTimeMax = 0f, Cooldown = 0f };
            Run(reaction);
            Assert.IsTrue(_rig.StepUntil(() => _rig.StateName == "Rest", 1f, STEP), "Out of the yard: the patrol");

            _target.transform.position = _yard.transform.position + new Vector3(RADIUS - 1f, 0f, 0f);
            Assert.IsTrue(_rig.StepUntil(() => _rig.StateName == "Shout", 1f, STEP),
                "In the yard: the reaction is taken at once");

            _target.transform.position = _yard.transform.position + new Vector3(RADIUS + reaction.Slack + 2f, 0f, 0f);
            Assert.IsTrue(_rig.StepUntil(() => _rig.StateName == "Rest", 2f, STEP), "Gone out of the yard: let go");
        }

        [Test]
        public void EntersZone_OnTheEdge_TheSlackKeepsIt_NoFlicker()
        {
            var reaction = new EntersZoneReaction { Zone = "Yard", Slack = 1f };
            Vector3 inside = _yard.transform.position + new Vector3(RADIUS - 0.2f, 0f, 0f);
            Vector3 onTheSlack = _yard.transform.position + new Vector3(RADIUS + 0.5f, 0f, 0f);

            Assert.IsTrue(reaction.InAZone(_rig.Context, inside, 0f), "Inside the yard");
            Assert.IsFalse(reaction.InAZone(_rig.Context, onTheSlack, 0f), "Just out — not in, when not caught yet");
            Assert.IsTrue(reaction.InAZone(_rig.Context, onTheSlack, reaction.Slack), "…but still in with the slack");
        }

        [Test]
        public void EntersZone_AZoneOfAnotherKind_OrNoAreaAtAll_IsNoZoneOfIt()
        {
            var other = new GameObject("Camp") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(other);
            other.transform.position = new Vector3(-10f, 0f, 0f);
            var camp = other.AddComponent<AIObject>();
            camp.Kind = "Camp";
            camp.ZoneRadius = RADIUS;
            _registry.Register(camp);
            var well = new GameObject("Well") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(well);
            var spot = well.AddComponent<AIObject>();
            spot.Kind = "Yard";                             // a yard marker with no area: a place, not a zone
            _registry.Register(spot);

            var reaction = new EntersZoneReaction { Zone = "Yard" };
            Assert.IsFalse(reaction.InAZone(_rig.Context, other.transform.position, 0f), "The camp is not a yard");
            Assert.IsFalse(reaction.InAZone(_rig.Context, well.transform.position, 0f), "A place with no area is no zone");
            Assert.IsTrue(new EntersZoneReaction().InAZone(_rig.Context, other.transform.position, 0f),
                "No kind given: any AI Object with an area");
        }

        [Test]
        public void EntersZone_WhoseMine_AnotherCharactersYard_IsNotWatched()
        {
            var stranger = new GameObject("Stranger") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(stranger);
            _yard.Owners.Add(stranger.AddComponent<AICharacter>());

            var mine = new EntersZoneReaction { Zone = "Yard", Whose = Ownership.Mine };
            var anyone = new EntersZoneReaction { Zone = "Yard", Whose = Ownership.Anyone };
            Assert.IsFalse(mine.InAZone(_rig.Context, _yard.transform.position, 0f), "Mine only: someone else's yard");
            Assert.IsTrue(anyone.InAZone(_rig.Context, _yard.transform.position, 0f), "Anyone's: it is watched");
        }

        private void Run(ReasonerTask reaction)
        {
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            g.Do(g.Decision(reaction, 1), g.Block(new WaitTask(100f), "Shout"));
            g.Do(g.Decision(new PatrolReasoner(), 2), g.Block(new WaitTask(100f), "Rest"));
            _rig.SetGraph(g.Build());
            _rig.Agent.Blackboard.Set(TARGET, _target.transform);
        }
    }
}
