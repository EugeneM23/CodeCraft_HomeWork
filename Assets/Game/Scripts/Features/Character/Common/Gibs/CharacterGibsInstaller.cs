using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Makes the character come apart into ragdolled pieces on the killing hit.
    /// Put on Regax.prefab by RegaxGibsBuilder.
    /// </summary>
    public sealed class CharacterGibsInstaller : MonoEntityInstaller
    {
        [SerializeField] private Gibs _gibs;

        [Tooltip("The animated hierarchy whose bone names the gibs' bones share.")]
        [SerializeField] private Transform _skeleton;

        [Tooltip("What disappears when the gibs take the body's place.")]
        [SerializeField] private Renderer[] _body;

        public override void Install(IEntity entity)
        {
            if (_gibs == null || _skeleton == null)
                return;

            entity.AddBehaviour(new GibsOnDeathBehaviour(_gibs, _skeleton, _body));
        }
    }
}
