using UnityEngine;

namespace Gameplay
{
    public class PlayerCharacterProvider : MonoBehaviour
    {
        public Character Character { get; private set; }

        public void Setup(Character character) => Character = character;
    }
}