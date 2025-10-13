using System;
using UnityEngine;

namespace Gameplay
{
    public class BoxTest : MonoBehaviour
    {
        public GUI _massage;

        [Inject]
        public void Construct(GUI massage)
        {
            _massage = massage;
        }
    }
}