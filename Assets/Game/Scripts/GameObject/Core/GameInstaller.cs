using UnityEngine;

namespace Gameplay
{
    [DefaultExecutionOrder(-1000)]
    public class GameInstaller : MonoBehaviour
    {
        private void Awake()
        {
            ServiceLocator.Add(GameID.InpuReader, new InputReader());
        }
    }
}