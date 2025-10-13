using Game.Scripts.Player;
using UnityEngine;

namespace Gameplay
{
    public class PlaerSpawner : MonoBehaviour
    {
        [SerializeField] private PlayerInstaller _player;

        /*private void Awake()
        {
            SceneContext.Instance.Container.InstantiatePrefab(_player, transform.position, transform.rotation);
        }*/
    }
}