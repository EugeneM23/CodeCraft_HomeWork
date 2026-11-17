using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Gives a character a volumetric blood effect on death; see <see cref="DeathBloodBehaviour"/>.
    /// </summary>
    public sealed class DeathBloodInstaller : MonoEntityInstaller
    {
        [SerializeField] private DeathBloodConfig _config;

        [Tooltip("Where the blood starts: at the character's feet, following it.")]
        [SerializeField] private Transform _anchor;

        [Tooltip("-1 picks a random effect each death; 0 and up always plays that one of the config's list.")]
        [SerializeField] private int _variant = -1;

        public override void Install(IEntity entity)
        {
            if (_config == null || _anchor == null)
                return;

            entity.AddBehaviour(new DeathBloodBehaviour(_config, _anchor, _variant));
        }
    }
}
