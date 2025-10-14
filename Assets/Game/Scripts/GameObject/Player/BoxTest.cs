using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Gameplay
{
    public class BoxTest : MonoBehaviour
    {
        [ShowInInspector] public InputReader input;
        [ShowInInspector] public Rigidbody2D rigidbody2D;

        [FormerlySerializedAs("_massage")] public string massage;

        [Inject]
        public void Construct(
            InputReader input,
            Rigidbody2D rigidbody2D,
            string massage
        )
        {
            this.input = input;
            this.rigidbody2D = rigidbody2D;
            this.massage = massage;
        }
    }
}