using AIKit.Editor;
using NUnit.Framework;

namespace AIKit.Tests
{
    /// <summary>
    /// The question of a Branch as a sentence (what the canvas shows instead of a form of fields): a Compare reads in
    /// plain words by the kind of its variable — a number with the words of a count, a flag on or off, an object there
    /// or not; an AND / OR reads by its name.
    /// </summary>
    public sealed class ArrowPhraseTests
    {
        private const string AMMO = "Ammo";

        [TestCase(NumberComparison.Less, 5f, "Ammo is less than 5")]
        [TestCase(NumberComparison.LessOrEqual, 5f, "Ammo is at most 5")]
        [TestCase(NumberComparison.Greater, 2.5f, "Ammo is more than 2.5")]
        [TestCase(NumberComparison.GreaterOrEqual, 8f, "Ammo is at least 8")]
        public void Compare_ANumber_ReadsAsACount(NumberComparison comparison, float value, string expected)
        {
            var compare = new CompareCondition(AMMO, comparison, value);

            Assert.AreEqual(expected, ArrowPhrases.Phrase(compare, null));
        }

        [Test]
        public void Compare_AgainstAnotherVariable_NamesIt()
        {
            var compare = new CompareCondition(AMMO, NumberComparison.GreaterOrEqual, 3f) { Other = "Needed" };

            Assert.AreEqual("Ammo is at least Needed", ArrowPhrases.Phrase(compare, null));
        }

        [Test]
        public void Compare_WithNoVariableWiredIn_SaysSomething()
        {
            var compare = new CompareCondition { Comparison = NumberComparison.Less, Value = 3f };

            Assert.AreEqual($"{ArrowPhrases.NOBODY} is less than 3", ArrowPhrases.Phrase(compare, null));
        }

        [Test]
        public void Compare_AFlag_ReadsOnOrOff()
        {
            Assert.AreEqual("Alarm is on", ArrowPhrases.Phrase(new CompareCondition("Alarm", VariableType.Bool), null));
            Assert.AreEqual("Alarm is off",
                ArrowPhrases.Phrase(new CompareCondition("Alarm", VariableType.Bool, expected: false), null));
        }

        [Test]
        public void Compare_AnObject_ReadsAsThereIs()
        {
            Assert.AreEqual("there is Night",
                ArrowPhrases.Phrase(new CompareCondition("Night", VariableType.Transform), null));
            Assert.AreEqual("there is no Night",
                ArrowPhrases.Phrase(new CompareCondition("Night", VariableType.Transform, expected: false), null));
        }

        [Test]
        public void AndOr_WithoutAPhraseOfTheirOwn_ReadByTheirName()
        {
            // Step 90: the operators are called AND / OR in the registry (spec 4.2), the phrase reads them so
            Assert.AreEqual("and", ArrowPhrases.Phrase(new AllCondition(new CompareCondition()), null));
            Assert.AreEqual("or", ArrowPhrases.Phrase(new AnyCondition(new CompareCondition()), null));
        }

        [Test]
        public void TheCanvasOffersItsConditions_ByPlainNames()
        {
            Assert.AreEqual("Compare", ArrowPhrases.Title(new CompareCondition()));
            Assert.AreEqual("AND", ArrowPhrases.Title(new AllCondition()));     // step 90: AND / OR in the registry
            Assert.AreEqual("OR", ArrowPhrases.Title(new AnyCondition()));
        }
    }
}
