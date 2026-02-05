using UnityEngine;

 [RequireComponent(typeof(Light))]
 public class Light_flicker : MonoBehaviour
 {
 
     [Tooltip("Max light intensity ")]
     [SerializeField, Min(0)] float maxIntensity = 17f;
 
     [Tooltip("Min light intensity")]
     [SerializeField, Min(0)] float minIntensity = 7f;
 
     [Tooltip("Max frequency flicker (seconds)")]
     [SerializeField, Min(0)] float maxFlickerFrequency = 0.5f;
 
     [Tooltip("Min frequency flicker (seconds)")]
     [SerializeField, Min(0)] float minFlickerFrequency = 0.5f;
 
     [Tooltip("Strength of flicker")]
     [SerializeField, Min(0)] float strength = 3f;
 
     float baseIntensity;
     float nextIntensity;
 
     float flickerFrequency;
     float timeOfLastFlicker;

 
     private Light LightCur => GetComponent<Light>();
 
 
     private void OnValidate()
     {
         if (maxIntensity < minIntensity) minIntensity = maxIntensity;
         if (maxFlickerFrequency < minFlickerFrequency) minFlickerFrequency = maxFlickerFrequency;
     }
 
     private void Awake()
     {
         baseIntensity = LightCur.intensity;
 
         timeOfLastFlicker = Time.time;
     }
 
     private void Update()
     {
         if (timeOfLastFlicker + flickerFrequency < Time.time)
         {
             timeOfLastFlicker = Time.time;
             nextIntensity = Random.Range(minIntensity, maxIntensity);
             flickerFrequency = Random.Range(minFlickerFrequency, maxFlickerFrequency);
         }
 
         Flicker();
     }
 
     private void Flicker()
     {
         LightCur.intensity = Mathf.Lerp(
             LightCur.intensity,
             nextIntensity, 
             strength * Time.deltaTime
         );
     }
 
     public void Reset()
     {
         LightCur.intensity = baseIntensity;
     }
 }
