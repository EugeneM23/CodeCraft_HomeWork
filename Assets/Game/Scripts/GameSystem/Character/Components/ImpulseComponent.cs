using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class ImpulseComponent : IImpulse
    {
        [Inject] private readonly CharacterController2D _controller;

        public void AddImpulse(Vector3 power) => _controller.AddImpulse(power);
    }
}