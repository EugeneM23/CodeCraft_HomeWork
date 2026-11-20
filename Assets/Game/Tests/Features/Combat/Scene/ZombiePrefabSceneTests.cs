using System.Collections;
using System.Linq;
using System.Reflection;
using Atomic.Elements;
using Atomic.Entities;
using Game.Voxels;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// Zombie.prefab (the variant of Regax.prefab with the low-poly zombie's look, Tools/Zombie/Build Zombie Prefab)
    /// placed in VoxelSandbox: a URP skin on a humanoid rig under AC_Zombie, a hit plays TakeDamage, and the killing
    /// blow trades the skin for voxel flesh.
    /// </summary>
    public sealed class ZombiePrefabSceneTests
    {
        private const string SCENE = "Assets/Game/Voxels/Scenes/VoxelSandbox.unity";
        private const string ZOMBIE = "Assets/Game/Prefabs/Characters/Zombie.prefab";

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator Zombie_Stands_TakesAHit_AndDiesIntoVoxelFlesh()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(SCENE, new LoadSceneParameters(LoadSceneMode.Single));
            for (int i = 0; i < 30; i++)
                yield return new WaitForFixedUpdate();

            FieldInfo on = typeof(VoxelAirStrike).GetField("_on", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (VoxelAirStrike strike in Object.FindObjectsByType<VoxelAirStrike>(FindObjectsSortMode.None))
                on?.SetValue(strike, false);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ZOMBIE);
            Assert.That(prefab, Is.Not.Null, ZOMBIE + " (Tools/Zombie/Build Zombie Prefab)");
            GameObject zombie = Object.Instantiate(prefab, new Vector3(-20f, 0f, 12f), Quaternion.identity);
            yield return null;
            yield return null;

            Animator animator = zombie.GetComponentInChildren<Animator>();
            SkinnedMeshRenderer skin = zombie.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
            Assert.That(animator.avatar != null && animator.avatar.isHuman, Is.True, "a humanoid avatar");
            Assert.That(animator.runtimeAnimatorController.name, Is.EqualTo("AC_Zombie"), "the weapon switched to the zombie's controller");
            Assert.That(skin.sharedMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"), "no pink: URP");
            Assert.That(skin.bounds.size.y, Is.InRange(1.4f, 1.85f), "about 1.6 m tall");

            var entity = zombie.GetComponent<MonoEntity>();
            IEntity source = new Entity();
            source.AddDamage(new ReactiveVariable<int>(10));
            source.AddTransform(zombie.transform);

            DamageUseCase.TakeDamage(entity, new TakeDamageArgs(source));
            for (int i = 0; i < 10; i++)
                yield return null;
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("TakeDamage")
                        || animator.GetNextAnimatorStateInfo(0).IsName("TakeDamage"), Is.True, "a hit plays TakeDamage");

            int before = Object.FindObjectsByType<VoxelBody>(FindObjectsSortMode.None).Count(b => b.name.Contains(VoxelGibs.NAME));
            for (int i = 0; i < 10 && entity.TryGetHealth(out Health _); i++)
                DamageUseCase.TakeDamage(entity, new TakeDamageArgs(source));
            for (int i = 0; i < 6; i++)
                yield return null;

            int flesh = Object.FindObjectsByType<VoxelBody>(FindObjectsSortMode.None).Count(b => b.name.Contains(VoxelGibs.NAME)) - before;
            Assert.That(skin.enabled, Is.False, "the skin gave way to the flesh");
            Assert.That(flesh, Is.GreaterThanOrEqualTo(2), "the body broke into voxel pieces");
            foreach (VoxelBody piece in Object.FindObjectsByType<VoxelBody>(FindObjectsSortMode.None))
                if (piece.name.Contains(VoxelGibs.NAME) && piece.TryGetComponent(out Rigidbody body))
                    Assert.That(body.excludeLayers.value & (1 << PhysicsLayers.Character), Is.Not.Zero, "the flesh never touches the tank");
            Debug.Log($"[ZombieTest] flesh pieces {flesh}, height {skin.bounds.size.y:F2} m");
        }
    }
}
