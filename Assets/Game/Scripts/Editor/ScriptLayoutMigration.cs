using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// One-shot migration of the script tree from a layer-first layout
    /// (GameEntities/{Core,Content,View}) to a feature-first one, per
    /// Docs/superpowers/specs/2026-11-04-scripts-by-feature.md.
    /// </summary>
    /// <remarks>
    /// Every move goes through <see cref="AssetDatabase.MoveAsset"/>, which carries the .meta file
    /// along with the asset and so keeps its GUID. Around forty of these scripts are MonoBehaviours
    /// referenced by prefabs and scenes; a filesystem move that left the .meta behind would mint a
    /// new GUID and strand every one of those references.
    ///
    /// The table below is this migration's only record of what goes where — the spec deliberately
    /// does not repeat it, so there is one copy to keep honest rather than two. It is a flat
    /// file-to-file list on purpose: a folder-to-folder table would be a third of the length but
    /// would need an exception list and a rename list beside it, and three mechanisms is three
    /// chances to be subtly wrong about a file nobody checks again.
    ///
    /// Re-running is safe: a row whose source is already gone is skipped, not failed.
    /// </remarks>
    public static class ScriptLayoutMigration
    {
        private const string Tag = "[LAYOUT]";

        private static readonly string[] Deletions =
        {
            // The older playground builder. It rebuilds the scene that the box playground replaced,
            // and its menu entry sits right next to the live one.
            "Assets/Tools/Playground",
            // Not referenced by any scene — checked by GUID against both of them.
            "Assets/Game/Scripts/Gameplay/Level/PlaygroundOverlay.cs",
            // The on-screen sticks are gone: nothing reads GetJoystick any more.
            "Assets/Modules/Joystick.cs",
            // Commented out from the first line to the last, so they declare no type at all —
            // which is why these were the four files in the project with no namespace. No .cs
            // references them, and no scene or prefab holds their GUIDs.
            "Assets/Game/Scripts/Gameplay/GameEntities/View/MeleeAttack/AttackSoundBehaviour.cs",
            "Assets/Game/Scripts/Gameplay/GameEntities/View/BodyFall/BodyFallBloodBehaviour.cs",
            "Assets/Game/Scripts/Gameplay/GameEntities/View/BodyFall/BodyFallSoundBehaviour.cs",
            "Assets/Game/Scripts/Gameplay/GameEntities/View/TakeDamage/TakeDamageSoundTypeBehaviour.cs",
        };

        /// <summary>source|destination, both relative to Assets/.</summary>
        private static readonly string[] Moves =
        {
            "Game/Scripts/Gameplay/CodeGen/EntityAPI.cs|Game/Scripts/Declarations/EntityAPI.cs",
            "Game/Scripts/Gameplay/CodeGen/GameContextAPI.cs|Game/Scripts/Declarations/GameContextAPI.cs",
            "Game/Scripts/Gameplay/CodeGen/PlayerContextAPI.cs|Game/Scripts/Declarations/PlayerContextAPI.cs",
            "Game/Scripts/Gameplay/CodeGen/PlayerID.cs|Game/Scripts/Declarations/PlayerID.cs",
            "Game/Scripts/Gameplay/Abilities/AbilityAPI.cs|Game/Scripts/Declarations/AbilityAPI.cs",
            "Game/Scripts/Gameplay/GameContext/GameContext.cs|Game/Scripts/Contexts/Game/GameContext.cs",
            "Game/Scripts/Gameplay/GameContext/GameContextInstaller.cs|Game/Scripts/Contexts/Game/GameContextInstaller.cs",
            "Game/Scripts/Gameplay/GameContext/IGameContext.cs|Game/Scripts/Contexts/Game/IGameContext.cs",
            "Game/Scripts/Gameplay/GameContext/WeaponCatalog.cs|Game/Scripts/Features/Combat/Model/WeaponCatalog.cs",
            "Game/Scripts/Gameplay/GameContext/WeaponID.cs|Game/Scripts/Features/Combat/Model/WeaponID.cs",
            "Game/Scripts/Gameplay/PlayerContext/PlayerContext.cs|Game/Scripts/Contexts/Player/PlayerContext.cs",
            "Game/Scripts/Gameplay/PlayerContext/PlayerContextInstaller.cs|Game/Scripts/Contexts/Player/PlayerContextInstaller.cs",
            "Game/Scripts/Gameplay/PlayerContext/IPlayerContext.cs|Game/Scripts/Contexts/Player/IPlayerContext.cs",
            "Game/Scripts/Gameplay/PlayerContext/PlayerUseCase.cs|Game/Scripts/Contexts/Player/PlayerUseCase.cs",
            "Game/Scripts/Gameplay/PlayerContext/CharacterSystemInstaller.cs|Game/Scripts/Contexts/Player/CharacterSystemInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Move/CharacterMoveBehaviour.cs|Game/Scripts/Features/Movement/Model/CharacterMoveBehaviour.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Move/CharacterRotateBehaviour.cs|Game/Scripts/Features/Movement/Model/CharacterRotateBehaviour.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Move/CharacterVelocityBehaviour.cs|Game/Scripts/Features/Movement/Model/CharacterVelocityBehaviour.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Move/MoveUsecase.cs|Game/Scripts/Features/Movement/Model/MoveUseCase.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Move/RigidbodyMoveBehaviour.cs|Game/Scripts/Features/Movement/Model/RigidbodyMoveBehaviour.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Rotate/RigidbodyRotateBehaviour.cs|Game/Scripts/Features/Movement/Model/RigidbodyRotateBehaviour.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Rotate/RotateUseCase.cs|Game/Scripts/Features/Movement/Model/RotateUseCase.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Jump/JumpUseCase.cs|Game/Scripts/Features/Movement/Model/JumpUseCase.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/GravityScaler.cs|Game/Scripts/Features/Movement/Model/GravityScaler.cs",
            "Game/Scripts/Gameplay/GameEntities/View/Move/CharacterMoveAnimBehaviour.cs|Game/Scripts/Features/Movement/View/CharacterMoveAnimBehaviour.cs",
            "Game/Scripts/Gameplay/GameEntities/View/Move/MoveStepSoundBehaviour.cs|Game/Scripts/Features/Movement/View/MoveStepSoundBehaviour.cs",
            "Game/Scripts/Gameplay/PlayerContext/Controllers/CharacterMoveController.cs|Game/Scripts/Features/Movement/Input/CharacterMoveController.cs",
            "Game/Scripts/Gameplay/PlayerContext/Controllers/CharacterRotateController.cs|Game/Scripts/Features/Movement/Input/CharacterRotateController.cs",
            "Game/Scripts/Gameplay/PlayerContext/Controllers/CharacterJumpController.cs|Game/Scripts/Features/Movement/Input/CharacterJumpController.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Fire/CharacterFireAction.cs|Game/Scripts/Features/Combat/Model/CharacterFireAction.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Fire/RangeWeaponFireAction.cs|Game/Scripts/Features/Combat/Model/RangeWeaponFireAction.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Bullet/BulletCollisionBehaviour.cs|Game/Scripts/Features/Combat/Model/BulletCollisionBehaviour.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/MeleeAttack/DamageCastBehaviour.cs|Game/Scripts/Features/Combat/Model/DamageCastBehaviour.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Cooldown/TickCooldownBehaviour.cs|Game/Scripts/Features/Combat/Model/TickCooldownBehaviour.cs",
            "Modules/Cooldown.cs|Game/Scripts/Features/Combat/Model/CooldownTimer.cs",
            "Modules/Ammo.cs|Game/Scripts/Features/Combat/Model/Ammo.cs",
            "Game/Scripts/Gameplay/GameEntities/View/Fire/AttackSoundBehaviour.cs|Game/Scripts/Features/Combat/View/AttackSoundBehaviour.cs",
            "Game/Scripts/Gameplay/GameEntities/View/Hit/HitSoundBehaviour.cs|Game/Scripts/Features/Combat/View/HitSoundBehaviour.cs",
            "Game/Scripts/Gameplay/PlayerContext/Controllers/CharacterFireController.cs|Game/Scripts/Features/Combat/Input/CharacterFireController.cs",
            "Modules/Health.cs|Game/Scripts/Features/Vitals/Model/Health.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/TakeDamage/DamageUseCase.cs|Game/Scripts/Features/Vitals/Model/DamageUseCase.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/TakeDamage/TakeDamageArgs.cs|Game/Scripts/Features/Vitals/Model/TakeDamageArgs.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Death/DeathBehaviour.cs|Game/Scripts/Features/Vitals/Model/DeathBehaviour.cs",
            "Game/Scripts/Gameplay/GameEntities/View/TakeDamage/PainSoundBehaviour.cs|Game/Scripts/Features/Vitals/View/PainSoundBehaviour.cs",
            "Game/Scripts/Gameplay/GameEntities/View/TakeDamage/TakeDamageBloodBehaviour.cs|Game/Scripts/Features/Vitals/View/TakeDamageBloodBehaviour.cs",
            "Game/Scripts/Gameplay/GameEntities/View/TakeDamage/TakeDamageSoundBehaviour.cs|Game/Scripts/Features/Vitals/View/TakeDamageSoundBehaviour.cs",
            "Game/Scripts/Gameplay/GameEntities/View/Death/DeathAnimBehaviour.cs|Game/Scripts/Features/Vitals/View/DeathAnimBehaviour.cs",
            "Game/Scripts/Gameplay/GameEntities/View/Death/DeathSoundBehaviour.cs|Game/Scripts/Features/Vitals/View/DeathSoundBehaviour.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Buffs/BuffBase.cs|Game/Scripts/Features/Buffs/Model/BuffBase.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Buffs/BuffConfig.cs|Game/Scripts/Features/Buffs/Model/BuffConfig.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Buffs/BuffUseCase.cs|Game/Scripts/Features/Buffs/Model/BuffUseCase.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Buffs/DiscardAllBuffBehaviour.cs|Game/Scripts/Features/Buffs/Model/DiscardAllBuffBehaviour.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Buffs/TemporaryBuff.cs|Game/Scripts/Features/Buffs/Model/TemporaryBuff.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Buffs/ExtraDamageBuff.cs|Game/Scripts/Features/Buffs/Model/ExtraDamageBuff.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Buffs/ExtraDamageBuffConfig.cs|Game/Scripts/Features/Buffs/Model/ExtraDamageBuffConfig.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Buffs/JumpBuff.cs|Game/Scripts/Features/Buffs/Model/JumpBuff.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Buffs/JumpBuffConfig.cs|Game/Scripts/Features/Buffs/Model/JumpBuffConfig.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Buffs/MoveSpeedBuff.cs|Game/Scripts/Features/Buffs/Model/MoveSpeedBuff.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Buffs/MoveSpeedBuffConfig.cs|Game/Scripts/Features/Buffs/Model/MoveSpeedBuffConfig.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Buffs/StunBuff.cs|Game/Scripts/Features/Buffs/Model/StunBuff.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Buffs/StunBuffConfig.cs|Game/Scripts/Features/Buffs/Model/StunBuffConfig.cs",
            "Game/Scripts/Gameplay/GameEntities/View/Buffs/BuffVFXBehaviour.cs|Game/Scripts/Features/Buffs/View/BuffVFXBehaviour.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Buffs/CharacterDebug.cs|Game/Scripts/Sandbox/CharacterDebug.cs",
            "Game/Scripts/Gameplay/Abilities/Ability.cs|Game/Scripts/Features/Abilities/Model/Ability.cs",
            "Game/Scripts/Gameplay/Abilities/AbilityConfig.cs|Game/Scripts/Features/Abilities/Model/AbilityConfig.cs",
            "Game/Scripts/Gameplay/Abilities/Content/DashAbilityConfig.cs|Game/Scripts/Features/Abilities/Model/DashAbilityConfig.cs",
            "Game/Scripts/Gameplay/Abilities/Content/StunAbilityConfig.cs|Game/Scripts/Features/Abilities/Model/StunAbilityConfig.cs",
            "Game/Scripts/Gameplay/Abilities/Content/TeleportAbilityConfig.cs|Game/Scripts/Features/Abilities/Model/TeleportAbilityConfig.cs",
            "Game/Scripts/Gameplay/PlayerContext/Ability/AbilitySystemInstaller.cs|Game/Scripts/Features/Abilities/Model/AbilitySystemInstaller.cs",
            "Game/Scripts/Gameplay/PlayerContext/Ability/AbilityUseCase.cs|Game/Scripts/Features/Abilities/Model/AbilityUseCase.cs",
            "Game/Scripts/Gameplay/PlayerContext/Ability/AbillityRunner.cs|Game/Scripts/Features/Abilities/Model/AbilityRunner.cs",
            "Game/Scripts/Gameplay/PlayerContext/Mana/ManaSystemInstaller.cs|Game/Scripts/Features/Abilities/Model/ManaSystemInstaller.cs",
            "Game/Scripts/Gameplay/PlayerContext/Mana/ManaUseCase.cs|Game/Scripts/Features/Abilities/Model/ManaUseCase.cs",
            "Game/Scripts/Gameplay/PlayerContext/Mana/RestoreManaBehaviour.cs|Game/Scripts/Features/Abilities/Model/RestoreManaBehaviour.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Interact/DetectInteractableBehaviour.cs|Game/Scripts/Features/Interaction/Model/DetectInteractableBehaviour.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Interact/DropWeaponUseCase.cs|Game/Scripts/Features/Interaction/Model/DropWeaponUseCase.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Interact/InteractInstaller.cs|Game/Scripts/Features/Interaction/Model/InteractInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Interact/InteractUseCase.cs|Game/Scripts/Features/Interaction/Model/InteractUseCase.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Interact/PickUpWeaponUseCase.cs|Game/Scripts/Features/Interaction/Model/PickUpWeaponUseCase.cs",
            "Game/Scripts/Gameplay/GameEntities/View/Interact/InteractMarkerHighlightBehaviour.cs|Game/Scripts/Features/Interaction/View/InteractMarkerHighlightBehaviour.cs",
            "Game/Scripts/Gameplay/GameEntities/View/Interact/InteractMarkerUIBehaviour.cs|Game/Scripts/Features/Interaction/View/InteractMarkerUIBehaviour.cs",
            "Game/Scripts/Gameplay/PlayerContext/Controllers/CharacterInteractController.cs|Game/Scripts/Features/Interaction/Input/CharacterInteractController.cs",
            "Game/Scripts/Gameplay/PlayerContext/Controllers/CharacterDropWeaponController.cs|Game/Scripts/Features/Interaction/Input/CharacterDropWeaponController.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Enemy/DirectionUseCase.cs|Game/Scripts/Features/EnemyAI/Model/DirectionUseCase.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Enemy/EnemyAttackBehaviour.cs|Game/Scripts/Features/EnemyAI/Model/EnemyAttackBehaviour.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Enemy/EnemyChaseBehaviour.cs|Game/Scripts/Features/EnemyAI/Model/EnemyChaseBehaviour.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Enemy/EnemyPatrolBehaviour.cs|Game/Scripts/Features/EnemyAI/Model/EnemyPatrolBehaviour.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Enemy/PatrolUseCase.cs|Game/Scripts/Features/EnemyAI/Model/PatrolUseCase.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Sensors/FindTargetUseCase.cs|Game/Scripts/Features/EnemyAI/Model/FindTargetUseCase.cs",
            "Game/Scripts/Gameplay/GameEntities/View/Enemy/EnemyAttackAnimBehaviour.cs|Game/Scripts/Features/EnemyAI/View/EnemyAttackAnimBehaviour.cs",
            "Game/Scripts/Gameplay/GameEntities/View/Enemy/EnemyTakeDamageAnimBehaviour.cs|Game/Scripts/Features/EnemyAI/View/EnemyTakeDamageAnimBehaviour.cs",
            "Game/Scripts/Gameplay/GameEntities/View/Camera/CameraFollowUseCase.cs|Game/Scripts/Features/Cameras/CameraFollowUseCase.cs",
            "Game/Scripts/Gameplay/GameEntities/View/Camera/CameraShakeArgs.cs|Game/Scripts/Features/Cameras/CameraShakeArgs.cs",
            "Game/Scripts/Gameplay/GameEntities/View/Camera/CameraShakeBehaviour.cs|Game/Scripts/Features/Cameras/CameraShakeBehaviour.cs",
            "Game/Scripts/Gameplay/GameEntities/View/Camera/ShakeTransformUseCase.cs|Game/Scripts/Features/Cameras/ShakeTransformUseCase.cs",
            "Game/Scripts/Gameplay/PlayerContext/Camera/CameraFollowController.cs|Game/Scripts/Features/Cameras/CameraFollowController.cs",
            "Game/Scripts/Gameplay/PlayerContext/Camera/CameraInstaller.cs|Game/Scripts/Features/Cameras/CameraInstaller.cs",
            "Game/Scripts/Gameplay/PlayerContext/Camera/CameraSystemInstaller.cs|Game/Scripts/Features/Cameras/CameraSystemInstaller.cs",
            "Game/Scripts/Gameplay/PlayerContext/Controllers/CharacterShakeController.cs|Game/Scripts/Features/Cameras/CharacterShakeController.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/GameFactory/EntityPrefabPool.cs|Game/Scripts/Features/Spawning/EntityPrefabPool.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/GameFactory/GameFactory.cs|Game/Scripts/Features/Spawning/GameFactory.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/GameFactory/GameFactoryID.cs|Game/Scripts/Features/Spawning/GameFactoryID.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/SpawnEntity/SpawnUseCase.cs|Game/Scripts/Features/Spawning/SpawnUseCase.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/LifeTime/LifeTimeBehaviour.cs|Game/Scripts/Features/Spawning/LifeTimeBehaviour.cs",
            "Game/Scripts/Gameplay/Level/EnemyTrigger.cs|Game/Scripts/Features/Spawning/EnemyTrigger.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/Character/CharacterCoreInstaller.cs|Game/Scripts/Entities/Character/CharacterCoreInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/Character/CharacterAnimInstaller.cs|Game/Scripts/Entities/Character/CharacterAnimInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/Character/CharacterAudioInstaller.cs|Game/Scripts/Entities/Character/CharacterAudioInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/Character/CharacterVfxInstaller.cs|Game/Scripts/Entities/Character/CharacterVfxInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/Character/CharacterBuffInstaller.cs|Game/Scripts/Entities/Character/CharacterBuffInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/Enemy/EnemyCoreInstaller.cs|Game/Scripts/Entities/Enemy/EnemyCoreInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/Enemy/EnemyAnimInstaller.cs|Game/Scripts/Entities/Enemy/EnemyAnimInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/Enemy/EnemyAudioInstaller.cs|Game/Scripts/Entities/Enemy/EnemyAudioInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/Enemy/EnemyVfxInstaller.cs|Game/Scripts/Entities/Enemy/EnemyVfxInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/Weapons/MeleeWeaponInstaller.cs|Game/Scripts/Entities/Weapon/MeleeWeaponInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/Weapons/RangeWeaponInstaller.cs|Game/Scripts/Entities/Weapon/RangeWeaponInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/Weapons/WeaponVisualInstaller.cs|Game/Scripts/Entities/Weapon/WeaponVisualInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/Bullet/BulletInstaller.cs|Game/Scripts/Entities/Projectile/BulletInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/Bullet/HitInstaller.cs|Game/Scripts/Entities/Projectile/HitInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/Projectile/ProjectileCoreInstaller.cs|Game/Scripts/Entities/Projectile/ProjectileCoreInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/Projectile/ProjectileVisualInstaller.cs|Game/Scripts/Entities/Projectile/ProjectileVisualInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/Shell/ShellInstaller.cs|Game/Scripts/Entities/Projectile/ShellInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/PickUps/AmmoPickUpInstaller.cs|Game/Scripts/Entities/PickUp/AmmoPickUpInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/PickUps/BuffPickUpInstaller.cs|Game/Scripts/Entities/PickUp/BuffPickUpInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/PickUps/DashPickUpInstaller.cs|Game/Scripts/Entities/PickUp/DashPickUpInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/PickUps/HealthPickUpInstaller.cs|Game/Scripts/Entities/PickUp/HealthPickUpInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/PickUps/WeaponPickUpInstaller.cs|Game/Scripts/Entities/PickUp/WeaponPickUpInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/Items/ItemVisualInstaller.cs|Game/Scripts/Entities/PickUp/ItemVisualInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/InteractMarker/MarkerInstaller.cs|Game/Scripts/Entities/PickUp/MarkerInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/Audio/AudioSourceInstaller.cs|Game/Scripts/Entities/Common/AudioSourceInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/EffectArea/EffectAreaInstaller.cs|Game/Scripts/Entities/Common/EffectAreaInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/EffectArea/EffectAreaBehaviour.cs|Game/Scripts/Entities/Common/EffectAreaBehaviour.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Aspects/DamageAspect.cs|Game/Scripts/Shared/Aspects/DamageAspect.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Aspects/JumpAspect.cs|Game/Scripts/Shared/Aspects/JumpAspect.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Aspects/MoveSpeedAspect.cs|Game/Scripts/Shared/Aspects/MoveSpeedAspect.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Aspects/ScriptableEntityAspectComposite.cs|Game/Scripts/Shared/Aspects/ScriptableEntityAspectComposite.cs",
            "Game/Scripts/Gameplay/GameEntities/View/Animator/AnimatorTrigger.cs|Game/Scripts/Shared/Animations/AnimatorTrigger.cs",
            "Game/Scripts/Gameplay/GameEntities/View/Animator/SwitchAnimatorBehaviour.cs|Game/Scripts/Shared/Animations/SwitchAnimatorBehaviour.cs",
            "Game/Scripts/Gameplay/GameEntities/View/Animator/SwitchAnimatorUseCase.cs|Game/Scripts/Shared/Animations/SwitchAnimatorUseCase.cs",
            "Modules/AnimationEventReceiver.cs|Game/Scripts/Shared/Events/AnimationEventReceiver.cs",
            "Modules/CollisionEventReceiver.cs|Game/Scripts/Shared/Events/CollisionEventReceiver.cs",
            "Modules/TriggerEventReceiver.cs|Game/Scripts/Shared/Events/TriggerEventReceiver.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/Vector/VectorUseCase.cs|Game/Scripts/Shared/Math/VectorUseCase.cs",
            "Game/Scripts/Gameplay/GameEntities/Core/RayCast/RayCastUseCase.cs|Game/Scripts/Shared/Math/RayCastUseCase.cs",
            "Modules/TrailView.cs|Game/Scripts/Shared/View/TrailView.cs",
            "Modules/LightAnimation.cs|Game/Scripts/Shared/View/LightAnimation.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/Dummy/DummyInstaller.cs|Game/Scripts/Sandbox/DummyInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/Dummy/RestoreHealthOnDamageBehaviour.cs|Game/Scripts/Sandbox/RestoreHealthOnDamageBehaviour.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/SpawnButton/SpawnButtonInstaller.cs|Game/Scripts/Sandbox/SpawnButtonInstaller.cs",
            "Game/Scripts/Gameplay/GameEntities/Content/SpawnButton/SpawnButtonBehaviour.cs|Game/Scripts/Sandbox/SpawnButtonBehaviour.cs",
        };

        [MenuItem("Tools/Project/Migrate script layout")]
        public static void Run()
        {
            int deleted = DeleteRetired();
            (int moved, int skipped, List<string> failures) = MoveAll();

            RewriteAssemblyDefinitions();
            AssetDatabase.Refresh();

            int pruned = PruneEmptyFolders("Assets/Game/Scripts")
                         + PruneEmptyFolders("Assets/Modules")
                         + PruneEmptyFolders("Assets/Tools");
            AssetDatabase.Refresh();

            foreach (string failure in failures)
                Debug.LogError($"{Tag} {failure}");

            Debug.Log($"{Tag} deleted={deleted} moved={moved} skipped={skipped} " +
                      $"failed={failures.Count} prunedFolders={pruned}");
        }

        private static int DeleteRetired()
        {
            int deleted = 0;

            foreach (string path in Deletions)
            {
                if (!File.Exists(path) && !Directory.Exists(path))
                    continue;

                if (AssetDatabase.DeleteAsset(path))
                    deleted++;
                else
                    Debug.LogError($"{Tag} could not delete {path}");
            }

            return deleted;
        }

        private static (int moved, int skipped, List<string> failures) MoveAll()
        {
            int moved = 0;
            int skipped = 0;
            List<string> failures = new();

            foreach (string row in Moves)
            {
                string[] parts = row.Split('|');
                string source = "Assets/" + parts[0];
                string destination = "Assets/" + parts[1];

                // Already in place from an earlier run.
                if (!File.Exists(source))
                {
                    skipped++;
                    continue;
                }

                EnsureFolder(Path.GetDirectoryName(destination).Replace('\\', '/'));

                string error = AssetDatabase.MoveAsset(source, destination);

                if (string.IsNullOrEmpty(error))
                    moved++;
                else
                    failures.Add($"{source} -> {destination}: {error}");
            }

            return (moved, skipped, failures);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        /// <summary>
        /// Deletes folders left with nothing in them, deepest first, so a chain of emptied parents
        /// goes in one pass. Goes through DeleteAsset so each folder's own .meta goes with it.
        /// </summary>
        private static int PruneEmptyFolders(string root)
        {
            if (!AssetDatabase.IsValidFolder(root))
                return 0;

            int pruned = 0;

            foreach (string child in Directory.GetDirectories(root))
                pruned += PruneEmptyFolders(child.Replace('\\', '/'));

            bool empty = !Directory.EnumerateFileSystemEntries(root)
                .Any(entry => !entry.EndsWith(".meta"));

            if (!empty)
                return pruned;

            if (AssetDatabase.DeleteAsset(root))
                return pruned + 1;

            Debug.LogError($"{Tag} could not delete empty folder {root}");
            return pruned;
        }

        /// <summary>
        /// The Modules assembly is dissolved into Game, so its own references move onto Game and
        /// every reference to it goes away.
        /// </summary>
        /// <remarks>
        /// Written out whole rather than patched in place: they are three short files, and a
        /// hand-rolled JSON edit is how a field goes missing without anyone noticing.
        /// </remarks>
        private static void RewriteAssemblyDefinitions()
        {
            Write("Assets/Game/Scripts/Game.asmdef", @"{
    ""name"": ""Game"",
    ""rootNamespace"": ""Game"",
    ""references"": [
        ""Atomic.Elements"",
        ""Atomic.Entities"",
        ""HighlightPlus"",
        ""Unity.TextMeshPro"",
        ""Unity.Mathematics"",
        ""UniTask"",
        ""DOTweenModules"",
        ""UnityEngine.UI""
    ],
    ""includePlatforms"": [],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": false,
    ""precompiledReferences"": [],
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": [],
    ""noEngineReferences"": false
}");

            // PlaygroundBuilder uses TriggerEventReceiver, which lived in Modules until this
            // migration moved it into Game. The reference has to go, or Unity reports a reference
            // to a non-existent assembly.
            Write("Assets/Game/Scripts/Editor/Game.Editor.asmdef", @"{
    ""name"": ""Game.Editor"",
    ""rootNamespace"": ""Game.EditorTools"",
    ""references"": [
        ""Game"",
        ""Atomic.Elements"",
        ""Atomic.Entities""
    ],
    ""includePlatforms"": [
        ""Editor""
    ],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": false,
    ""precompiledReferences"": [],
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": [],
    ""noEngineReferences"": false
}");

            Write("Assets/Game/Tests/Game.Tests.asmdef", @"{
    ""name"": ""Game.Tests"",
    ""rootNamespace"": ""Game.Tests"",
    ""references"": [
        ""Game"",
        ""Atomic.Elements"",
        ""Atomic.Entities"",
        ""UnityEngine.TestRunner"",
        ""UnityEditor.TestRunner""
    ],
    ""includePlatforms"": [],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": true,
    ""precompiledReferences"": [
        ""nunit.framework.dll""
    ],
    ""autoReferenced"": false,
    ""defineConstraints"": [
        ""UNITY_INCLUDE_TESTS""
    ],
    ""versionDefines"": [],
    ""noEngineReferences"": false
}");

            if (File.Exists("Assets/Modules/Modules.asmdef"))
                AssetDatabase.DeleteAsset("Assets/Modules/Modules.asmdef");
        }

        private static void Write(string path, string contents)
        {
            File.WriteAllText(path, contents.Replace("\r\n", "\n"));
            AssetDatabase.ImportAsset(path);
        }
    }
}
