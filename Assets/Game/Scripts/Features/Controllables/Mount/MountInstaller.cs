using Atomic.Elements;
using Atomic.Entities;
using HighlightPlus;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A Regax the player climbs on and rides, in place of the enemy's EnemyCharacterCoreInstaller: the
    /// same body, animator, claws, blood and sounds, but no patrol, chase or attack AI.
    ///
    /// To the player context it is a vehicle (VehicleTag, Driver, ExitPoints, InteractAction,
    /// VehicleExitAction), so E gets on and off through ControllableUseCase exactly like the tank. Unlike
    /// the tank it has no DriveInput: it carries the keys a walking character has — MoveDirection,
    /// RotateDirection, MoveCondition — so PlayerMoveController walks it with WASD and
    /// PlayerAimController turns it to the cursor, as on foot. The left mouse button
    /// (ControllableFireController) swings the claws; the hit lands on the swing's damage_cast_event.
    ///
    /// Moved through its Rigidbody, like the player, not by transform as enemies are: a ridden
    /// mount must stop at walls. Every collider the game looks for sits on this object.
    /// </summary>
    public sealed class MountInstaller : MonoEntityInstaller
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 7f;
        [SerializeField] private float _rotationSpeed = 10f;

        [Header("Combat")]
        [Tooltip("The claws: a melee weapon whose damage layer is the enemies'.")]
        [SerializeField] private MonoEntity _weapon;
        [SerializeField] private Transform _weaponRoot;

        [Header("Body")]
        [SerializeField] private Transform _characterRoot;
        [SerializeField] private Rigidbody _rigidbody;
        [SerializeField] private Transform _cameraPoint;
        [Tooltip("Camera offset multiplier while riding: Regax is more than twice a person's height.")]
        [SerializeField] private float _cameraDistance = 1.25f;
        [SerializeField] private Transform[] _exitPoints;
        [SerializeField] private int _health = 300;

        [Header("Interaction")]
        [SerializeField] private HighlightEffect _highlight;
        [SerializeField] private Vector3 _markerOffset = new(0f, 4.8f, 0f);

        public override void Install(IEntity entity)
        {
            GameContext gameContext = GameContext.Instance;

            // Core
            entity.AddVehicleTag();
            entity.AddDamageableTag();
            entity.AddInteractableTag();
            entity.AddGameObject(gameObject);
            entity.AddTransform(_characterRoot);
            entity.AddRiggedBody(_rigidbody);

            // Health. The rider is thrown off before DeathBehaviour disposes the body.
            HealthUseCase.AddHealthWithEvents(entity, _health);
            entity.AddBehaviour(new MountDeathBehaviour(gameContext));
            entity.AddBehaviour(new DeathBehaviour(gameContext));

            // Riding
            entity.AddDriver(new ReactiveVariable<IEntity>());
            entity.AddExitPoints(_exitPoints);

            // Movement: the player's keys, so the player's controllers drive it as they are.
            WalkingUseCase.AddWalking(entity, _moveSpeed, _rotationSpeed, _characterRoot.forward);
            entity.AddBehaviour<RigidbodyMoveBehaviour>();
            entity.AddBehaviour<RigidbodyRotateBehaviour>();
            entity.AddBehaviour<CharacterVelocityBehaviour>();
            entity.AddBehaviour(new PassThroughLoosePiecesBehaviour());
            entity.AddBehaviour(new DriverAudioListenerBehaviour());

            // Attack: only with somebody on its back.
            CharacterAttackUseCase.AddHeldWeapon(entity, _weapon, _weaponRoot,
                () => entity.GetDriver().Value != null);

            // Camera
            entity.AddCameraPoint(_cameraPoint);
            entity.AddCameraDistance(_cameraDistance);

            // Interaction: E next to it climbs on, E on it climbs off.
            if (_highlight != null)
            {
                ControllableHighlight.UseSingleTargetFX(_highlight);
                entity.AddHighlight(_highlight);
            }
            entity.AddPositionOffset(_markerOffset);

            entity.AddInteractAction(new InlineAction<IEntity>(driver =>
            {
                if (ControllableUseCase.CanEnter(entity, driver))
                    ControllableUseCase.Enter(entity, driver, gameContext.GetPlayerContext().GetCharacter(),
                        gameContext.GetEntityWorld());
            }));

            entity.AddVehicleExitAction(new InlineAction(() =>
                ControllableUseCase.TryExit(entity, gameContext.GetPlayerContext().GetCharacter(),
                    gameContext.GetEntityWorld())));

            // CharacterBuffInstaller on the same object may have added it already.
            if (!entity.HasBuffsEffects())
                entity.AddBuffsEffects(new ReactiveList<BuffBase>());
        }
    }
}
