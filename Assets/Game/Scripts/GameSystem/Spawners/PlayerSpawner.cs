using Gameplay;
using UnityEngine;

namespace Game.Scripts.GameObject.Player
{
    public class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] private Entity character;
        [Inject] private readonly PlayerCharacterProvider _playerCharacterProvider;

        private void OnEnable() => Spawn();

        public void Spawn()
        {
            Entity entity =
                SceneContext.Instance.Container.InstantiatePrefab(character, transform.position, Quaternion.identity);
            var player = entity.GetEntityComponent<Character>();
            _playerCharacterProvider.SetCharacter(player);
        }
    }
}