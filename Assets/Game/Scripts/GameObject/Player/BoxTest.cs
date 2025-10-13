using System;
using UnityEngine;

namespace Gameplay
{
    public class BoxTest : MonoBehaviour
    {
        private string _massage;

        [Inject]
        public void Construct(string massage)
        {
            _massage = massage;
        }

        private void Start()
        {
            _massage.Log();
        }
    }
}