using Modules.Planets;
using Sirenix.OdinInspector;
using UnityEngine;
using Zenject;

namespace Game.Gameplay
{
    public class PlanetDebug : MonoBehaviour
    {
        [Inject] [ShowInInspector] private Planet _planets;
    }
}