using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay
{
    public class BoxTest : MonoBehaviour
    {
        [ShowInInspector] public InputReader _input;
        public string _massage;

        [Inject]
        public void Construct(InputReader input, string massage)
        {
            _massage = massage;
            _input = input;
        }
    }
}