using System.Collections;
using System.Reflection;
using Atomic.Entities;
using Game.Voxels;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// The tank's skin takes blood where gore hits it (TankBloodBehaviour), in VoxelSandbox: death goo thrown onto the
    /// hull is caught by the debris step and splats there, and a body part burst under a track bloodies that track only.
    /// </summary>
    public sealed class TankBloodSceneTests
    {
        private const string SCENE = "Assets/Game/Voxels/Scenes/VoxelSandbox.unity";

        private static readonly Color32[] Goo = { new Color(0.6f, 0.04f, 0.03f) };

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator GooOnTheHull_StainsTheSkin_AndASquash_BloodiesThatTrackOnly()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(SCENE, new LoadSceneParameters(LoadSceneMode.Single));
            for (int i = 0; i < 30; i++)
                yield return new WaitForFixedUpdate();

            FieldInfo on = typeof(VoxelAirStrike).GetField("_on", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (VoxelAirStrike strike in Object.FindObjectsByType<VoxelAirStrike>(FindObjectsSortMode.None))
                on?.SetValue(strike, false);

            GameObject tank = GameObject.Find("Tank_01");
            Assert.That(tank, Is.Not.Null, "the sandbox's tank");
            TankBloodBehaviour blood = null;
            foreach (object behaviour in ((IEntity)tank.GetComponent<MonoEntity>()).GetBehaviours())
                if (behaviour is TankBloodBehaviour found)
                    blood = found;
            Assert.That(blood, Is.Not.Null, "the tank has a TankBloodBehaviour");

            // Goo dropped onto the deck from above: it never collides with the tank, the debris step catches it.
            Vector3 above = tank.transform.position + Vector3.up * 3.5f;
            int thrown = VoxelSpray.Goo(above, Vector3.down, 16, 2f, 0.2f, Goo, 0.08f, default, 0.4f);
            Assert.That(thrown, Is.GreaterThan(0));
            int before = blood.Laid;
            for (int i = 0; i < 90 && blood.Laid == before; i++)
                yield return new WaitForFixedUpdate();
            int fromGoo = blood.Laid - before;
            Assert.That(fromGoo, Is.GreaterThan(0), "goo that hit the hull stained it");

            // A big part burst under the right track: that track only takes the blood (its belt, then its prints).
            Transform frame = tank.transform;
            var ruts = tank.GetComponent<VoxelTrackRuts>();
            Assert.That(ruts, Is.Not.Null, "the tank has VoxelTrackRuts");
            before = blood.Laid;
            TankBloodStains.Squash(frame.position + frame.right * 1.0f, 1f);
            for (int i = 0; i < 5; i++)
                yield return new WaitForFixedUpdate();
            Assert.That(blood.Laid - before, Is.GreaterThanOrEqualTo(1), "the squash was laid");
            Assert.That(ruts.TrackBlood(1), Is.GreaterThan(0.5f), "the right track is bloody");
            Assert.That(ruts.TrackBlood(0), Is.EqualTo(0f), "the left one is not");
            Debug.Log($"[TankBloodTest] goo stains {fromGoo}, right track blood {ruts.TrackBlood(1):F2}");
        }
    }
}
