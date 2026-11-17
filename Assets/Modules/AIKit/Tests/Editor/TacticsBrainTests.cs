using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The fighters of the tactics island as whole graphs — a Brain, its decisions and their chains of ready blocks — on
    /// a real agent (step 90: the objects say what they offer and what happens; the graph only searches, walks and
    /// interacts): the gunner searches the turret, mans it and the Attack decision fires its gun empty; then it searches
    /// a turret to load, fetches a box from the depot (held in front of it), brings it and loads it — and meeting the
    /// target on the way puts the box down and fights hand to hand; the charger charges a target that ran far, stands
    /// after it, does not charge again for a while and fights close; the shooter searches a cover at shooting distance,
    /// takes the place behind it and shoots from there. (The rock thrower's whole loop: SearchForTests.)
    /// </summary>
    public sealed class TacticsBrainTests
    {
        private const string TARGET = "Target";
        private const string TURRET = "Turret";
        private const string DEPOT = "Depot";
        private const string COVER = "Cover";
        private const float STEP = 0.05f;

        private GraphRig _rig;
        private WorldObjects _registry;
        private WorldRack _rack;
        private BrainGraphBuilder _graph;
        private GameObject _victim;
        private HitProbe _hits;
        private int _spent;
        private System.Action _afterBuild;
        private readonly List<GameObject> _created = new();
        private readonly List<System.Type> _entered = new();

        [SetUp]
        public void SetUp()
        {
            _rig = new GraphRig(trace: false) { ThinkInterval = 0f };
            _rig.WakeUpAt(Vector3.zero);
            _registry = _rig.UseObjects();
            _rack = _rig.UseWorldRack();
            _victim = Make("Victim", new Vector3(15f, 0f, 0f));
            _hits = _victim.AddComponent<HitProbe>();
            _graph = new BrainGraphBuilder();
            _graph.BrainTask.Target = TARGET;
            _graph.Variable(TARGET, VariableType.Transform);
            _graph.Variable(TURRET, VariableType.Transform);
            _graph.Variable(DEPOT, VariableType.Transform);
            _graph.Variable(COVER, VariableType.Transform);
            _entered.Clear();
            _spent = 0;
            _afterBuild = null;
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
            GraphRig.DestroyAll();
        }

        [Test]
        public void TurretGunner_MansIt_FiresItEmpty_FetchesAmmoFromTheDepot_LoadsIt_FiresAgain()
        {
            Turret turret = Gunner(out GameObject depot);
            Run();

            bool carriedBox = false;
            bool boxInFront = false;
            Assert.IsTrue(_rig.StepUntil(() =>
            {
                Watch(turret);
                Transform box = _rig.Agent.Item;
                if (box != null)
                {
                    carriedBox = true;
                    boxInFront |= box.parent == _rig.Agent.transform && box.localPosition.y > 1f && box.localPosition.z > 0.2f;
                }

                return _hits.Hits >= turret.Capacity + 3;
            }, 60f, STEP), $"Rounds of the second load hit too (hits {_hits.Hits}, state {_rig.StateName}; {Why()})");

            Assert.IsTrue(carriedBox, "It went for ammo");
            Assert.IsTrue(boxInFront, "The box is carried at the chest, in front");
            Assert.That(Order(typeof(InteractTask), typeof(AttackTask), typeof(InteractTask), typeof(InteractTask),
                typeof(AttackTask)), Is.True, "Man — fire — fetch — load — fire again");
            Assert.IsTrue(_rack.TakenUp.All(taken => taken.Weapon == turret.Weapon && taken.Mounted),
                "It fired the turret's gun, on its mount");
            Assert.That(depot.transform.position, Is.EqualTo(new Vector3(-10f, 0f, 0f)), "The depot stays");
        }

        [Test]
        public void TurretGunner_MeetsTheTargetCarryingTheBox_PutsItDown_AndFightsHandToHand()
        {
            Turret turret = Gunner(out _);
            turret.Ammo = 0;                                            // empty from the start: off to the depot
            Run();

            Assert.IsTrue(_rig.StepUntil(() => _rig.Agent.Item != null, 20f, STEP), $"It fetched a box ({Why()})");
            Transform box = _rig.Agent.Item;
            _rig.Step(STEP, 4);
            int beforeFight = _hits.Hits;
            _victim.transform.position = _rig.Actuator.Position + new Vector3(1.2f, 0f, 0f);   // the target on its way back

            bool struckWithTheBox = false;
            Assert.IsTrue(_rig.StepUntil(() =>
            {
                struckWithTheBox |= _rig.Agent.Item != null && _hits.Hits > beforeFight;
                return _hits.Hits > beforeFight;
            }, 10f, STEP), "It fought the target it met");

            Assert.IsFalse(struckWithTheBox, "No blow while the box was in the hands");
            Assert.IsNull(box.parent, "The box was put down on the ground");
            Assert.AreEqual(0, turret.Ammo, "Not loaded: it fights first");
            CollectionAssert.Contains(_entered, typeof(DropTask));

            _victim.transform.position = new Vector3(40f, 0f, 0f);      // the target gone: back to the work
            Assert.IsTrue(_rig.StepUntil(() => turret.IsFull, 30f, STEP), "It fetches a box again and loads the turret");
        }

        [Test]
        public void Charger_ChargesATargetThatRanFar_StandsAfter_NoChargeAgainForAWhile_FightsClose()
        {
            _rig.GiveWeapon(1.6f, 0.2f, 0.3f);
            var charge = new ChargeTask { ChargeTime = 1f, AimSpeed = 180f, Distance = 9f, Speed = 12f, Recovery = 1f };
            var decision = new ChargeReasoner { Farther = 6f, Within = 25f, Every = 4f };
            Decide(new FightReasoner { Closer = 1.5f, LetGoAfter = 2f }, 1,
                Chain(new AttackTask()));
            AgentState charging = Decide(decision, 2, Chain(charge));
            Decide(new ChaseReasoner(), 3,
                Chain(new ChaseTask { StopDistance = 1.2f, FinishWhenCaught = true }));
            _victim.transform.position = new Vector3(12f, 0f, 0f);
            Run();

            Assert.IsTrue(_rig.StepUntil(() => _rig.StateName == "Charge", 1f, STEP), "Far: the Charge decision");
            Assert.IsTrue(_rig.StepUntil(() => charge.PhaseOf(_rig.Context) == ChargeTask.Phase.Recovering, 4f, STEP));
            Assert.That(_rig.Actuator.Position.x, Is.GreaterThan(8f), "The rush covered the way to the target");
            float recovered = _rig.Context.LocalTime;
            Assert.IsTrue(_rig.StepUntil(() => !charge.IsRunningFor(_rig.Context), 2f, STEP));
            Assert.That(_rig.Context.LocalTime - recovered, Is.EqualTo(1f).Within(2f * STEP), "It stood for Recovery");

            // The target keeps running 10 m ahead: no second charge before Every is up — it chases meanwhile
            Assert.IsTrue(_rig.StepUntil(() => _rig.Instance.Decision != charging, 1f, STEP),
                "The charge over, the decision lets go");
            float left = _rig.Context.LocalTime;
            Assert.IsTrue(_rig.StepUntil(() =>
            {
                _victim.transform.position = _rig.Actuator.Position + new Vector3(10f, 0f, 0f);
                return _rig.Instance.Decision == charging;
            }, 8f, STEP), "Charged again once rested");
            Assert.That(_rig.Context.LocalTime - left, Is.GreaterThanOrEqualTo(decision.Every - 2f * STEP),
                "Not twice in a row: the next charge waits Every seconds");

            _victim.transform.position = _rig.Actuator.Position + new Vector3(3f, 0f, 0f);
            Assert.IsTrue(_rig.StepUntil(() => _hits.Hits > 0, 10f, STEP), "Close: it strikes");
        }

        [Test]
        public void CoverShooter_SearchesACoverAtShootingDistance_TakesThePlaceBehindIt_AndShootsFromThere()
        {
            WeaponAsset bow = WeaponAsset.Create(new WeaponMove { Name = "Shot", Reach = 16f, WindUp = 0.3f, Recovery = 0.3f });
            bow.Shoots = true;
            bow.Shot.Pool = "";
            _rig.GiveWeapon(bow);
            var what = new WhatValue
            {
                Component = typeof(Cover).FullName, Offer = Offer.Occupy, Radius = 15f, FromTarget = true,
                MinDistance = 5f, MaxDistance = 15f
            };
            Decide(new SearchForReasoner { Rest = 1f, Condition = new IsInCover { Not = true } }, 1,
                Chain(new FindTask { What = what, StoreIn = COVER }, new MoveToTask { Target = COVER },
                    new InteractTask { Object = COVER, Interaction = Offer.Occupy }));
            Decide(new AttackInReach(), 2, Chain(new AttackTask()));
            _victim.transform.position = new Vector3(0f, 0f, 12f);
            var near = Make(COVER, new Vector3(2f, 0f, 3f)).AddComponent<Cover>();
            var tooNear = Make(COVER, new Vector3(0f, 0f, 9f)).AddComponent<Cover>();   // too near the target to shoot from
            _registry.Register(near);
            _registry.Register(tooNear);
            Run();

            Assert.IsTrue(_rig.StepUntil(() => _hits.Hits >= 3, 15f, STEP), "It shoots");

            Vector3 spot = near.PlaceFor(Offer.Occupy, _rig.Context, _victim.transform);
            Assert.That(Flat(_rig.Actuator.Position - spot).magnitude, Is.LessThan(0.8f), "…from behind the cover");
            Assert.IsTrue(_rig.Context.InCover, "In cover");
            CollectionAssert.Contains(_entered, typeof(InteractTask));
        }

        // ---------- the graphs ----------

        // The gunner: Attack (1: whatever is in the hand — the turret's gun, else its sword; a box goes down first), a
        // search for a turret to man (2: Find → Move To → Interact Use), a search for one to load (3: Find a turret that
        // takes a load, Find the depot, Move To, Interact Take, Move To the turret, Interact Load; Failed → Drop), a
        // patrol (4). A turret at 5 m looking at the target, the depot 10 m behind
        private Turret Gunner(out GameObject depot)
        {
            _rig.GiveWeapon(1.6f, 0.2f, 0.3f);
            GameObject holder = Make(TURRET, new Vector3(5f, 0f, 0f));
            holder.transform.rotation = Quaternion.LookRotation(Vector3.right);
            var turret = holder.AddComponent<Turret>();
            turret.Weapon = WeaponAsset.Create(new WeaponMove
            {
                Name = "Attack 1", Reach = 30f, WindUp = 0.05f, Recovery = 0.15f, Damage = 1f
            });
            turret.Capacity = 10;
            turret.Ammo = 10;
            turret.LoadTime = 0.5f;
            depot = Make(DEPOT, new Vector3(-10f, 0f, 0f));
            var store = depot.AddComponent<ItemSource>();
            store.TakeTime = 0.3f;
            _registry.Register(turret);
            _registry.Register(store);

            Decide(new AttackInReach(), 1, Chain(new DropTask(), new AttackTask()));
            AgentState man = Decide(new SearchForReasoner { Rest = 0.5f, Condition = new OccupiesSomething { Not = true } }, 2,
                Chain(new FindTask { What = Turret(Offer.Use), StoreIn = TURRET }, new MoveToTask { Target = TURRET },
                    new InteractTask { Object = TURRET, Interaction = Offer.Use }));
            AgentState[] reload = Blocks(
                new FindTask { What = Turret(Offer.Load), StoreIn = TURRET },
                new FindTask
                {
                    What = new WhatValue { Component = typeof(ItemSource).FullName, Offer = Offer.Take, Radius = 0f },
                    StoreIn = DEPOT
                },
                new MoveToTask { Target = DEPOT },
                new InteractTask { Object = DEPOT, Interaction = Offer.Take },
                new MoveToTask { Target = TURRET },
                new InteractTask { Object = TURRET, Interaction = Offer.Load });
            AgentState load = Decide(new SearchForReasoner { Rest = 0.5f, Condition = new OccupiesSomething { Not = true } }, 3,
                reload[0]);
            _afterBuild = () => man.Reasoner.NotFrom.Add(load.Id);     // a load on its way is not dropped to man the empty gun
            _graph.OnFail(reload[5], _graph.Block(new DropTask()));
            Decide(new PatrolReasoner(), 4, Chain(new WaitTask(100f)));
            return turret;
        }

        private static WhatValue Turret(Offer offer) =>
            new() { Component = typeof(Turret).FullName, Offer = offer, Radius = 0f };

        // A decision wired to the Brain with its priority, its Do pin wired to the first block of its chain
        private AgentState Decide(ReasonerTask decision, int priority, AgentState chain)
        {
            AgentState state = _graph.Decision(decision, priority);
            _graph.Do(state, chain);
            return state;
        }

        // Blocks one after another, each going on when it is done (its Done pin); the first is returned
        private AgentState Chain(params AgentTask[] tasks) => Blocks(tasks)[0];

        private AgentState[] Blocks(params AgentTask[] tasks)
        {
            var blocks = new AgentState[tasks.Length];
            for (int i = 0; i < tasks.Length; i++)
            {
                blocks[i] = _graph.Block(tasks[i]);
                if (i > 0)
                    _graph.Then(blocks[i - 1], blocks[i]);
            }

            return blocks;
        }

        private void Run()
        {
            BehaviorGraph graph = _graph.Build();
            _afterBuild?.Invoke();
            _rig.SetGraph(graph);
            _rig.Agent.Blackboard.Set(TARGET, _victim.transform);
            _rig.Context.ExplainsDecisions = true;     // a failure says why each decision did not hold
            _rig.Instance.StateChanged += (_, to) =>
            {
                if (to != null && to.Tasks.Count > 0)
                    _entered.Add(to.Tasks[0].GetType());
            };
        }

        // A round that hit is a use of the gun spent (the moves lane's attack runner spends it), and the turret looks at
        // its gunner as it does every frame
        private void Watch(Turret turret)
        {
            if (_hits.Hits > _spent)
            {
                _spent = _hits.Hits;
                if (_rack.FromWorld != null)
                    _rack.SpendUse();
            }

            _ = turret.Ammo;
        }

        // The Brain's verdicts at its last choice, and the blocks entered: what a failure says
        private string Why()
        {
            var said = new System.Text.StringBuilder();
            foreach (ReasonerVerdict verdict in CombatBrainTask.VerdictsOf(_graph.Brain, _rig.Context))
                said.Append(verdict.State.DisplayName).Append(verdict.Holds ? " holds" : $" — {verdict.WhyNot}").Append("; ");
            said.Append("entered: ").Append(string.Join(", ", _entered.ConvertAll(type => type.Name)));
            return said.ToString();
        }

        // The blocks were entered in this order (others in between)
        private bool Order(params System.Type[] types)
        {
            int at = 0;
            foreach (System.Type entered in _entered)
            {
                if (at < types.Length && entered == types[at])
                    at++;
            }

            return at == types.Length;
        }

        /// <summary>
        /// The Attack decision as the conditions lane makes it ("Closer 0 — the reach of the weapon in the hand"): it holds
        /// while the weapon in the hand reaches the Brain's target.
        /// </summary>
        private sealed class AttackInReach : ReasonerTask
        {
            public override bool Holds(in ReasonerQuery query, out string whyNot)
            {
                WeaponAsset weapon = query.Context?.Weapon;
                bool reaches = query.Facts.Sees && weapon != null && query.Facts.Distance <= weapon.ShortestReach;
                whyNot = reaches ? null : "out of the weapon's reach";
                return reaches;
            }
        }

        private GameObject Make(string name, Vector3 at)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.position = at;
            _created.Add(go);
            return go;
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
