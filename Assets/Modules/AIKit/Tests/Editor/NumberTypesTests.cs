using NUnit.Framework;

namespace AIKit.Tests
{
    /// <summary>
    /// The graph's variables are of any number type — int, float, double (the user's word, 15.11.2026): every number of
    /// the graph reads and writes any of them, each kept in its own type (an int rounds), nothing throws on a type of
    /// another number, and a read allocates nothing.
    /// </summary>
    public sealed class NumberTypesTests
    {
        [Test]
        public void Blackboard_ANumberReadAsAnyNumberType_WrittenInTheVariablesOwnType()
        {
            var board = new Blackboard();
            board.Declare("Count", 3);
            board.Declare("Wide", 2.5d);

            Assert.AreEqual(3f, board.Get("Count", 0f), "An int read as a float");
            Assert.AreEqual(2.5f, board.Get("Wide", 0f), "A double read as a float");
            Assert.AreEqual(3, board.Get("Wide", 0), "A double read as an int: rounded");

            board.Set("Count", 4.6f);
            Assert.IsTrue(board.TryGetKey("Count", out BlackboardKey<int> count), "It stays an int");
            Assert.AreEqual(5, board.Get(count), "A float written into an int: rounded");
            board.Set("Wide", 7);
            Assert.IsTrue(board.TryGetKey("Wide", out BlackboardKey<double> wide), "It stays a double");
            Assert.AreEqual(7d, board.Get(wide));
        }

        [Test]
        public void ANumberPin_WiredToAnIntOrADoubleVariable_ReadsIt()
        {
            var rig = new GraphRig(trace: false);
            try
            {
                rig.Context.Blackboard.Declare("Count", 3);
                rig.Context.Blackboard.Declare("Wide", 1.25d);

                Assert.AreEqual(3f, BlackboardValue<float>.Variable("Count").Get(rig.Context));
                Assert.AreEqual(1.25f, BlackboardValue<float>.Variable("Wide").Get(rig.Context));
            }
            finally
            {
                GraphRig.DestroyAll();
            }
        }

        [Test]
        public void SetAndAdd_OnIntAndDoubleVariables_KeepTheirType()
        {
            var rig = new GraphRig(trace: false);
            try
            {
                Blackboard board = rig.Context.Blackboard;
                board.Declare("Count", 1);
                board.Declare("Wide", 0.1d);

                new AddNumberTask { Variable = "Count", Value = 2.4f }.Tick(rig.Context, 0.1f);
                new AddNumberTask { Variable = "Wide", Value = 0.2f }.Tick(rig.Context, 0.1f);
                new SetNumberTask { Variable = "Wide", Value = 4f }.Tick(rig.Context, 0.1f);
                new MultiplyNumberTask { Variable = "Count", Value = 3f }.Tick(rig.Context, 0.1f);

                Assert.IsTrue(board.TryGetKey("Count", out BlackboardKey<int> count));
                Assert.AreEqual(9, board.Get(count), "1 + 2.4 → 3, × 3 → 9: an int all along");
                Assert.IsTrue(board.TryGetKey("Wide", out BlackboardKey<double> wide));
                Assert.AreEqual(4d, board.Get(wide));
            }
            finally
            {
                GraphRig.DestroyAll();
            }
        }

        [Test]
        public void Compare_OfADoubleVariable_ComparesItsNumber()
        {
            var rig = new GraphRig(trace: false);
            try
            {
                rig.Context.Blackboard.Declare("Wide", 2.5d);
                var more = new CompareCondition("Wide", NumberComparison.Greater, 2f) { Kind = VariableType.Double };

                Assert.AreEqual(Truth.Yes, more.Ask(rig.Context));
            }
            finally
            {
                GraphRig.DestroyAll();
            }
        }

        [Test]
        public void Declaration_OfADouble_DeclaresADoubleWithItsStartValue()
        {
            var board = new Blackboard();
            new VariableDeclaration("Wide", VariableType.Double) { Double = 1.5d }.DeclareOn(board);

            Assert.IsTrue(board.TryGetKey("Wide", out BlackboardKey<double> wide));
            Assert.AreEqual(1.5d, board.Get(wide));
            Assert.AreEqual(typeof(double), new VariableDeclaration("Wide", VariableType.Double).ValueType);
        }

        [Test]
        public void ReadingAnIntAsAFloat_AllocatesNothing()
        {
            var board = new Blackboard();
            board.Declare("Count", 3);
            var variable = new BlackboardVariable<float>();
            const string NAME = "Count";
            TestDelegate read = () => variable.Get(board, NAME);
            read();                                         // the first call compiles

            Assert.That(read, NoAllocation.Constraint);
        }
    }
}
