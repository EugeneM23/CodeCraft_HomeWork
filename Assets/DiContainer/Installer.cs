using UnityEngine;

namespace Gameplay
{
    public abstract class Installer : MonoBehaviour
    {
        public abstract void Install(diContainer container);
    }
}