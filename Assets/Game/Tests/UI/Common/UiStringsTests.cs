using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class UiStringsTests
    {
        private static readonly Regex KeyPattern = new(@"^[a-z0-9_]+(\.[a-z0-9_]+)+$");

        [Test]
        public void RussianTable_EveryKey_HasText()
        {
            foreach (KeyValuePair<string, string> entry in UiStringTable.Russian)
                Assert.That(string.IsNullOrWhiteSpace(entry.Value), Is.False, $"Empty text for {entry.Key}");
        }

        [Test]
        public void RussianTable_EveryKey_IsDottedLowercase()
        {
            foreach (string key in UiStringTable.Russian.Keys)
                Assert.That(KeyPattern.IsMatch(key), Is.True, $"Bad key {key}");
        }

        [Test]
        public void RussianTable_EveryFormatKey_HasAPlaceholder()
        {
            foreach (KeyValuePair<string, string> entry in UiStringTable.Russian)
            {
                if (entry.Key.EndsWith("_format"))
                    Assert.That(entry.Value, Does.Contain("{0"), $"No {{0}} in {entry.Key}");
            }
        }

        [Test]
        public void Get_KnownKey_ReturnsItsText()
        {
            Assert.That(UiStrings.Get("game.title"), Is.EqualTo(UiStringTable.Russian["game.title"]));
        }

        [Test]
        public void Get_MissingKey_ReturnsTheKeyInBracketsWithoutThrowing()
        {
            string text = null;

            Assert.DoesNotThrow(() => text = UiStrings.Get("tests.no_such_key"));
            Assert.That(text, Is.EqualTo("[tests.no_such_key]"));
        }

        [Test]
        public void Get_NullKey_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => UiStrings.Get(null));
        }
    }
}
