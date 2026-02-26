using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class UITurntable : MonoBehaviour, IDragHandler
{
    [Header("Rotation")]
    [SerializeField] private RectTransform target;
    [SerializeField] private float speed = 0.5f;

    [Header("Render To Texture")]
    [SerializeField] private Camera renderCamera;
    [SerializeField] private int textureWidth = 1000;
    [SerializeField] private int textureHeight = 1200;

    private RenderTexture runtimeRT;
    private RawImage rawImage;

    void Awake()
    {
        rawImage = GetComponent<RawImage>();
        EnsureRenderTexture();
    }

    void EnsureRenderTexture()
    {
        if (runtimeRT != null) return;

        runtimeRT = new RenderTexture(textureWidth, textureHeight, 16)
        {
            useMipMap = false,
            autoGenerateMips = false,
            filterMode = FilterMode.Bilinear
        };

        renderCamera.targetTexture = runtimeRT;
        rawImage.texture = runtimeRT;
    }

    public void OnDrag(PointerEventData e)
    {
        if (target == null) return;

        EnsureRenderTexture();
        target.Rotate(0f, e.delta.x * speed, 0f);
    }

    void OnDestroy()
    {
        if (runtimeRT != null)
        {
            renderCamera.targetTexture = null;
            runtimeRT.Release();
        }
    }
}