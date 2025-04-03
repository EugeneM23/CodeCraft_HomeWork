using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gameplay
{
    public class PlayerDeathObserver
    {
        public PlayerDeathObserver(Character player)
        {
            player.DeSpawn += OnDeath;
        }

        private void OnDeath(GameObject obj)
        {
            SceneManager.LoadScene("Game");
        }
    }
}