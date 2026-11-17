using UnityEngine;

namespace Game
{
    /// <summary>
    /// Prototype line-of-sight fog. Casts rays around the viewer and writes, per ray, how far it got
    /// (R) and where the obstacle it hit ends on the far side (G) into a 1-pixel-high texture. A
    /// full-screen pass rebuilds each pixel's world position from the depth texture and darkens it
    /// when it lies further than the ray in its direction reached — unless it is raised above the
    /// floor and still within the obstacle, which is that obstacle's own top.
    /// </summary>
    public class FogOfWarView : MonoBehaviour
    {
        [SerializeField] private LayerMask _blockers = 1 | (1 << PhysicsLayers.Destructible);
        [SerializeField, Range(64, 4096)] private int _rays = 1024;
        [SerializeField] private float _radius = 25f;
        [SerializeField] private float _eyeHeight = 1.2f;
        [SerializeField] private float _wallBleed = 0.15f;
        [Tooltip("How deep into an obstacle its far side is looked for; thicker ones get lit this far.")]
        [SerializeField] private float _maxThickness = 3f;
        [Tooltip("Pixels this far above the viewer's feet count as raised (an obstacle's top), not floor.")]
        [SerializeField] private float _floorTolerance = 0.15f;
        [SerializeField] private float _softness = 0.4f;
        [SerializeField] private Color _darkness = new(0f, 0f, 0f, 0.85f);

        private Material _material;
        private Texture2D _rayTexture;
        private Vector2[] _pixels;

        private void Awake()
        {
            _material = new Material(Shader.Find("Game/FogOfWar/Overlay"));
            _rayTexture = new Texture2D(_rays, 1, TextureFormat.RGFloat, false, true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                name = "FogOfWarRays"
            };
            _pixels = new Vector2[_rays];
            _material.SetTexture("_FogRayTex", _rayTexture);

            var go = new GameObject("Overlay");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = BuildScreenQuad();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void OnDestroy()
        {
            Destroy(_rayTexture);
            Destroy(_material);
        }

        public void Rebuild(Vector3 viewer)
        {
            Vector3 eye = viewer + Vector3.up * _eyeHeight;

            for (int i = 0; i < _rays; i++)
            {
                float angle = i * Mathf.PI * 2f / _rays;
                Vector3 dir = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                if (!Physics.Raycast(eye, dir, out RaycastHit hit, _radius, _blockers, QueryTriggerInteraction.Ignore))
                {
                    _pixels[i] = new Vector2(_radius, _radius);
                    continue;
                }

                // Look back from beyond the obstacle at that collider alone to find its far side.
                float far = hit.distance + _maxThickness;
                var back = new Ray(eye + dir * far, -dir);
                if (hit.collider.Raycast(back, out RaycastHit exit, _maxThickness))
                    far -= exit.distance;

                _pixels[i] = new Vector2(hit.distance + _wallBleed, far + _wallBleed);
            }

            _rayTexture.SetPixelData(_pixels, 0);
            _rayTexture.Apply(false);

            _material.SetVector("_FogViewer", new Vector4(viewer.x, viewer.y + _floorTolerance, viewer.z, 0f));
            _material.SetFloat("_FogRays", _rays);
            _material.SetFloat("_Softness", _softness);
            _material.SetColor("_Color", _darkness);
        }

        private void OnEnable() => Camera.onPreCull += OnCameraPreCull;
        private void OnDisable() => Camera.onPreCull -= OnCameraPreCull;

        // Camera matrices are taken right before the camera renders, after it has followed this frame.
        private void OnCameraPreCull(Camera cam)
        {
            if (_material == null || cam.cameraType != CameraType.Game) return;
            cam.depthTextureMode |= DepthTextureMode.Depth;

            _material.SetMatrix("_FogInvViewProj", (cam.projectionMatrix * cam.worldToCameraMatrix).inverse);
            _material.SetVector("_FogCameraPos", cam.transform.position);
            _material.SetVector("_FogCameraForward", cam.transform.forward);
        }

        private static Mesh BuildScreenQuad()
        {
            var quad = new Mesh { name = "FogOfWarOverlay" };
            quad.vertices = new[] { new Vector3(-1, -1), new Vector3(1, -1), new Vector3(1, 1), new Vector3(-1, 1) };
            quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            quad.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            return quad;
        }
    }
}
