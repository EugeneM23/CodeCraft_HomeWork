using UnityEngine;
using UnityEngine.Serialization;

namespace Inventories
{
    public class AudioPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource _addToInventory;
        [SerializeField] private AudioSource _addToSlot;
        [SerializeField] private AudioSource _removeFromInventory;
        [SerializeField] private AudioSource _removeFromSlot;

        public void PlayInventoryAdd()
        {
            _addToInventory.Play();
        }

        public void PlayInventoryRemove()
        {
            Debug.Log("Removing");
            _removeFromInventory.Play();
        }

        public void PlaySlotAdd()
        {
            _addToSlot.Play();
        }

        public void PlaySlotRemove()
        {
            _removeFromSlot.Play();
        }
    }
}