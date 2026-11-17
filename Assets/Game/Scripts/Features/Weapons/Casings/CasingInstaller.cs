using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class CasingInstaller : MonoEntityInstaller
    {
        [SerializeField] private Rigidbody _rb;

        [SerializeField] private float _lifeTime = 3f;

        [Tooltip("How fast (rad/s) the casing tumbles as it is thrown out. 0 leaves it without spin.")]
        [Min(0f)]
        [SerializeField] private float _spin;

        private GameFactory _gameFactory;

        public override void Install(IEntity entity)
        {
            _gameFactory = GameContext.Instance.GetGameFactory();

            //Core
            // The pool id, by which GameFactory.Destroy returns the casing. Taken from the object, as
            // bullets do: it was the fixed Shell_01, and the tank's big casing would have been
            // returned into the rifle casings' pool and handed out to the AR next.
            entity.AddEntityID(gameObject.name.Replace("(Clone)", ""));
            entity.AddRiggedBody(_rb);
            entity.AddGameObject(transform.gameObject);
            entity.AddTransform(transform);
            entity.AddDestroyAction(new InlineAction(() => SpawnUseCase.UnSpawnEntity(_gameFactory, entity)));

            //Spin
            if (_spin > 0f)
            {
                entity.AddShellSpin(_spin);
                // Unity caps angular speed at 7 rad/s by default, which would quietly clip the spin.
                _rb.maxAngularVelocity = Mathf.Max(_rb.maxAngularVelocity, _spin * 1.5f);
            }

            //LifeTime
            entity.AddLifeTime(new CooldownTimer(_lifeTime));
            entity.AddBehaviour(new LifeTimeBehaviour());
        }
    }
}