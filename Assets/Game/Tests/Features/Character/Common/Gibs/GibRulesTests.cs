using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class GibRulesTests
    {
        private static readonly SeamLink[] Chain = { new(0, 1, 1f), new(1, 2, 1f) };

        [Test]
        public void EverySeamHolds_StillTwoClusters()
        {
            var held = new List<int>();
            int[] cluster = GibRules.RollClusters(3, Chain, () => 0f, held);

            Assert.AreEqual(2, cluster.Distinct().Count(), "a body that stayed whole is no burst");
            Assert.AreEqual(1, held.Count);
        }

        [Test]
        public void NoSeamHolds_EveryPieceAlone()
        {
            var held = new List<int>();
            int[] cluster = GibRules.RollClusters(3, Chain, () => 1f, held);

            Assert.AreEqual(3, cluster.Distinct().Count());
            Assert.IsEmpty(held);
        }

        [Test]
        public void AHeldSeam_KeepsItsPiecesTogether()
        {
            var seams = new[] { new SeamLink(0, 1, 1f), new SeamLink(1, 2, 0f) };
            var held = new List<int>();
            int[] cluster = GibRules.RollClusters(3, seams, () => 0.5f, held);

            Assert.AreEqual(cluster[0], cluster[1]);
            Assert.AreNotEqual(cluster[1], cluster[2]);
            CollectionAssert.AreEqual(new[] { 0 }, held);
        }

        [Test]
        public void NoHit_TheBodyItselfIsTheOrigin()
        {
            KillingHit hit = GibRules.ReadHit(null, new Vector3(1f, 2f, 3f));

            Assert.AreEqual(new Vector3(1f, 2f, 3f), hit.Origin);
            Assert.IsFalse(hit.Blast);
            Assert.AreEqual(0f, hit.Damage);
        }

        [Test]
        public void AClusterFliesAwayFromTheOrigin()
        {
            var speeds = new GibSpeeds { scatter = 0f, speedJitter = 0f };
            Vector3 velocity = GibRules.ClusterVelocity(new Vector3(2f, 0f, 0f), Vector3.zero, 3f, 0f, speeds);

            Assert.That(velocity.x, Is.EqualTo(3f).Within(1e-4f));
            Assert.That(velocity.y, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void ABlastThrowsByHowCloseItWent()
        {
            var speeds = new GibSpeeds();
            GibRules.Throw(speeds, new KillingHit(Vector3.zero, 0f, true, 1f), out float centre, out _, out _);
            GibRules.Throw(speeds, new KillingHit(Vector3.zero, 0f, true, 0f), out float edge, out _, out _);

            Assert.AreEqual(speeds.blastMaxSpeed, centre);
            Assert.AreEqual(speeds.blastMinSpeed, edge);
        }
    }
}
