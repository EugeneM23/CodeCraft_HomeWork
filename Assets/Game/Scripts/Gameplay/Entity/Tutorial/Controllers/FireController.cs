using System;
using Atomic.Entities;
using SampleGame;
using UnityEngine;

namespace Game
{
    public class FireController : MonoBehaviour
    {
        [SerializeField] private SceneEntity _character;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.E))
                _character.GetWeapon().GetFireAction().Invoke();
        }
    }
}