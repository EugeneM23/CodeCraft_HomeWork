using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The blows that land on an AI character (step 90, reactions): one hit path — DirectStriker tells every receiver of
    /// the victim, the character among them, which remembers the hit (who, when, from where, the pressure of recent
    /// hits halving every 2 s), marks itself hurt for its presentation and thinks on its next step; the Hit By shape of
    /// Detection sees whoever struck for Remember seconds (handbook §9.3: damage always wakes).
    /// </summary>
    public sealed class HitTests
    {
        private const string SEEN = "Seen";
        private const float DT = 0.05f;

        private GraphRig _rig;
        private GameObject _attacker;
        private readonly List<GameObject> _created = new();

        [SetUp]
        public void SetUp()
        {
            _rig = new GraphRig(trace: false);
            _rig.Context.Self.rotation = Quaternion.LookRotation(_rig.Actuator.Facing);     // the body faces +X
            _attacker = New("Player", new Vector3(2f, 0f, 0f));
            _attacker.tag = "Player";
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject created in _created)
                Object.DestroyImmediate(created);
            _created.Clear();
            GraphRig.DestroyAll();
        }

        [Test]
        public void TakeHit_RemembersWhoWhenFromWhereHowHard_AndMarksTheCharacterHurt()
        {
            _rig.Tick(1.5f);
            float now = _rig.Context.LocalTime;

            _rig.Agent.TakeHit(new HitInfo(_attacker.transform, 3f));

            HitMemory hits = _rig.Context.Hits;
            Assert.AreEqual(1, hits.Count);
            Assert.AreSame(_attacker.transform, hits.LastAttacker);
            Assert.AreEqual(now, hits.LastAt);
            Assert.AreEqual(_attacker.transform.position, hits.LastFrom);
            Assert.AreEqual(3f, hits.LastDamage);
            Assert.IsFalse(hits.LastFromBehind, "The attacker stands in front");
            Assert.AreEqual(now, _rig.Context.HurtAt, "Hurt now: the presentation plays the flinch (the body's, not a decision)");
            Assert.AreEqual(_attacker.transform.position, _rig.Context.HurtFrom);
        }

        [Test]
        public void TakeHit_FromBehind_IsMarkedSo()
        {
            _attacker.transform.position = new Vector3(-2f, 0f, 0.5f);

            _rig.Agent.TakeHit(new HitInfo(_attacker.transform, 1f));

            Assert.IsTrue(_rig.Context.Hits.LastFromBehind, "From the back half of where the character faces");
            Assert.IsTrue(AICharacter.IsBehind(Vector3.right, Vector3.left));
            Assert.IsFalse(AICharacter.IsBehind(Vector3.right, new Vector3(0.1f, 0f, 1f)), "From the side, a little ahead: not behind");
        }

        [Test]
        public void TakeHit_ThePressureOfRecentHits_AddsUp_AndHalvesEveryTwoSeconds()
        {
            _rig.Agent.TakeHit(new HitInfo(_attacker.transform, 1f));
            _rig.Agent.TakeHit(new HitInfo(_attacker.transform, 1f));
            float now = _rig.Context.LocalTime;
            HitMemory hits = _rig.Context.Hits;

            Assert.AreEqual(2f, hits.PressureAt(now), 1e-4f, "Each blow adds one");
            Assert.AreEqual(1f, hits.PressureAt(now + HitMemory.HALF_LIFE), 1e-4f, "Half after the half-life (handbook §19.2: ~2 s)");
            Assert.AreEqual(0.5f, hits.PressureAt(now + 2f * HitMemory.HALF_LIFE), 1e-4f);
            Assert.AreEqual(0f, new HitMemory().PressureAt(now), "Never hit: no pressure");
        }

        [Test]
        public void TakeHit_TheCharacterThinksOnItsNextStep_NotAfterItsThinkInterval()
        {
            const float interval = 1f;
            _rig.ThinkInterval = interval;
            var g = new BrainGraphBuilder();
            g.Chain(g.Always(), new WaitTask(100f));
            _rig.SetGraph(g.Build());
            _rig.Step(DT, 3);
            int decisions = _rig.Agent.DecisionCount;

            _rig.Step(DT);
            Assert.AreEqual(decisions, _rig.Agent.DecisionCount, "Mid think interval: no think");

            _rig.Agent.TakeHit(new HitInfo(_attacker.transform, 1f));
            _rig.Step(DT);

            Assert.AreEqual(decisions + 1, _rig.Agent.DecisionCount, "A hit is worth a decision at once");
        }

        [Test]
        public void TakeHit_ItsOwnBlow_IsNoHitOnIt()
        {
            _rig.Agent.TakeHit(new HitInfo(_rig.Agent.transform, 1f));

            Assert.AreEqual(0, _rig.Context.Hits.Count);
        }

        [Test]
        public void DirectStriker_TellsEveryReceiverOfTheVictim_TheCharacterAndTheGamesHealth_OnceEach()
        {
            GameObject victim = _rig.Agent.gameObject;
            victim.SetActive(true);                     // in play (Edit Mode sends the character no messages)
            HitProbe health = victim.AddComponent<HitProbe>();

            DirectStriker.Deliver(_attacker.transform, victim.transform, 2f);

            Assert.AreEqual(1, health.Hits, "The game's receiver hears it once");
            Assert.AreEqual(1, _rig.Context.Hits.Count, "…and the character remembers it once: nobody reports it twice");
            Assert.AreSame(_attacker.transform, _rig.Context.Hits.LastAttacker);
        }

        [Test]
        public void DirectStriker_AReceiverAboveTheStruckObject_HearsItToo()
        {
            GameObject owner = New("Owner", Vector3.zero);
            GameObject arm = New("Arm", Vector3.zero);
            arm.transform.SetParent(owner.transform);
            HitProbe below = arm.AddComponent<HitProbe>();
            HitProbe above = owner.AddComponent<HitProbe>();

            DirectStriker.Deliver(_attacker.transform, arm.transform, 1f);

            Assert.AreEqual(1, below.Hits);
            Assert.AreEqual(1, above.Hits);
        }

        [Test]
        public void DirectStriker_AReceiverThatStrikesBack_EveryBlowArrives()
        {
            GameObject first = New("First", Vector3.zero);
            GameObject second = New("Second", Vector3.zero);
            HitProbe answered = second.AddComponent<HitProbe>();
            first.AddComponent<StrikesBack>().Back = second.transform;
            HitProbe firstProbe = first.AddComponent<HitProbe>();

            DirectStriker.Deliver(_attacker.transform, first.transform, 1f);

            Assert.AreEqual(1, firstProbe.Hits, "The first victim's every receiver heard the blow");
            Assert.AreEqual(1, answered.Hits, "…and the blow struck from inside the delivery arrived too");
        }

        [Test]
        public void HitBy_SeesWhoeverStruck_ForRememberSeconds_ThenNot_AndNobodyElse()
        {
            GameObject bystander = New("Bystander", new Vector3(0f, 0f, 2f));
            bystander.tag = "Player";
            _rig.Context.Objects = new TaggedFinder(_attacker.transform, bystander.transform);
            var shape = new HitBySensorTask { Remember = 1f };
            float remember = shape.Remember.Get(null);
            var detection = new DetectionTask(shape) { StoreIn = SEEN, Interval = 0f };

            Look(detection);
            Assert.IsNull(detection.SeenBy(_rig.Context), "Nobody struck: nothing seen");

            _rig.Agent.TakeHit(new HitInfo(_attacker.transform, 1f));
            float hitAt = _rig.Context.LocalTime;
            Look(detection);
            Assert.AreSame(_attacker.transform, detection.SeenBy(_rig.Context), "Whoever struck is seen (handbook §9.3)");

            for (int i = 0; i < 100 && detection.SeenBy(_rig.Context) != null; i++)
                Look(detection);

            Assert.IsNull(detection.SeenBy(_rig.Context), "Not for ever");
            Assert.That(_rig.Context.LocalTime - hitAt, Is.InRange(remember, remember + 2 * DT), "For Remember seconds");
        }

        [Test]
        public void HitBy_AnAttackerNotOfDetectionsKind_OrFartherThanWithin_IsNotSeen()
        {
            const float within = 10f;
            _rig.Agent.SetSignals(new SignalBus());     // the second shape listens: the agent has its own bus
            _rig.Context.Objects = new TaggedFinder(_attacker.transform);
            var stranger = new DetectionTask(new HitBySensorTask()) { StoreIn = "Stranger", Interval = 0f, Tag = "MainCamera" };
            _rig.Agent.TakeHit(new HitInfo(_attacker.transform, 1f));

            Look(stranger);
            Assert.IsNull(stranger.SeenBy(_rig.Context), "Not of Detection's kind (its tag): not its business");

            // Another shape gathers from much farther: Hit By still keeps to its own Within
            var far = new DetectionTask(new HitBySensorTask { Within = within }, new CallsForHelpSensorTask { Within = 100f })
            {
                StoreIn = SEEN, Interval = 0f
            };
            _attacker.transform.position = new Vector3(within + 2f, 0f, 0f);
            _rig.Agent.TakeHit(new HitInfo(_attacker.transform, 1f));
            Look(far);
            Assert.IsNull(far.SeenBy(_rig.Context), "Farther than Within");

            _attacker.transform.position = new Vector3(within - 2f, 0f, 0f);
            Look(far);
            Assert.AreSame(_attacker.transform, far.SeenBy(_rig.Context), "Within: seen");
        }

        // ---------- helpers ----------

        private void Look(SensorTask detection)
        {
            _rig.Context.Advance(DT);
            detection.Tick(_rig.Context, DT);
        }

        private GameObject New(string name, Vector3 at)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.position = at;
            _created.Add(go);
            return go;
        }
    }

    /// <summary>A receiver that strikes another object back from inside its ReceiveHit (a thorn, a riposte).</summary>
    internal sealed class StrikesBack : MonoBehaviour, IHitReceiver
    {
        public Transform Back;

        public void ReceiveHit(in HitInfo hit)
        {
            if (Back != null)
                DirectStriker.Deliver(transform, Back, hit.Damage);
        }
    }
}
