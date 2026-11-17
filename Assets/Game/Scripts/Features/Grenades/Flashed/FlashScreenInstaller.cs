using Atomic.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// Lets a flash grenade dazzle the player: FlashedEvent carries the flash's strength, and the
    /// white full-screen image fades the view back in.
    /// </summary>
    public sealed class FlashScreenInstaller : MonoEntityInstaller
    {
        [Tooltip("White, full-screen, on a Screen Space Overlay canvas. Starts transparent.")]
        [SerializeField] private Image _whiteout;

        public override void Install(IEntity entity)
        {
            entity.AddFlashedEvent(new Atomic.Elements.Event<float>());

            if (_whiteout != null)
                entity.AddBehaviour(new FlashScreenBehaviour(_whiteout));
        }
    }
}
