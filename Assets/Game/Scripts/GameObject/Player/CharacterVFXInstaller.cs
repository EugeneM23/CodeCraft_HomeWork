using Gameplay;
using UnityEngine;

namespace Game.Scripts.GameObject.Player
{
    public class CharacterVFXInstaller : Installer
    {
        [SerializeField] private Transform _jumpPrefab;
        [SerializeField] private Transform _rollPrefab;
        [SerializeField] private Transform _smashPrefab;

        public override void Install(DiContainer container)
        {
            container.BindInterfacesAndSelf(new JumpVFXController(_jumpPrefab));
            container.BindInterfacesAndSelf(new RollVFXController(_rollPrefab));
            container.BindInterfacesAndSelf(new SmashVFXController(_smashPrefab));
        }
    }
}