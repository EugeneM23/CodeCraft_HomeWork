using System.Collections.Generic;
using AIKit.Editor;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Step 91, Try Get Component: what a graph may read of a component (AIReadables: public numbers, flags, vectors,
    /// [AIReadable]), the block (flow in, Then out; Component and Found out), a Get, an Is and a Compare reading what it
    /// gives, the graph's problems, the asset keeping the outputs, sensors looking by type, the editor's lists.
    /// </summary>
    public sealed class TryGetComponentTests
    {
        private const string TARGET = "Target";
        private const float DT = 0.05f;
        private readonly List<Object> _created = new();
        private GraphRig _rig;

        [SetUp]
        public void SetUp()
        {
            _rig = new GraphRig(trace: false);
            TagObjectFinder.Shared.Clear();
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
            TagObjectFinder.Shared.Clear();
            GraphRig.DestroyAll();
        }

        // ---------- AIReadables ----------

        [Test]
        public void Readables_PublicNumbersFlagsVectors_AndWhatIsMarked_TheTypesOwnFirst()
        {
            IReadOnlyList<ReadableMember> members = AIReadables.MembersOf(typeof(Player));
            var names = new List<string>();
            foreach (ReadableMember member in members)
                names.Add(member.Name);

            CollectionAssert.AreEqual(new[]
            {
                "_stamina", "_wide", "Aim.x", "Aim.y", "Aim.magnitude", "Fury", "IsBlocking", "Hidden", "_level", "enabled",
                "isActiveAndEnabled"
            }, names, "Its own first (marked private ones, public ones — a vector by axis and length), then its bases' — " +
                      "not a string, not an unmarked private field, nothing of MonoBehaviour, Component or Object");
            Assert.AreEqual("Stamina", AIReadables.Find(typeof(Player), "_stamina").Label);
            Assert.AreEqual("Rage", AIReadables.Find(typeof(Player), "Fury").Label, "Its own name in the lists");
            Assert.AreEqual("Aim Length", AIReadables.Find(typeof(Player), "Aim.magnitude").Label);
            Assert.IsTrue(AIReadables.Find(typeof(Player), "IsBlocking").IsFlag);
            Assert.IsFalse(AIReadables.Find(typeof(Player), "Fury").IsFlag);
            Assert.IsNull(AIReadables.Find(typeof(Player), "_hidden"), "Private and not marked: not read");
        }

        [Test]
        public void Readables_AnEngineComponent_Rigidbody_Transform_ItsPublicValues()
        {
            Assert.IsNotNull(AIReadables.Find(typeof(Rigidbody), "mass"));
            Assert.IsTrue(AIReadables.Find(typeof(Rigidbody), "isKinematic").IsFlag);
            Assert.IsNotNull(AIReadables.Find(typeof(Rigidbody), "linearVelocity.magnitude"));
            Assert.IsNull(AIReadables.Find(typeof(Rigidbody), "velocity.x"), "Obsolete: not offered");
            Assert.IsNotNull(AIReadables.Find(typeof(Transform), "position.y"));
            Assert.IsNull(AIReadables.Find(typeof(Transform), "name"), "A string is not read");

            var body = Make<Rigidbody>("Body");
            body.mass = 7f;
            body.isKinematic = true;
            body.transform.position = new Vector3(1f, 2f, 3f);
            body.transform.localScale = new Vector3(0f, 3f, 4f);

            Assert.IsTrue(AIReadables.Find(typeof(Rigidbody), "mass").TryNumber(body, out float mass));
            Assert.AreEqual(7f, mass);
            Assert.IsTrue(AIReadables.Find(typeof(Rigidbody), "isKinematic").TryFlag(body, out bool kinematic));
            Assert.IsTrue(kinematic);
            Assert.IsTrue(AIReadables.Find(typeof(Transform), "position.y").TryNumber(body.transform, out float y));
            Assert.AreEqual(2f, y);
            Assert.IsTrue(AIReadables.Find(typeof(Transform), "localScale.magnitude").TryNumber(body.transform, out float scale));
            Assert.AreEqual(5f, scale, 1e-5f);
        }

        [Test]
        public void Readables_ReadTheComponentLive_ANumberIsNoFlag_AndAFlagNoNumber()
        {
            Player player = Make<Player>("Player");
            player.Set(stamina: 40f, fury: 7, blocking: true, wide: 2.5, level: 3f);

            Assert.IsTrue(AIReadables.Find(typeof(Player), "_stamina").TryNumber(player, out float stamina));
            Assert.AreEqual(40f, stamina);
            Assert.IsTrue(AIReadables.Find(typeof(Player), "Fury").TryNumber(player, out float fury));
            Assert.AreEqual(7f, fury);
            Assert.IsTrue(AIReadables.Find(typeof(Player), "_wide").TryNumber(player, out float wide));
            Assert.AreEqual(2.5f, wide);
            Assert.IsTrue(AIReadables.Find(typeof(Player), "_level").TryNumber(player, out float level));
            Assert.AreEqual(3f, level, "A member of the base class");
            Assert.IsTrue(AIReadables.Find(typeof(Player), "IsBlocking").TryFlag(player, out bool blocking));
            Assert.IsTrue(blocking);

            Assert.IsFalse(AIReadables.Find(typeof(Player), "IsBlocking").TryNumber(player, out _));
            Assert.IsFalse(AIReadables.Find(typeof(Player), "Fury").TryFlag(player, out _));

            player.Set(stamina: 5f, fury: 1, blocking: false, wide: 0, level: 3f);
            AIReadables.Find(typeof(Player), "_stamina").TryNumber(player, out stamina);
            Assert.AreEqual(5f, stamina, "Read live, not copied");
        }

        [Test]
        public void Readables_AProperty_IsReadWithoutAllocating()
        {
            Player player = Make<Player>("Player");
            ReadableMember fury = AIReadables.Find(typeof(Player), "Fury");
            ReadableMember blocking = AIReadables.Find(typeof(Player), "IsBlocking");
            ReadableMember height = AIReadables.Find(typeof(Transform), "position.y");
            Transform at = player.transform;
            fury.TryNumber(player, out _);
            blocking.TryFlag(player, out _);
            height.TryNumber(at, out _);

            Assert.That(() =>
            {
                fury.TryNumber(player, out _);
                blocking.TryFlag(player, out _);
                height.TryNumber(at, out _);
            }, NoAllocation.Constraint);
        }

        [Test]
        public void Readables_AnyComponentCanBeCastTo_NotAnythingElse_MarkedOnesAreKnown()
        {
            Assert.IsTrue(AIReadables.IsComponentType(typeof(Rigidbody)));
            Assert.IsTrue(AIReadables.IsComponentType(typeof(Transform)));
            Assert.IsTrue(AIReadables.IsComponentType(typeof(Unmarked)));
            Assert.IsFalse(AIReadables.IsComponentType(typeof(NotAComponent)));
            Assert.IsFalse(AIReadables.IsComponentType(typeof(GameObject)), "A GameObject is no component");

            Assert.IsTrue(AIReadables.IsMarked(typeof(Marker)));
            Assert.IsTrue(AIReadables.IsMarked(typeof(AICharacter)), "The module's own character is marked");
            Assert.IsFalse(AIReadables.IsMarked(typeof(Player)), "Marked members do not mark the class");
        }

        [Test]
        public void Readables_Words_MembersAndTypesInPlainWords()
        {
            Assert.AreEqual("Is Blocking", AIReadables.Spaced("_isBlocking"));
            Assert.AreEqual("Max HP", AIReadables.Spaced("maxHP"));
            Assert.AreEqual("Speed", AIReadables.Spaced("m_Speed"));
            Assert.AreEqual("Player Controller", AIReadables.Words("Game.PlayerController"));
            Assert.AreEqual("Player", AIReadables.Words(typeof(Player).FullName), "A nested type by its own name");
            Assert.AreSame(typeof(Player), AIReadables.TypeNamed(typeof(Player).FullName));
            Assert.IsNull(AIReadables.TypeNamed("No.Such.Type"));
        }

        // ---------- Try Get Component ----------

        [Test]
        public void TryGetComponent_TheObjectHasIt_GoesOn_GivesTheComponentAndFound_WithoutIt_FoundIsFalse()
        {
            Player player = Make<Player>("Player");
            Target(player.transform);
            TryGetComponentTask look = Look(typeof(Player));

            Assert.AreEqual(TaskStatus.Success, Run(look), "The flow goes on at once");
            Assert.IsTrue(look.Component.TryGetComponent(_rig.Context, out Component found));
            Assert.AreSame(player, found);
            Assert.IsTrue(look.Found.TryGetFlag(_rig.Context, out bool has));
            Assert.IsTrue(has);

            Target(Make<Unmarked>("Other").transform);
            Assert.AreEqual(TaskStatus.Success, Run(look), "Not found: the flow goes on as well (Found says it)");
            Assert.IsFalse(look.Component.TryGetComponent(_rig.Context, out _), "No component: no data");
            Assert.AreEqual(false, look.Component.IsOne(_rig.Context));
            Assert.IsTrue(look.Found.TryGetFlag(_rig.Context, out has));
            Assert.IsFalse(has, "Found: false");
        }

        [Test]
        public void TryGetComponent_BeforeItsFirstRun_NoData_ItGivesWhatItFoundLastTime()
        {
            Player player = Make<Player>("Player");
            Target(player.transform);
            TryGetComponentTask look = Look(typeof(Player));

            Assert.IsFalse(look.Found.TryGetFlag(_rig.Context, out _), "Not run yet: no data");
            Assert.IsNull(look.Component.IsOne(_rig.Context));

            Run(look);
            Target(Make<Unmarked>("Other").transform);
            Assert.AreEqual(true, look.Component.IsOne(_rig.Context), "What it found when it ran, until it runs again");
            Run(look);
            Assert.AreEqual(false, look.Component.IsOne(_rig.Context));
        }

        [Test]
        public void TryGetComponent_TheComponentOnAParent_IsFound_TheObjectGivenIsTheComponents()
        {
            Player player = Make<Player>("Player");
            var collider = new GameObject("Collider");
            _created.Add(collider);
            collider.transform.SetParent(player.transform);
            Target(collider.transform);
            TryGetComponentTask look = Look(typeof(Player));

            Run(look);

            Assert.IsTrue(look.Component.TryGetObject(_rig.Context, out Transform given));
            Assert.AreSame(player.transform, given, "The object the component is on");
        }

        [Test]
        public void TryGetComponent_NoObject_NoType_AnUnknownType_FoundIsFalse_TheComponentHasNoData()
        {
            Target(Make<Player>("Player").transform);

            foreach (TryGetComponentTask look in new[]
                     {
                         Look(null), new TryGetComponentTask { Object = TARGET, Type = "No.Such.Type" },
                         new TryGetComponentTask { Type = typeof(Player).FullName }
                     })
            {
                Run(look);
                Assert.IsTrue(look.Found.TryGetFlag(_rig.Context, out bool has));
                Assert.IsFalse(has);
                Assert.IsNull(look.Component.IsOne(_rig.Context));
                Assert.IsNotNull(look.Component.WhyNone(_rig.Context));
            }

            Target(null);
            TryGetComponentTask empty = Look(typeof(Player));
            Run(empty);
            Assert.IsNull(empty.Component.IsOne(_rig.Context), "The variable holds nothing: no data");
            StringAssert.Contains("held nothing", empty.Component.WhyNone(_rig.Context));
        }

        [Test]
        public void TryGetComponent_Rigidbody_GetReadsItsMass_IsReadsWhetherItIsKinematic()
        {
            var body = Make<Rigidbody>("Body");
            body.mass = 12f;
            body.isKinematic = false;
            Target(body.transform);
            TryGetComponentTask look = Look(typeof(Rigidbody));
            Run(look);

            Assert.IsTrue(new GetValue { As = look.Component, Member = "mass" }.TryGetNumber(_rig.Context, out float mass));
            Assert.AreEqual(12f, mass);
            Assert.AreEqual(Truth.No, new IsCondition { As = look.Component, Member = "isKinematic" }.Ask(_rig.Context));
            Assert.AreEqual(Truth.Yes, new IsCondition { As = look.Component }.Ask(_rig.Context), "It has a Rigidbody");

            Target(Make<Unmarked>("Plain").transform);
            Run(look);
            Assert.AreEqual(Truth.No, new IsCondition { As = look.Component }.Ask(_rig.Context), "No Rigidbody on it");
        }

        [Test]
        public void TryGetComponent_TransformPosition_OneAxisOfAVector()
        {
            Player player = Make<Player>("Player");
            player.transform.position = new Vector3(1f, 2.5f, 3f);
            Target(player.transform);
            TryGetComponentTask look = Look(typeof(Transform));
            Run(look);

            Assert.IsTrue(new GetValue { As = look.Component, Member = "position.y" }.TryGetNumber(_rig.Context, out float y));
            Assert.AreEqual(2.5f, y);
        }

        // ---------- Get, Is and Compare reading what it gives ----------

        [Test]
        public void Get_ReadsTheMember_NotFound_NoMember_AFlag_NoData()
        {
            Player player = Make<Player>("Player");
            player.Set(stamina: 40f, fury: 7, blocking: true, wide: 0, level: 0);
            Target(player.transform);
            TryGetComponentTask look = Look(typeof(Player));
            Run(look);
            ComponentValue component = look.Component;

            Assert.IsTrue(new GetValue { As = component, Member = "_stamina" }.TryGetNumber(_rig.Context, out float stamina));
            Assert.AreEqual(40f, stamina);

            Assert.IsFalse(new GetValue { As = component }.TryGetNumber(_rig.Context, out _), "No member picked");
            Assert.IsFalse(new GetValue { As = component, Member = "IsBlocking" }.TryGetNumber(_rig.Context, out _), "A flag");
            Assert.IsFalse(new GetValue { As = component, Member = "Gone" }.TryGetNumber(_rig.Context, out _), "No such member");

            Target(Make<Unmarked>("Other").transform);
            Run(look);
            Assert.IsFalse(new GetValue { As = component, Member = "_stamina" }.TryGetNumber(_rig.Context, out _),
                "Not found: no data, never a zero");
        }

        [Test]
        public void Compare_OfFound_IsOn_AndOfAGetOfTheComponent()
        {
            Player player = Make<Player>("Player");
            player.Set(stamina: 10f, fury: 0, blocking: false, wide: 0, level: 0);
            Target(player.transform);
            TryGetComponentTask look = Look(typeof(Player));
            var found = new CompareCondition { A = look.Found, Kind = VariableType.Bool, Expected = true };
            var tired = new CompareCondition(new GetValue { As = look.Component, Member = "_stamina" }, NumberComparison.Less, 20f);

            Assert.AreEqual(Truth.NoData, found.Ask(_rig.Context), "Not run yet");
            Run(look);
            Assert.AreEqual(Truth.Yes, found.Ask(_rig.Context));
            Assert.AreEqual(Truth.Yes, tired.Ask(_rig.Context));
            Assert.AreEqual("Player found is on", found.Phrase(_rig.Context));

            Target(Make<Unmarked>("Other").transform);
            Run(look);
            Assert.AreEqual(Truth.No, found.Ask(_rig.Context));
            Assert.AreEqual(Truth.NoData, tired.Ask(_rig.Context));
        }

        [Test]
        public void Is_WithAFlag_ReadsIt_NotFound_NoData()
        {
            Player player = Make<Player>("Player");
            Target(player.transform);
            TryGetComponentTask look = Look(typeof(Player));
            Run(look);
            var blocks = new IsCondition { As = look.Component, Member = "IsBlocking" };

            player.Set(stamina: 0, fury: 0, blocking: true, wide: 0, level: 0);
            Assert.AreEqual(Truth.Yes, blocks.Ask(_rig.Context));
            player.Set(stamina: 0, fury: 0, blocking: false, wide: 0, level: 0);
            Assert.AreEqual(Truth.No, blocks.Ask(_rig.Context));

            Target(Make<Unmarked>("Other").transform);
            Run(look);
            Assert.AreEqual(Truth.NoData, blocks.Ask(_rig.Context), "Not found: no data, not \"no\"");
            StringAssert.Contains("has no Player", blocks.WhyNoData(_rig.Context));
        }

        [Test]
        public void Phrases_SayTheComponentAndTheMember()
        {
            TryGetComponentTask look = Look(typeof(Player));

            Assert.AreEqual("Target's Player", look.Component.Phrase);
            Assert.AreEqual("Player found", look.Found.Phrase);
            Assert.AreEqual("Stamina of Target's Player", new GetValue { As = look.Component, Member = "_stamina" }.Phrase);
            Assert.AreEqual("Target has a Player", new IsCondition { As = look.Component }.Phrase(_rig.Context));
            Assert.AreEqual("Target's Player: Is Blocking",
                new IsCondition { As = look.Component, Member = "IsBlocking" }.Phrase(_rig.Context));
        }

        [Test]
        public void Shows_WithAComponentWiredIn_OnlyTheMember_WithoutIt_TheNodesOwnSettings()
        {
            var get = new GetValue();
            Assert.IsTrue(get.Shows(nameof(GetValue.Parameter)));
            Assert.IsFalse(get.Shows(nameof(GetValue.Member)));

            get.As = Look(typeof(Player)).Component;
            Assert.IsTrue(get.Shows(nameof(GetValue.Member)));
            Assert.IsFalse(get.Shows(nameof(GetValue.Parameter)));
            Assert.IsFalse(get.Shows(nameof(GetValue.Object)), "Its own object pin is not used: no hidden variable");

            var condition = new IsCondition { As = get.As };
            Assert.IsFalse(condition.Shows(nameof(IsCondition.State)));
            Assert.IsFalse(condition.Shows(nameof(IsCondition.Object)));
            Assert.IsTrue(condition.Shows(nameof(IsCondition.Member)));
        }

        // ---------- the graph ----------

        [Test]
        public void Graph_TheBlockAndWhatReadsItsOutputs_AreWrittenAndReadBack_TheOutputsKnowTheirBlock()
        {
            var g = new BrainGraphBuilder();
            g.Variable(TARGET, VariableType.Transform);
            AgentState always = g.Always();
            var look = new TryGetComponentTask(TARGET, typeof(Player));
            AgentState lookState = g.Block(look);
            AgentState branch = g.Block(new BranchTask(new IsCondition { As = look.Component, Member = "IsBlocking" }));
            AgentState low = g.Block(new BranchTask(new CompareCondition(new GetValue { As = look.Component, Member = "_stamina" },
                NumberComparison.Less, 20f)));
            AgentState found = g.Block(new BranchTask(new CompareCondition { A = look.Found, Kind = VariableType.Bool }));
            g.Do(always, lookState);
            g.Then(lookState, branch);
            g.Then(branch, low);
            g.Then(low, found);
            BehaviorGraph graph = g.Build();
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _created.Add(asset);

            GraphSerializer.Write(graph, asset);
            var issues = new List<GraphIssue>();
            BehaviorGraph read = GraphSerializer.Read(asset, issues);

            CollectionAssert.IsEmpty(issues.FindAll(issue => issue.IsError), string.Join("\n", issues));
            var readLook = (TryGetComponentTask)read.Root.Children
                .Find(node => node.Tasks.Count > 0 && node.Tasks[0] is TryGetComponentTask).Tasks[0];
            Assert.AreEqual(typeof(Player).FullName, readLook.Type);
            Assert.AreEqual(TARGET, readLook.Object);
            Assert.AreSame(readLook, readLook.Component.Block, "The outputs know their block once read");
            Assert.AreSame(readLook, readLook.Found.Block);

            IsCondition readIs = null;
            CompareCondition readCompare = null;
            CompareCondition readFound = null;
            foreach (AgentState state in read.Root.Children)
            {
                if (state.Tasks.Count == 0 || state.Tasks[0] is not BranchTask task)
                    continue;

                readIs ??= task.Condition as IsCondition;
                if (task.Condition is CompareCondition { A: GetValue } compare)
                    readCompare = compare;
                if (task.Condition is CompareCondition { A: FoundOutput } flag)
                    readFound = flag;
            }

            Assert.AreSame(readLook.Component, readIs?.As, "The Is reads the block's own Component");
            Assert.AreEqual("IsBlocking", readIs.Member);
            Assert.AreSame(readLook.Component, ((GetValue)readCompare.A).As, "One output read by both");
            Assert.AreSame(readLook.Found, readFound?.A);
        }

        [Test]
        public void Validator_NoType_AnUnknownType_NothingWired_AGetReadingNothing_AnIsReadingANumber()
        {
            var g = new BrainGraphBuilder();
            g.Variable(TARGET, VariableType.Transform);
            g.Variable("Seen", VariableType.Float);
            AgentState always = g.Always();
            var good = new TryGetComponentTask(TARGET, typeof(Player));
            AgentState first = g.Block(new TryGetComponentTask(TARGET, null));
            AgentState second = g.Block(new TryGetComponentTask { Type = "No.Such.Type" });
            AgentState third = g.Block(good);
            AgentState fourth = g.Block(new SetNumberTask { Variable = "Seen", Number = new GetValue { As = good.Component } });
            AgentState fifth = g.Block(new BranchTask(new IsCondition { As = good.Component, Member = "Fury" }));
            g.Do(always, first);
            g.Then(first, second);
            g.Then(second, third);
            g.Then(third, fourth);
            g.Then(fourth, fifth);
            var issues = new List<GraphIssue>();

            ConditionChecks.Check(g.Build(), issues);

            string all = string.Join("\n", issues);
            Assert.IsTrue(issues.Exists(issue => issue.Code == GraphIssueCode.BadComponent && issue.Message.Contains("no type")), all);
            Assert.IsTrue(issues.Exists(issue => issue.Code == GraphIssueCode.BadComponent && issue.Message.Contains("No.Such.Type")), all);
            Assert.IsTrue(issues.Exists(issue => issue.Code == GraphIssueCode.UnwiredValue && issue.Message.Contains("looks at")), all);
            Assert.IsTrue(issues.Exists(issue => issue.Code == GraphIssueCode.BadComponent && issue.Message.Contains("reads nothing")), all);
            Assert.IsTrue(issues.Exists(issue => issue.Code == GraphIssueCode.BadComponent && issue.Message.Contains("a number")), all);
            Assert.IsFalse(issues.Exists(issue => issue.Code == GraphIssueCode.UnwiredValue && issue.Message.Contains("reads the wired")),
                "A Get reading a component does not need its own Object: " + all);
        }

        [Test]
        public void Validator_ASensorLookingForAnUnknownType_IsAProblem()
        {
            var g = new BrainGraphBuilder();
            g.Variable(TARGET, VariableType.Transform);
            g.Sensor(new RadiusSensorTask { StoreIn = TARGET, OfType = "No.Such.Type" });
            g.Do(g.Always(), g.Block(new WaitTask()));
            var issues = new List<GraphIssue>();

            ConditionChecks.Check(g.Build(), issues);

            Assert.IsTrue(issues.Exists(issue => issue.Code == GraphIssueCode.BadComponent && issue.Message.Contains("looks for")),
                string.Join("\n", issues));
        }

        // ---------- sensors by type ----------

        [Test]
        public void Sensor_OfTypeAndNoTag_SeesOnlyObjectsWithTheComponent_TheNearest()
        {
            _rig.Context.Objects = TagObjectFinder.Shared;
            var sensor = new RadiusSensorTask { StoreIn = TARGET, Tag = "", OfType = typeof(Player).FullName, Radius = 10f, Interval = 0f };
            Unmarked nearer = Make<Unmarked>("Nearer", visibleToFind: true);
            nearer.transform.position = _rig.Context.Position + new Vector3(2f, 0f, 0f);
            Player player = Make<Player>("Player", visibleToFind: true);
            player.transform.position = _rig.Context.Position + new Vector3(5f, 0f, 0f);
            Player far = Make<Player>("Far", visibleToFind: true);
            far.transform.position = _rig.Context.Position + new Vector3(30f, 0f, 0f);

            sensor.Tick(_rig.Context, DT);

            Assert.AreSame(player.transform, _rig.Context.Blackboard.Get<Transform>(TARGET),
                "The one with the component, within Radius — not the nearer object without it");
        }

        [Test]
        public void Sensor_TagAndType_OfTheTagsObjects_OnlyThoseWithTheComponent()
        {
            var finder = new ListFinder();
            _rig.Context.Objects = finder;
            Unmarked nearer = Make<Unmarked>("Nearer");
            nearer.transform.position = _rig.Context.Position + new Vector3(2f, 0f, 0f);
            Player player = Make<Player>("Player");
            player.transform.position = _rig.Context.Position + new Vector3(5f, 0f, 0f);
            finder.Objects.Add(nearer.transform);
            finder.Objects.Add(player.transform);
            var sensor = new RadiusSensorTask { StoreIn = TARGET, Tag = "Player", Radius = 10f, Interval = 0f };

            sensor.Tick(_rig.Context, DT);
            Assert.AreSame(nearer.transform, _rig.Context.Blackboard.Get<Transform>(TARGET), "No type: the nearest of the tag");

            sensor.OfType = typeof(Player).FullName;
            _rig.Context.Blackboard.Set(TARGET, (Transform)null);
            sensor.Tick(_rig.Context, DT);
            Assert.AreSame(player.transform, _rig.Context.Blackboard.Get<Transform>(TARGET), "Only the one with the component");
        }

        [Test]
        public void Sensor_ByType_AFinderThatCannotLookByType_FindsNothing()
        {
            var finder = new ListFinder();
            _rig.Context.Objects = finder;
            Player player = Make<Player>("Player");
            player.transform.position = _rig.Context.Position + new Vector3(2f, 0f, 0f);
            finder.Objects.Add(player.transform);
            var sensor = new RadiusSensorTask { StoreIn = TARGET, Tag = "", OfType = typeof(Player).FullName, Radius = 10f, Interval = 0f };

            sensor.Tick(_rig.Context, DT);

            Assert.IsNull(_rig.Context.Blackboard.Get<Transform>(TARGET), "A game's own finder answers by type, or nothing");
        }

        // ---------- the editor's lists ----------

        [Test]
        public void Editor_TheTypesOffered_EveryComponentOfTheGameAndUnity_TheMarkedFirst_NotTheEditors()
        {
            List<string> types = NodeOptions.TypesOf(typeof(Component), anyComponent: true);

            CollectionAssert.Contains(types, typeof(Rigidbody).FullName);
            CollectionAssert.Contains(types, typeof(Transform).FullName);
            CollectionAssert.Contains(types, typeof(Marker).FullName, "Marked: offered though the test's own type is internal");
            CollectionAssert.DoesNotContain(types, typeof(Unmarked).FullName, "Not marked and not public");
            Assert.Less(types.IndexOf(typeof(AICharacter).FullName), types.IndexOf(typeof(Rigidbody).FullName),
                "The marked ones first");
            Assert.IsFalse(types.Exists(name => name.StartsWith("UnityEditor.", System.StringComparison.Ordinal)),
                "No component of the editor");
        }

        [Test]
        public void Editor_TheMembersOffered_AnIsWhetherItHasOneAndFlags_AGetNumbers()
        {
            ComponentValue cast = new TryGetComponentTask(TARGET, typeof(Player)).Component;

            List<(string Value, string Word)> flags = ElementInspector.MemberOptions(new IsCondition { As = cast }, "");
            CollectionAssert.AreEqual(new[]
            {
                ("", "(has a Player)"), ("IsBlocking", "Is Blocking"), ("enabled", "Enabled"),
                ("isActiveAndEnabled", "Is Active And Enabled")
            }, flags);

            List<(string Value, string Word)> numbers = ElementInspector.MemberOptions(new GetValue { As = cast }, "Gone");
            CollectionAssert.AreEqual(new[]
            {
                ("", "(none)"), ("_stamina", "Stamina"), ("_wide", "Wide"), ("Aim.x", "Aim X"), ("Aim.y", "Aim Y"),
                ("Aim.magnitude", "Aim Length"), ("Fury", "Rage"), ("Hidden", "Hidden"), ("_level", "Level"),
                ("Gone", "Gone (not found)")
            }, numbers);
        }

        // ---------- inside ----------

        private void Target(Transform target) => _rig.Context.Blackboard.Set(TARGET, target);

        private static TryGetComponentTask Look(System.Type type) => new(TARGET, type);

        private TaskStatus Run(TryGetComponentTask look) => look.Tick(_rig.Context, DT);

        // A scene object (visibleToFind — not hidden: FindObjectsByType skips objects that are not saved)
        private T Make<T>(string name, bool visibleToFind = false) where T : Component
        {
            var go = new GameObject(name);
            if (!visibleToFind)
                go.hideFlags = HideFlags.HideAndDontSave;
            _created.Add(go);
            return go.AddComponent<T>();
        }

        internal class Leveled : MonoBehaviour
        {
            [AIReadable] [SerializeField] private float _level;

            protected void SetLevel(float level) => _level = level;
        }

        internal sealed class Player : Leveled
        {
            [AIReadable] [SerializeField] private float _stamina;
            [AIReadable] [SerializeField] private double _wide;
            [AIReadable] [SerializeField] private string _name = "";
            [SerializeField] private float _hidden;
            public Vector2 Aim;

            [AIReadable("Rage")] public int Fury { get; private set; }

            [AIReadable] public bool IsBlocking { get; private set; }

            public float Hidden => _hidden;

            public string Name => _name;

            public void Set(float stamina, int fury, bool blocking, double wide, float level)
            {
                _stamina = stamina;
                _wide = wide;
                Fury = fury;
                IsBlocking = blocking;
                SetLevel(level);
            }
        }

        [AIReadable]
        internal sealed class Marker : MonoBehaviour
        {
        }

        internal sealed class Unmarked : MonoBehaviour
        {
            public float Speed;
        }

        [AIReadable]
        internal sealed class NotAComponent
        {
        }

        // The objects of a tag, as a game's finder would give them — one that cannot look by type
        private sealed class ListFinder : IObjectFinder
        {
            public readonly List<Transform> Objects = new();

            public Transform FindNearest(string kind, Vector3 center, float radius, Transform self) => null;

            public void FindAll(string kind, Vector3 center, float radius, Transform self, List<Transform> results)
            {
                results.Clear();
                foreach (Transform found in Objects)
                {
                    if (found != null && (found.position - center).sqrMagnitude <= radius * radius)
                        results.Add(found);
                }
            }
        }
    }
}
