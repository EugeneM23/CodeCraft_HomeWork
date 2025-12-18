using UnityEngine;
using UnityEngine.UI;

public class PrefabImageSetup : MonoBehaviour
{
    public RawImage targetImage;
    
    void Start()
    {
        Debug.Log("=== ДИАГНОСТИКА ПРЕФАБА ===");
        Debug.Log($"targetImage назначен? {targetImage != null}");
        Debug.Log($"targetImage.texture ДО: {targetImage.texture}");
        
        if (CameraRenderer.Instance != null)
        {
            RenderTexture rt = CameraRenderer.Instance.GetRenderTexture();
            Debug.Log($"RenderTexture от камеры: {rt}");
            Debug.Log($"RenderTexture создана? {rt != null && rt.IsCreated()}");
            
            targetImage.texture = rt;
            Debug.Log($"targetImage.texture ПОСЛЕ: {targetImage.texture}");
        }
        else
        {
            Debug.LogError("CameraRenderer.Instance NULL!");
        }
    }
}