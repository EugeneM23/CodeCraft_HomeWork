using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class GameContextInstaller : MonoEntityInstaller<GameContext>
    {
        [SerializeField] private PlayerContext _playerContext;
        [SerializeField] private Transform _poolsParent;
        [SerializeField] private MonoEntity _hitEffectPrefab;
        [SerializeField] private MonoEntityWorld _entityWorld;

        [Header("Pool prewarm")]
        [Tooltip("Instances made per frame, from just after the start, of what the scene's weapons and vehicles spawn: bullets, casings, impacts, muzzle smoke, blasts. 0 turns the prewarm off.")]
        [SerializeField, Min(0)] private int _prewarmPerFrame = 4;
        [Tooltip("No more instances are made in a frame that has spent this long (ms) on them. One always is.")]
        [SerializeField, Min(0f)] private float _prewarmFrameBudgetMs = 2f;
        [Tooltip("Most instances of one prefab made ahead. Each gets about its rate of fire × its lifetime, plus one.")]
        [SerializeField, Min(1)] private int _prewarmMaxPerPrefab = 24;

        public override void Install(GameContext context)
        {
            context.AddEntityWorld(_entityWorld);

            // A null here used to throw and leave out the factory, the prewarm and the enemy filter
            // too, so every blast broke halfway. It is usually the PlayerContext's script showing as
            // "missing" in an editor whose Library was copied from another branch.
            if (_playerContext != null)
                context.AddPlayerContext(_playerContext);
            else
                Debug.LogError("[GameContextInstaller] No PlayerContext: the camera, the HUD and the controls get no player. " +
                               "If the scene has one, its script is missing: reimport the scripts.", this);
            context.AddGameFactory(new GameFactory(_poolsParent));

            // Without it the first shot, hit and blast of each kind instantiates its object mid-fight.
            if (_prewarmPerFrame > 0)
                context.AddBehaviour(new CombatPoolPrewarm(_prewarmPerFrame, _prewarmFrameBudgetMs, _prewarmMaxPerPrefab));

            context.AddEnemyFilter(new EntityFilter(_entityWorld, static entity => entity.HasEnemyTag()));
        }
    }
}
