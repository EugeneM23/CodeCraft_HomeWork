using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace AIKit.Tests
{
    /// <summary>
    /// A squad of twenty (step 88): an AI Squad runs its command graph — Squad Sight → Squad Brain → Surround / Hold The
    /// Line / Advance / Form Up, each leading the formation by its block — and every member runs its own graph, keeping
    /// its slot by Follow Squad → Keep Formation → Strike. They form two ranks of ten at the banner; a foe far off — they
    /// march on it in their ranks; a lone foe — the ranks become rings round it, the front rank strikes by turns; the foe
    /// runs — the ranks form again and march after it; a group of foes — the line holds, only the front rank strikes; a
    /// man of the front falls — the one behind him steps up; a member's own more important decision takes it out of the
    /// ranks, and it comes back to its slot when that lets go.
    /// </summary>
    public sealed class SquadTests
    {
        private const string TARGET = "Target";
        private const float DT = 0.02f;
        private const int FILES = 10;
        private const float SPACING = 1.8f;
        private const float RANK_GAP = 2f;
        private const float REACH = 3.4f;

        private readonly List<GameObject> _created = new();
        private readonly List<GraphRig> _rigs = new();
        private AIWorld _world;
        private AISquad _squad;
        private BehaviorGraph _members;

        [SetUp]
        public void SetUp()
        {
            _world = new AIWorld();
            _squad = Make("Squad", Vector3.zero).AddComponent<AISquad>();
            _squad.ThinkInterval = 0f;
            _squad.SetGraph(CommandGraph());
            _members = MemberGraph();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _created.Clear();
            _rigs.Clear();
            GraphRig.DestroyAll();
        }

        [Test]
        public void Squad_TwentyScattered_FormTwoRanksOfTenAtTheBanner_FacingItsWay()
        {
            var random = new System.Random(7);
            for (int i = 0; i < 20; i++)
                Member(new Vector3((float)random.NextDouble() * 14f - 7f, 0f, (float)random.NextDouble() * 10f - 7f));

            Run(10f);

            Squad squad = _squad.Squad;
            Assert.IsInstanceOf<FormUpDecision>(_squad.Graph.Decision?.Reasoner, "Nobody to fight: Form Up");
            Assert.IsInstanceOf<ReturnToBannerTask>(_squad.Graph.Current?.Tasks[0], "…its block leads the formation");
            Assert.AreEqual(SquadShape.Phalanx, squad.Shape);
            Assert.That(Vector3.Angle(squad.Facing, _squad.transform.forward), Is.LessThan(1f), "Facing the banner's way");
            int front = 0;
            foreach (GraphRig rig in _rigs)
            {
                Assert.IsInstanceOf<KeepFormationTask>(rig.Instance.Current?.Tasks[0], "Each keeps its slot by its own graph");
                Assert.IsTrue(squad.SlotOf(rig.Context, out Vector3 slot, out _, out _, out bool isFront));
                Assert.That(Flat(slot - rig.Context.Position).magnitude, Is.LessThan(0.5f), "Every one on its slot");
                float along = Vector3.Dot(slot - _squad.transform.position, _squad.transform.forward);
                Assert.That(along, Is.EqualTo(isFront ? 0f : -RANK_GAP).Within(0.01f), "The front rank at the banner, the rear one behind it");
                if (isFront)
                    front++;
            }

            Assert.AreEqual(FILES, front, "Ten in the front rank");
            float width = _rigs.Max(rig => Vector3.Dot(rig.Context.Position, _squad.transform.right)) -
                          _rigs.Min(rig => Vector3.Dot(rig.Context.Position, _squad.transform.right));
            Assert.That(width, Is.EqualTo((FILES - 1) * SPACING).Within(1f), "A rank ten files wide");
        }

        [Test]
        public void Squad_AFoeFarOff_MarchesOnItInItsRanks_NobodyRunsAhead()
        {
            Formed();
            Transform foe = Foe(new Vector3(3f, 0f, 30f));
            SeeAll(foe);

            Squad squad = _squad.Squad;
            int steps = 0;
            int together = 0;
            float mostAhead = 0f;
            bool marched = false;
            Run(20f, () =>
            {
                if (_squad.Graph.Decision?.Reasoner is not AdvanceDecision)
                    return _squad.Graph.Decision?.Reasoner is SurroundDecision;

                marched |= _squad.Graph.Current?.Tasks[0] is MarchToTargetTask;
                if (squad.Now > 2f)
                {
                    steps++;
                    if (squad.Cohesion >= 0.8f)
                        together++;
                }

                foreach (GraphRig rig in _rigs)
                    mostAhead = Mathf.Max(mostAhead, squad.AheadOfFront(rig.Context.Position));
                return false;
            });

            Assert.IsTrue(marched, "Advance: March To Target led the formation");
            Assert.IsInstanceOf<SurroundDecision>(_squad.Graph.Decision?.Reasoner, "Came up to the lone foe");
            Assert.That(together / (float)Mathf.Max(1, steps), Is.GreaterThan(0.9f), "In its ranks on the march");
            Assert.That(mostAhead, Is.LessThan(1f), "Nobody runs ahead of the front rank");
        }

        [Test]
        public void Squad_ALoneFoe_SurroundedByTheFrontRank_TheRearRankBehindIt_StruckByTurns()
        {
            Formed();
            Transform foe = Foe(new Vector3(0f, 0f, 8f));
            HitProbe hits = foe.gameObject.AddComponent<HitProbe>();
            SeeAll(foe);

            int mostStriking = 0;
            Squad squad = _squad.Squad;
            Run(14f, () =>
            {
                mostStriking = Mathf.Max(mostStriking, squad.StrikersAt(foe));
                return false;
            });

            Assert.IsInstanceOf<SurroundDecision>(_squad.Graph.Decision?.Reasoner);
            Assert.IsInstanceOf<FormRingTask>(_squad.Graph.Current?.Tasks[0]);
            Assert.AreEqual(SquadShape.Ring, squad.Shape);
            var front = new List<float>();
            foreach (GraphRig rig in _rigs)
            {
                Assert.IsTrue(squad.SlotOf(rig.Context, out _, out _, out _, out bool isFront));
                Vector3 off = Flat(rig.Context.Position - foe.position);
                float expected = squad.RingRadius + (isFront ? 0f : RANK_GAP);
                Assert.That(off.magnitude, Is.EqualTo(expected).Within(1f), $"{(isFront ? "Inner" : "Outer")} ring");
                if (isFront)
                    front.Add(Mathf.Repeat(Mathf.Atan2(off.x, off.z) * Mathf.Rad2Deg, 360f));
            }

            Assert.AreEqual(FILES, front.Count, "The front rank is the inner ring");
            front.Sort();
            float widest = 360f - front[front.Count - 1] + front[0];
            for (int i = 1; i < front.Count; i++)
                widest = Mathf.Max(widest, front[i] - front[i - 1]);
            Assert.That(widest, Is.LessThan(360f / FILES + 20f), "All round the foe, no side left open");
            Assert.That(hits.Hits, Is.GreaterThan(3), "The front rank strikes");
            Assert.That(mostStriking, Is.LessThanOrEqualTo(3), "No more than Strikers Per Foe at once");
        }

        [Test]
        public void Squad_TheSurroundedFoeRunsOff_TheRanksFormAgainAndMarchAfterIt()
        {
            Formed();
            Transform foe = Foe(new Vector3(0f, 0f, 8f));
            SeeAll(foe);
            Run(10f);
            Squad squad = _squad.Squad;
            Assume.That(squad.Shape, Is.EqualTo(SquadShape.Ring), "Set-up: surrounded");

            // The foe runs out of the rings, 5 m/s, and stops far off
            Vector3 away = new(25f, 0f, 30f);
            bool pursued = false;
            Run(5f, () =>
            {
                foe.position = Vector3.MoveTowards(foe.position, away, 5f * DT);
                pursued |= _squad.Graph.Decision?.Reasoner is AdvanceDecision && squad.Shape == SquadShape.Phalanx;
                return false;
            });

            Assert.IsTrue(pursued, "The pursuit is Advance, in ranks");
            int steps = 0;
            int together = 0;
            Run(6f, () =>
            {
                if (_squad.Graph.Decision?.Reasoner is AdvanceDecision)
                {
                    steps++;
                    if (squad.Cohesion >= 0.8f)
                        together++;
                }

                return false;
            });

            Assert.That(steps, Is.GreaterThan(0), "Still marching after it");
            Assert.That(together / (float)steps, Is.GreaterThan(0.7f), "The ranks keep together on the pursuit");
            Assert.That(Vector3.Angle(squad.Facing, Flat(foe.position - squad.Centre)), Is.LessThan(20f), "Facing it");
        }

        [Test]
        public void Squad_AgainstAGroup_HoldsTheLine_OnlyTheFrontRankStrikes_NobodyWalksIntoTheFoes()
        {
            Formed();
            var foes = new List<Transform>();
            var hits = new List<HitProbe>();
            for (int i = 0; i < 6; i++)
            {
                Transform foe = Foe(new Vector3((i - 2.5f) * 1.8f, 0f, 14f));
                hits.Add(foe.gameObject.AddComponent<HitProbe>());
                foes.Add(foe);
            }

            // Each member sees the foe nearest to it (its Detection would)
            foreach (GraphRig rig in _rigs)
                rig.Agent.Blackboard.Set(TARGET, foes.OrderBy(f => Flat(f.position - rig.Context.Position).sqrMagnitude).First());

            Squad squad = _squad.Squad;
            int steps = 0;
            int together = 0;
            float nearest = float.MaxValue;
            bool rearStruck = false;
            Run(16f, () =>
            {
                if (_squad.Graph.Decision?.Reasoner is HoldLineDecision)
                {
                    steps++;
                    if (squad.Cohesion >= 0.8f)
                        together++;
                }

                foreach (GraphRig rig in _rigs)
                {
                    squad.SlotOf(rig.Context, out _, out _, out _, out bool isFront);
                    if (!isFront && rig.Instance.Current?.Tasks[0] is AttackTask)
                        rearStruck = true;
                }

                foreach (Transform foe in foes)
                    nearest = Mathf.Min(nearest, squad.AheadOfFront(foe.position));
                return false;
            });

            Assert.IsInstanceOf<HoldLineDecision>(_squad.Graph.Decision?.Reasoner, "A group: the line holds");
            Assert.IsInstanceOf<HoldLineTask>(_squad.Graph.Current?.Tasks[0]);
            Assert.AreEqual(SquadShape.Phalanx, squad.Shape, "In ranks, not round one of them");
            Assert.That(together / (float)Mathf.Max(1, steps), Is.GreaterThan(0.8f), "The ranks hold");
            Assert.That(nearest, Is.GreaterThan(1.5f), "The front stops before the foes, it does not walk into them");
            Assert.That(hits.Count(h => h.Hits > 0), Is.GreaterThanOrEqualTo(3), "The front rank strikes the foes in front of it");
            Assert.IsFalse(rearStruck, "The rear rank does not strike");
        }

        [Test]
        public void Squad_AManOfTheFrontFalls_TheOneBehindHimStepsUp()
        {
            Formed();
            Run(2f);
            Squad squad = _squad.Squad;
            GraphRig fallen = _rigs.First(rig => squad.SlotIndexOf(rig.Context) == 4);
            GraphRig behind = _rigs.First(rig => squad.SlotIndexOf(rig.Context) == 4 + FILES);
            Assert.IsTrue(squad.SlotOf(fallen.Context, out Vector3 hole, out _, out _, out _));

            fallen.Agent.enabled = false;               // it falls: its character is switched off
            Run(4f);

            Assert.IsFalse(squad.Has(fallen.Context), "The fallen one has left the squad");
            Assert.AreEqual(4, squad.SlotIndexOf(behind.Context), "The one behind took his place");
            Assert.That(Flat(behind.Context.Position - hole).magnitude, Is.LessThan(0.6f), "…and stands there");
            Assert.AreEqual(FILES, _rigs.Count(rig => rig != fallen && squad.SlotIndexOf(rig.Context) < FILES),
                "The front rank is full");
        }

        [Test]
        public void Squad_AMembersOwnMoreImportantDecision_TakesItOutOfTheRanks_ItComesBackToItsSlot()
        {
            Formed();
            Transform far = Foe(new Vector3(0f, 0f, 40f));
            SeeAll(far);
            Run(1f);

            // One of them fetches things: its own search (empty hands, a thing to carry within 8 m) matters more than
            // the ranks
            GraphRig thrower = _rigs[3];
            WorldObjects registry = thrower.UseObjects();
            const string FOUND = "Found";
            thrower.SetGraph(MemberGraph(new SearchForReasoner { Condition = new CarriesSomething { Not = true } },
                new FindTask { What = new WhatValue { Offer = Offer.Take, Radius = 8f }, StoreIn = FOUND },
                new MoveToTask { Target = FOUND }, new InteractTask { Object = FOUND, Interaction = Offer.Take }));
            GameObject rock = Make("Rock", thrower.Context.Position + new Vector3(-6f, 0f, 1f));
            var thing = rock.AddComponent<PickableItem>();
            thing.TakeTime = 0.3f;
            registry.Register(thing);

            Squad squad = _squad.Squad;
            float farthest = 0f;
            bool othersKept = true;
            Run(8f, () =>
            {
                if (thrower.Instance.Decision?.Reasoner is SearchForReasoner && squad.SlotOf(thrower.Context, out Vector3 at, out _, out _, out _))
                {
                    farthest = Mathf.Max(farthest, Flat(at - thrower.Context.Position).magnitude);
                    othersKept &= _rigs.Where(rig => rig != thrower).All(rig => rig.Instance.Current?.Tasks[0] is KeepFormationTask);
                }

                return thrower.Agent.Item != null;
            });

            Assert.IsNotNull(thrower.Agent.Item, "It went for the rock and took it");
            Assert.That(farthest, Is.GreaterThan(2f), "It left the ranks for it");
            Assert.IsTrue(othersKept, "The others kept their slots meanwhile");
            Assert.IsInstanceOf<AdvanceDecision>(_squad.Graph.Decision?.Reasoner, "The squad marched on");

            Run(5f);

            Assert.IsInstanceOf<FollowSquadReasoner>(thrower.Instance.Decision?.Reasoner, "Hands full: back to the ranks");
            Assert.IsTrue(squad.SlotOf(thrower.Context, out Vector3 slot, out _, out _, out _));
            Assert.That(Flat(slot - thrower.Context.Position).magnitude, Is.LessThan(1f), "…on its slot");
        }

        [Test]
        public void FollowSquad_InNoSquad_OrWithNoFormationLed_DoesNotHold()
        {
            var rig = new GraphRig(trace: false);
            var follow = new FollowSquadReasoner();
            Assert.IsFalse(follow.Holds(new ReasonerQuery(rig.Context, TargetFacts.None, false), out string whyNot));
            StringAssert.Contains("no squad", whyNot);
        }

        [Test]
        public void SquadDecision_OnACharactersBrain_NeverHolds()
        {
            var rig = new GraphRig(trace: false);
            Assert.IsFalse(new AdvanceDecision().Holds(new ReasonerQuery(rig.Context, TargetFacts.None, false), out string whyNot));
            StringAssert.Contains("command graph", whyNot);
        }

        [Test]
        public void Kite_Within_TakenOnlyWhenTheTargetComesNear_LetGoFarther()
        {
            var rig = new GraphRig(trace: false);
            WeaponAsset bow = WeaponAsset.Create<WeaponAsset>(new WeaponMove { Name = "Shot", Reach = 14f });
            bow.Shoots = true;
            rig.GiveWeapon(bow);
            Transform foe = Foe(new Vector3(9f, 0f, 0f));
            var kite = new KiteReasoner { Within = 6f, LetGoAfter = 9f };

            Assert.IsFalse(kite.Holds(new ReasonerQuery(rig.Context, new TargetFacts(foe, 8f), false), out _),
                "8 m: not near enough to leave the ranks");
            Assert.IsTrue(kite.Holds(new ReasonerQuery(rig.Context, new TargetFacts(foe, 5f), false), out _),
                "5 m: kites");
            Assert.IsTrue(kite.Holds(new ReasonerQuery(rig.Context, new TargetFacts(foe, 8f), true), out _),
                "While kiting it lets go only beyond Let Go After");
            Assert.IsTrue(new KiteReasoner().Holds(new ReasonerQuery(rig.Context, new TargetFacts(foe, 20f), false), out _),
                "Within 0: at any distance, as before");
        }

        [Test]
        public void SquadGraphs_AnythingOnTheWrongBrain_IsAProblem()
        {
            // A character's decision on a Squad Brain
            var command = new BrainGraphBuilder(new SquadBrainTask { Target = TARGET });
            command.Variable(TARGET, VariableType.Transform);
            AgentState fight = command.Decision(new FightReasoner(), 1);
            command.Chain(fight, new WaitTask(1f));
            AgentState advance = command.Decision(new AdvanceDecision(), 2);
            command.Chain(advance, new MarchToTargetTask());
            List<GraphIssue> issues = Issues(command.Build());
            Assert.IsTrue(issues.Exists(issue => issue.Code == GraphIssueCode.WrongBrain && issue.RecordId == fight.Id),
                "Fight on a Squad Brain is shown as a problem");
            Assert.IsFalse(issues.Exists(issue => issue.Code == GraphIssueCode.WrongBrain && issue.RecordId == advance.Id),
                "Advance on a Squad Brain is right");

            // A squad's decision and a squad's block in a character's graph
            var character = new BrainGraphBuilder();
            character.BrainTask.Target = TARGET;
            character.Variable(TARGET, VariableType.Transform);
            advance = character.Decision(new AdvanceDecision(), 1);
            AgentState march = character.Chain(advance, new MarchToTargetTask())[0];
            issues = Issues(character.Build());
            Assert.IsTrue(issues.Exists(issue => issue.Code == GraphIssueCode.WrongBrain && issue.RecordId == advance.Id),
                "Advance on a character's Brain is shown as a problem");
            Assert.IsTrue(issues.Exists(issue => issue.Code == GraphIssueCode.WrongBrain && issue.RecordId == march.Id),
                "March To Target in a character's graph is shown as a problem");
        }

        [Test]
        public void SquadGraphs_OnTheCanvas_EveryNodeByItsType_NothingLostOnReading()
        {
            CanvasShows(CommandGraph(), "Squad Brain", "Surround", "Hold Line", "Advance", "Form Up", "Form Ring",
                "Hold Line", "March To Target", "Return To Banner");      // step 90: the decision is called Hold Line too
            CanvasShows(MemberGraph(), "Brain", "Follow Squad", "Keep Formation", "Attack");
        }

        [Test]
        public void Detection_AlsoTag_SeesBothKinds_TakesTheNearest()
        {
            var rig = new GraphRig(trace: false) { ThinkInterval = 0f };
            var finder = new ListFinder();
            rig.Context.Objects = finder;
            Transform player = Make("Player", new Vector3(9f, 0f, 0f)).transform;
            Transform soldier = Make("Soldier", new Vector3(5f, 0f, 0f)).transform;
            finder.Add("Player", player);
            finder.Add("Blue", soldier);
            var detection = new DetectionTask(new RadiusSensorTask { Radius = 12f })
                { Tag = "Player", AlsoTag = "Blue", StoreIn = TARGET, Interval = 0f };
            rig.SetGraph(GraphRig.OneState(new WaitTask(100f)));
            rig.Agent.Blackboard.Set(TARGET, (Transform)null);

            detection.Tick(rig.Context, DT);
            Assert.AreEqual(soldier, rig.Agent.Blackboard.Get<Transform>(TARGET), "The nearer one, of the second kind");

            detection.AlsoTag = "";
            rig.Agent.Blackboard.Set(TARGET, (Transform)null);
            detection.Tick(rig.Context, DT);
            detection.Tick(rig.Context, DT);
            Assert.AreEqual(player, rig.Agent.Blackboard.Get<Transform>(TARGET), "Without it: only the first kind");
        }

        // ---------- the graphs ----------

        // The squad's command graph: Squad Sight → Squad Brain → its four decisions, each with the block that leads the
        // formation
        private static BehaviorGraph CommandGraph()
        {
            var g = new BrainGraphBuilder(new SquadBrainTask
            {
                Target = TARGET, Files = FILES, Spacing = SPACING, RankGap = RANK_GAP, StrikersPerFoe = 3
            });
            g.Sensor(new SquadSightTask { StoreIn = TARGET, Interval = 0f });
            g.Variable(TARGET, VariableType.Transform);
            g.Chain(g.Decision(new SurroundDecision(), 1), new FormRingTask());
            g.Chain(g.Decision(new HoldLineDecision(), 2), new HoldLineTask());
            g.Chain(g.Decision(new AdvanceDecision(), 3), new MarchToTargetTask());
            g.Chain(g.Decision(new FormUpDecision(), 4), new ReturnToBannerTask());
            return g.Build();
        }

        // A member's own graph: its Brain, Follow Squad (5) → Keep Formation → (In Reach) Strike → back; and a decision
        // of its own, more important (1): it takes the member out of the ranks
        private static BehaviorGraph MemberGraph(ReasonerTask own = null, params AgentTask[] ownChain)
        {
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            if (own != null)
                g.Variable("Found", VariableType.Transform);
            if (own != null)
                g.Chain(g.Decision(own, 1), ownChain.Length > 0 ? ownChain : new AgentTask[] { new WaitTask(100f) });

            AgentState keep = g.Chain(g.Decision(new FollowSquadReasoner(), 5), new KeepFormationTask())[0];
            AgentState strike = g.Block(new AttackTask());
            g.Then(keep, strike);
            g.Then(strike, keep);
            g.OnFail(strike, keep);
            return g.Build();
        }

        private static List<GraphIssue> Issues(BehaviorGraph graph)
        {
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            try
            {
                GraphSerializer.Write(graph, asset);
                var issues = new List<GraphIssue>();
                GraphValidator.Check(asset, GraphSerializer.Read(asset, issues), issues);
                return issues;
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        private static void CanvasShows(BehaviorGraph graph, params string[] titles)
        {
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            try
            {
                GraphSerializer.Write(graph, asset);
                var document = new AIKit.Editor.GraphDocument(asset);
                Assert.IsFalse(document.IsReadOnly, "Every record is read (no unknown node or field)");

                var canvas = new AIKit.Editor.GraphViewCanvas();
                canvas.Show(document);
                List<string> shown = canvas.Query<UnityEditor.Experimental.GraphView.Node>().ToList()
                    .Select(node => node.title).ToList();
                CollectionAssert.IsSupersetOf(shown, titles);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        // ---------- the squad ----------

        private GraphRig Member(Vector3 at, BehaviorGraph graph = null)
        {
            var rig = new GraphRig(trace: false, world: _world);
            rig.Agent.gameObject.SetActive(true);
            rig.Actuator.Position = at;
            rig.Agent.transform.position = at;
            rig.GiveWeapon(REACH, 0.3f, 0.5f);
            rig.SetGraph(graph ?? _members);
            rig.Agent.Blackboard.Set(TARGET, (Transform)null);
            _rigs.Add(rig);
            _squad.Members.Add(rig.Agent);
            return rig;
        }

        // Twenty standing in their ranks at the banner already: two ranks of ten facing +z
        private void Formed(BehaviorGraph graph = null)
        {
            for (int rank = 0; rank < 2; rank++)
            {
                for (int file = 0; file < FILES; file++)
                    Member(new Vector3((file - (FILES - 1) * 0.5f) * SPACING, 0f, -rank * RANK_GAP), graph);
            }
        }

        private void SeeAll(Transform foe)
        {
            foreach (GraphRig rig in _rigs)
                rig.Agent.Blackboard.Set(TARGET, foe);
        }

        // The world's step, the squad's, then every member's that is awake; until the seconds are out or done says so
        private void Run(float seconds, System.Func<bool> done = null)
        {
            int steps = Mathf.CeilToInt(seconds / DT);
            for (int i = 0; i < steps; i++)
            {
                _world.BeginStep(DT);
                _squad.Step(DT);
                foreach (GraphRig rig in _rigs)
                {
                    if (!rig.Agent.enabled)
                        continue;

                    rig.Step(DT);
                    rig.Agent.transform.position = rig.Actuator.Position;
                }

                if (done != null && done())
                    return;
            }
        }

        private Transform Foe(Vector3 at) => Make("Foe", at).transform;

        private GameObject Make(string name, Vector3 at)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.position = at;
            _created.Add(go);
            return go;
        }

        private static Vector3 Flat(Vector3 vector) => new(vector.x, 0f, vector.z);
    }
}
