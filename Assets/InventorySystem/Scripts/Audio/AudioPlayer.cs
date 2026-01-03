using UnityEngine;

namespace Inventories
{
    public class AudioPlayer : MonoBehaviour
    {
        private static AudioPlayer _instance;


        public static AudioPlayer Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<AudioPlayer>();

                    if (_instance == null)
                    {
                        GameObject go = new GameObject("AudioPlayer");
                        go.AddComponent<AudioPlayer>();
                        go.AddComponent<AudioSource>();

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

        public void Play(AudioClip dropToInventory)
        {
            _instance.GetComponent<AudioSource>().clip = dropToInventory;
            _instance.GetComponent<AudioSource>().pitch = Random.Range(0.7f, 1.1f);
            _instance.GetComponent<AudioSource>().Play();
        }
    }
}