using System;
using UnityEngine;

namespace Gameplay
{
    public class PlayerMoveController : MonoBehaviour
    {
        [SerializeField] private PlayerInput _input;
        [SerializeField] private PlayerCharacterProvider _characterProvider;

        private void OnEnable() => _input.OnMove += _characterProvider.Character.Move;

        private void OnDisable() => _input.OnMove -= _characterProvider.Character.Move;
    }
}