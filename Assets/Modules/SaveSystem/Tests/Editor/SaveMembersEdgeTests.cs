using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Saving.Objects;
using UnityEngine;
using UnityEngine.TestTools;

namespace Saving.Tests
{
    /// <summary>Which member types a save can hold, keys used twice, and what a member tells about itself.</summary>
    public class SaveMembersEdgeTests
    {
        private struct Point
        {
            public int X;
            public int Y;
        }

        private abstract class Shape
        {
            public int Sides;
        }

        private sealed class Kinds
        {
            public int[] Flat;
            public int[,] Grid;
            public Dictionary<string, int> ByName;
            public Dictionary<Vector2, int> ByPoint;
            public Queue<int> Queue;
            public IDisposable Handle;
            public Shape Outline;
            public Uri Address;
            public Point Spot;
            public AnimationCurve Curve;
        }

        private sealed class TwiceKeyed
        {
            [Save("same")] public int First;
            [Save("same")] public int Second;
        }

        [Test]
        public void All_PlainClass_OnlyTypesThatGoThroughJson()
        {
            string[] names = SaveMembers.All(typeof(Kinds)).Select(member => member.Name).ToArray();

            CollectionAssert.IsSupersetOf(names, new[] { "Flat", "ByName", "Spot" });
            foreach (string skipped in new[] { "Grid", "ByPoint", "Queue", "Handle", "Outline", "Address", "Curve" })
                CollectionAssert.DoesNotContain(names, skipped);
        }

        [Test]
        public void Marked_KeyUsedTwice_SecondSkippedWithError()
        {
            SaveMembers.ForgetTypes(); // the error comes on the first look at the type only

            LogAssert.Expect(LogType.Error, new Regex("key 'same' is used twice in TwiceKeyed"));
            IReadOnlyList<SaveMember> members = SaveMembers.Marked(typeof(TwiceKeyed));

            Assert.AreEqual(1, members.Count);
            Assert.AreEqual("First", members[0].Name);
        }

        [Test]
        public void SaveMembers_NullType_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => SaveMembers.Marked(null));
            Assert.Throws<ArgumentNullException>(() => SaveMembers.All(null));
            Assert.Throws<ArgumentNullException>(() => SaveMembers.Candidates(null));
            Assert.IsFalse(SaveMembers.HoldsUnityObjects(null));
        }

        [Test]
        public void SaveMember_NameKeyKindAndReference()
        {
            SaveMember renamed = SaveMembers.Marked(typeof(PropertyProbe)).Single(member => member.Name == "Renamed");
            SaveMember score = SaveMembers.Marked(typeof(PropertyProbe)).Single(member => member.Name == "Score");
            SaveMember target = SaveMembers.Candidates(typeof(MembersProbe)).Single(member => member.Name == "Target");
            SaveMember health = SaveMembers.Candidates(typeof(MembersProbe)).Single(member => member.Name == "Health");

            Assert.AreEqual("Renamed (_old)", renamed.ToString());
            Assert.AreEqual("Score", score.ToString());
            Assert.IsTrue(renamed.IsProperty);
            Assert.IsFalse(health.IsProperty);
            Assert.IsTrue(target.IsReference);
            Assert.IsFalse(health.IsReference);
        }
    }
}
