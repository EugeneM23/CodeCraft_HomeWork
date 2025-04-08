using System;
using UnityEngine;

namespace Gameplay
{
    public class PlayerShootController : MonoBehaviour
    {
        [SerializeField] private PlayerInput _input;
        [SerializeField] private PlayerCharacterProvider _characterProvider;

        private void OnEnable() => _input.OnShoot += _characterProvider.Character.Shoot;

        private void OnDisable() => _input.OnShoot -= _characterProvider.Character.Shoot;
    }
}