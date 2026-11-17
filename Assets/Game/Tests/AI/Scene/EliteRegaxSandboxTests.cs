using System.Collections;
using System.Reflection;
using Atomic.Entities;
using Game.Voxels;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.AIKit.Adapters.Tests
{
    /// <summary>
    /// The elite Regax in VoxelSandbox with the game's own tank: it crosses a fresh crater, reaches the tank and bites
    /// it; a blast next to it makes it leap away, then it comes back to the fight.
    /// </summary>
    public sealed class EliteRegaxSandboxTests
    {
        private const string SCENE = "Assets/Game/Voxels/Scenes/VoxelSandbox.unity";
        private const string ELITE = "Assets/Game/Prefabs/Characters/Regax_Elite.prefab";

        private Transform _tank;
        private Health _tankHealth;
        private GameObject _elitePrefab;

        private IEnumerator LoadSandbox()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(SCENE, new LoadSceneParameters(LoadSceneMode.Single));
            for (int i = 0; i < 60; i++)
                yield return new WaitForFixedUpdate();

            // The sandbox's air strikes would blow up the elites at random: off for the test.
            FieldInfo on = typeof(VoxelAirStrike).GetField("_on", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (VoxelAirStrike strike in Object.FindObjectsByType<VoxelAirStrike>(FindObjectsSortMode.None))
                on?.SetValue(strike, false);

            // The sandbox's own elites (Tools/AI/Add Elite Regax To Sandbox) would bite the tank too: only the test's elite fights.
            foreach (GameObject placed in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (placed.name.StartsWith("Regax_Elite_"))
                    placed.SetActive(false);

            _tank = GameObject.Find("Tank_01").transform;
            Assert.IsTrue(_tank.GetComponent<MonoEntity>().TryGetHealth(out _tankHealth), "the tank has health");
            _elitePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ELITE);
            Assert.That(_elitePrefab, Is.Not.Null, ELITE);
        }

        private Vector3 Forward()
        {
            Vector3 forward = _tank.forward;
            forward.y = 0f;
            return forward.normalized;
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator Elite_CrossesACrater_ReachesTheTank_AndBitesIt()
        {
            yield return LoadSandbox();

            // A crater 12 m ahead of the tank, the elite 22 m out beyond it.
            Vector3 craterAt = _tank.position + Forward() * 12f;
            Assert.IsTrue(Physics.Raycast(craterAt + Vector3.up * 20f, Vector3.down, out RaycastHit ground, 60f, 1 << PhysicsLayers.Ground));
            VoxelBody terrain = ground.collider.GetComponentInParent<VoxelBody>();
            Assert.That(terrain.CarveCrater(ground.point, 5f, 35f, 2f, 100), Is.GreaterThan(0), "a crater was dug");

            Vector3 at = EliteRegaxSpawn.GroundNear(_tank.position, Forward(), 22f);
            GameObject elite = EliteRegaxSpawn.Spawn(_elitePrefab, at, -Forward());
            int startHealth = _tankHealth.GetCurrent();

            float lowest = float.MaxValue;
            float waited = 0f;
            while (waited < 25f && _tankHealth.GetCurrent() >= startHealth)
            {
                yield return null;
                waited += Time.deltaTime;
                lowest = Mathf.Min(lowest, elite.transform.position.y);
            }

            Debug.Log($"[EliteTest] tank health {startHealth} -> {_tankHealth.GetCurrent()} after {waited:F1} s; elite lowest y {lowest:F2}");
            Assert.That(_tankHealth.GetCurrent(), Is.LessThan(startHealth), "the elite reached the tank and bit it within 25 s");
            Assert.That(lowest, Is.LessThan(-0.3f), "it went through the crater, following the dug ground");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator Elite_LeapsAwayFromABlast_ThenComesBack()
        {
            yield return LoadSandbox();

            Vector3 at = EliteRegaxSpawn.GroundNear(_tank.position, Forward(), 8f);
            GameObject elite = EliteRegaxSpawn.Spawn(_elitePrefab, at, -Forward());
            for (int i = 0; i < 30; i++)
                yield return null;

            // The blast 1.5 m on the tank's side of it: away from the blast is away from the tank.
            Vector3 position = elite.transform.position;
            Vector3 toTank = _tank.position - position;
            toTank.y = 0f;
            Vector3 blast = position + toTank.normalized * 1.5f;
            ExplosionEvents.Raise(blast, 3f);

            float farthest = 0f;
            for (float t = 0f; t < 2f; t += Time.deltaTime)
            {
                yield return null;
                farthest = Mathf.Max(farthest, Flat(elite.transform.position - blast));
            }

            Debug.Log($"[EliteTest] farthest from the blast {farthest:F2} m");
            Assert.That(farthest, Is.GreaterThan(3f), "it leapt away from the blast");

            float nearest = float.MaxValue;
            for (float t = 0f; t < 8f; t += Time.deltaTime)
            {
                yield return null;
                nearest = Mathf.Min(nearest, Flat(elite.transform.position - _tank.position));
            }

            Assert.That(nearest, Is.LessThan(5f), "then it came back to the tank");
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
