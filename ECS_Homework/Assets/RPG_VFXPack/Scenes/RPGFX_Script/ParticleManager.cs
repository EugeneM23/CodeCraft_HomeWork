using UnityEngine;

namespace ParticleManager
{
    public class ParticleManager : MonoBehaviour
    {
        public Camera camera1;
        public Camera camera2;
        private Camera currentCamera;

        public ParticleSystem[] particleSystems = new ParticleSystem[16];
        public Vector3[] particlePositions = new Vector3[23];

        private int currentID = 0;

        void Start()
        {
            if (camera1 != null)
            {
                camera1.gameObject.SetActive(true);
                currentCamera = camera1;
            }
            if (camera2 != null)
            {
                camera2.gameObject.SetActive(false);
            }

            UpdateCamera1Position();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                if (camera1 != null)
                {
                    camera1.gameObject.SetActive(true);
                    currentCamera = camera1;
                    UpdateCamera1Position();
                }
                if (camera2 != null)
                {
                    camera2.gameObject.SetActive(false);
                }
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                if (camera2 != null)
                {
                    camera2.gameObject.SetActive(true);
                    currentCamera = camera2;
                }
                if (camera1 != null)
                {
                    camera1.gameObject.SetActive(false);
                }
            }

            if (Input.GetKeyDown(KeyCode.Q))
            {
                currentID--;
                if (currentID < 0)
                {
                    currentID = 22;
                }
                if (currentCamera == camera1)
                {
                    UpdateCamera1Position();
                }
            }
            else if (Input.GetKeyDown(KeyCode.E))
            {
                currentID++;
                if (currentID > 22)
                {
                    currentID = 0;
                }
                if (currentCamera == camera1)
                {
                    UpdateCamera1Position();
                }
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                if (currentCamera == camera2)
                {
                    for (int i = 0; i < particleSystems.Length; i++)
                    {
                        if (particleSystems[i] != null)
                        {
                            Vector3 spawnPosition = (i >= 0 && i < particlePositions.Length) ? particlePositions[i] : Vector3.zero;
                            GameObject particleObject = Instantiate(particleSystems[i].gameObject, spawnPosition, Quaternion.identity);
                            particleObject.SetActive(true);
                            ParticleSystem instantiatedParticleSystem = particleObject.GetComponent<ParticleSystem>();
                            if (instantiatedParticleSystem != null)
                            {
                                instantiatedParticleSystem.Play();
                            }
                        }
                    }
                }
                else
                {
                    if (currentID >= 0 && currentID < 16 && particleSystems[currentID] != null)
                    {
                        Vector3 spawnPosition = (currentID >= 0 && currentID < 23) ? particlePositions[currentID] : Vector3.zero;
                        GameObject particleObject = Instantiate(particleSystems[currentID].gameObject, spawnPosition, Quaternion.identity);
                        particleObject.SetActive(true);
                        ParticleSystem instantiatedParticleSystem = particleObject.GetComponent<ParticleSystem>();
                        if (instantiatedParticleSystem != null)
                        {
                            instantiatedParticleSystem.Play();
                        }
                    }
                }
            }
        }

        void UpdateCamera1Position()
        {
            if (camera1 != null && currentID >= 0 && currentID < 23)
            {
                camera1.transform.position = new Vector3(
                    particlePositions[currentID].x,
                    4.5f,
                    particlePositions[currentID].z - 6.5f
                );
            }
        }
    }
}