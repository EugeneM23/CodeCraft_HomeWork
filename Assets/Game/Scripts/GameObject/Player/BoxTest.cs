using System;
using UnityEngine;

namespace Gameplay
{
    public class BoxTest : MonoBehaviour
    {
        public string _massage;

        [Inject]
        public void Construct(string massage)
        {
            _massage = massage;
        }
    }
}