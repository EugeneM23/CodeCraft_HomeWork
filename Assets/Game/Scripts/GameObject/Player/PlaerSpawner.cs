using Game.Scripts.Player;
using UnityEngine;

namespace Gameplay
{
    public class PlaerSpawner : MonoBehaviour
    {
        [SerializeField] private PlayerInstaller _player;

        private void Start()
        {
            SceneContext.Instance.Container.InstantiatePrefab(_player, transform.position, transform.rotation);
        }
    }
}