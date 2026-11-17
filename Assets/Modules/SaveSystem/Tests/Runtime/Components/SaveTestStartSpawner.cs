using UnityEngine;

namespace Saving.Tests
{
    /// <summary>
    /// A script of a game that makes an object in its Start every time the scene starts — a weapon in the hand, a
    /// pool: after a load the saved copy must take that object over, not stand next to it.
    /// </summary>
    public sealed class SaveTestStartSpawner : MonoBehaviour
    {
        public GameObject Prefab;

        public GameObject Made { get; private set; }

        private void Start()
        {
            if (Prefab != null)
                Made = Instantiate(Prefab, transform);
        }
    }
}
