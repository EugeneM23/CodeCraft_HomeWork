using UnityEngine;

namespace Inventories
{
    public class AudioPlayer : MonoBehaviour
    {
        private static AudioPlayer _instance;

        [SerializeField] private AudioSource _addToInventory;
        [SerializeField] private AudioSource _addToSlot;
        [SerializeField] private AudioSource _removeFromInventory;
        [SerializeField] private AudioSource _removeFromSlot;
        [SerializeField] private AudioSource _stackIncreased;
        [SerializeField] private AudioSource _stackDecreased;

        private const string PrefabPath = "AudioPlayer";

        public static AudioPlayer Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<AudioPlayer>();

                    if (_instance == null)
                    {
                        GameObject prefab = Resources.Load<GameObject>(PrefabPath);
                        GameObject go = Instantiate(prefab);
                        _instance = go.GetComponent<AudioPlayer>();
                    }
                }

                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        public void PlayInventoryAdd() => _addToInventory.Play();

        public void PlayInventoryRemove() => _removeFromInventory.Play();

        public void PlaySlotAdd() => _addToSlot.Play();

        public void PlaySlotRemove() => _removeFromSlot.Play();

        public void PlayStackDecreased() => _stackDecreased.Play();

        public void PlayStackIncreased() => _stackIncreased.Play();
    }
}