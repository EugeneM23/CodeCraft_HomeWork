using UnityEngine;

public class CameraRenderer : MonoBehaviour
{
    public static CameraRenderer Instance;
    
    public RenderTexture renderTexture;
    public Camera renderCamera;
    
    void Awake()
    {
        Debug.Log("=== КАМЕРА AWAKE ===");
        
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        if (renderCamera == null)
        {
            renderCamera = GetComponent<Camera>();
        }
        
        // Создайте НОВУЮ текстуру
        renderTexture = new RenderTexture(512, 512, 24);
        renderTexture.Create();
        
        Debug.Log($"RenderTexture создана: {renderTexture.IsCreated()}");
        
        renderCamera.targetTexture = renderTexture;
        renderCamera.enabled = true;
        
        // Проверьте что камера видит
        Debug.Log($"Camera enabled: {renderCamera.enabled}");
        Debug.Log($"Camera cullingMask: {renderCamera.cullingMask}");
        Debug.Log($"Camera clearFlags: {renderCamera.clearFlags}");
    }
    
    void Start()
    {
        // Принудительный рендер
        renderCamera.Render();
        Debug.Log("Камера отрендерила");
    }
    
    public RenderTexture GetRenderTexture()
    {
        return renderTexture;
    }
}