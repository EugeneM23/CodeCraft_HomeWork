using System;
using AudioEngine;
using UnityEngine;

namespace Game.Scripts.GameObject.Triggers
{
    public class ItemCollisionSFVTrigger : MonoBehaviour
    {
        private AudioSystem _audioSystem;
        private float MAX_FRIQUIENCY = 0.2f;

        private void OnEnable() => _audioSystem = AudioSystem.Instance;

        private void OnCollisionEnter2D(Collision2D other)
        {
            _audioSystem.PlayEvent(MasterBankAPI.CollisionEvent, transform.position, Quaternion.identity,
                MAX_FRIQUIENCY);
        }
    }
}