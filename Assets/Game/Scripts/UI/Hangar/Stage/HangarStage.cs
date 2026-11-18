using System.Collections.Generic;
using Game.Voxels;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// The 3D hangar behind the menu UI: a camera that eases between two framings (the main menu's backdrop
    /// and the hangar screen's close view) and the tank on a slowly turning turntable, which the mouse can
    /// drag round on the hangar screen. Runs on unscaled time. The tank is a visual without gameplay; its
    /// voxel skin is built once on start (<see cref="HangarSkinUseCase"/>).
    /// </summary>
    public sealed class HangarStage : MonoBehaviour
    {
        [SerializeField] private Transform _camera;
        [Tooltip("Where the camera stands behind the main menu.")]
        [SerializeField] private Transform _menuPose;
        [Tooltip("Where the camera stands on the hangar screen: the tank framed between the side panels.")]
        [SerializeField] private Transform _hangarPose;
        [Tooltip("Seconds the camera takes from one framing to the other.")]
        [SerializeField] private float _moveTime = 0.9f;

        [SerializeField] private Transform _turntable;
        [Tooltip("The tank visual's parent on the turntable; its origin is the middle of the turntable's top.")]
        [SerializeField] private Transform _tankMount;
        [SerializeField] private GameObject _tankVisual;
        [Tooltip("Material of the voxel skin (Game/Voxel, colours in the vertices).")]
        [SerializeField] private Material _skinMaterial;
        [Tooltip("Voxel edge of the tank's VoxelizedModel, m. The builder copies it from the model.")]
        [SerializeField] private float _skinVoxelSize = 0.03f;

        [Tooltip("Turntable speed when nobody drags it, degrees per second.")]
        [SerializeField] private float _spinSpeed = 7f;
        [Tooltip("Degrees per pixel of mouse drag.")]
        [SerializeField] private float _dragDegreesPerPixel = 0.3f;
        [Tooltip("Seconds after a drag before the turntable turns on its own again.")]
        [SerializeField] private float _spinResumeDelay = 2f;

        private readonly List<Mesh> _skinMeshes = new();
        private Vector3 _fromPosition;
        private Quaternion _fromRotation;
        private Transform _target;
        private float _moveElapsed;
        private bool _moving;
        private bool _hangar;
        private bool _dragging;
        private float _sinceDrag = float.MaxValue;

        public bool InHangar => _hangar;

        private void Start()
        {
            if (!_moving && _target == null)
                Snap(_hangar ? _hangarPose : _menuPose);

            if (_tankVisual != null)
                BuildSkin(_tankVisual, _skinVoxelSize);
        }

        private void OnDestroy() => DestroySkin();

        /// <summary>The camera eases to the main menu's framing; the turntable cannot be dragged.</summary>
        public void ShowMenu()
        {
            _hangar = false;
            _dragging = false;
            MoveTo(_menuPose);
        }

        /// <summary>The camera eases to the hangar's framing; the mouse can drag the turntable.</summary>
        public void ShowHangar()
        {
            _hangar = true;
            MoveTo(_hangarPose);
        }

        /// <summary>
        /// Puts another tank on the turntable in place of the current one (which is destroyed). The visual must
        /// carry no gameplay: renderers and, for the voxel look, a disabled <see cref="VoxelizedModel"/>.
        /// </summary>
        public void SetTankVisual(GameObject visual) => SetTankVisual(visual, _skinVoxelSize);

        /// <inheritdoc cref="SetTankVisual(GameObject)"/>
        public void SetTankVisual(GameObject visual, float voxelSize)
        {
            if (visual == _tankVisual)
                return;

            DestroySkin();
            if (_tankVisual != null)
            {
                if (Application.isPlaying) Destroy(_tankVisual);
                else DestroyImmediate(_tankVisual);
            }

            _tankVisual = visual;
            _skinVoxelSize = voxelSize;
            if (visual == null)
                return;

            Seat(visual.transform, _tankMount);
            if (Application.isPlaying)
                BuildSkin(visual, voxelSize);
        }

        /// <summary>
        /// Parents the visual to the mount with its renderers' bounds centred on the mount's axis and their
        /// bottom on the mount's origin.
        /// </summary>
        public static void Seat(Transform visual, Transform mount)
        {
            visual.SetParent(null, false);
            visual.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            bool any = false;
            Bounds bounds = default;
            foreach (MeshRenderer renderer in visual.GetComponentsInChildren<MeshRenderer>())
            {
                if (!any) bounds = renderer.bounds;
                else bounds.Encapsulate(renderer.bounds);
                any = true;
            }

            visual.SetParent(mount, false);
            visual.localRotation = Quaternion.identity;
            visual.localPosition = any ? new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z) : Vector3.zero;
        }

        private void Update() // perf-ok: menu stage, ticks while the game is paused or not running
        {
            float dt = Time.unscaledDeltaTime;
            TickCamera(dt);
            TickTurntable(dt);
        }

        private void TickCamera(float dt)
        {
            if (!_moving || _camera == null || _target == null)
                return;

            _moveElapsed += dt;
            float t = _moveTime > 0f ? Mathf.Clamp01(_moveElapsed / _moveTime) : 1f;
            float eased = t * t * (3f - 2f * t);
            _camera.SetPositionAndRotation(
                Vector3.LerpUnclamped(_fromPosition, _target.position, eased),
                Quaternion.SlerpUnclamped(_fromRotation, _target.rotation, eased));
            if (t >= 1f)
                _moving = false;
        }

        private void TickTurntable(float dt)
        {
            if (_turntable == null)
                return;

            Mouse mouse = Mouse.current;
            if (_hangar && mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame && !PointerOverUi())
                    _dragging = true;
                else if (!mouse.leftButton.isPressed)
                    _dragging = false;
            }
            else
            {
                _dragging = false;
            }

            float degrees;
            if (_dragging)
            {
                degrees = -mouse.delta.ReadValue().x * _dragDegreesPerPixel;
                _sinceDrag = 0f;
            }
            else
            {
                _sinceDrag += dt;
                // Back to full speed over a second once the delay is over.
                float resume = Mathf.Clamp01(_sinceDrag - _spinResumeDelay);
                degrees = _spinSpeed * resume * dt;
            }

            if (degrees != 0f)
                _turntable.Rotate(0f, degrees, 0f, Space.Self);
        }

        private static bool PointerOverUi()
        {
            EventSystem system = EventSystem.current;
            return system != null && system.IsPointerOverGameObject();
        }

        private void MoveTo(Transform pose)
        {
            if (pose == null || _camera == null)
                return;

            _target = pose;
            _fromPosition = _camera.position;
            _fromRotation = _camera.rotation;
            _moveElapsed = 0f;
            _moving = true;
        }

        private void Snap(Transform pose)
        {
            if (pose == null || _camera == null)
                return;

            _camera.SetPositionAndRotation(pose.position, pose.rotation);
            _target = pose;
            _moving = false;
        }

        private void BuildSkin(GameObject visual, float voxelSize)
        {
            if (!visual.TryGetComponent(out VoxelizedModel model))
                return;

            Material material = _skinMaterial != null ? _skinMaterial : Resources.Load<Material>("Voxels/VoxelChunk");
            if (material == null)
            {
                Debug.LogWarning($"{nameof(HangarStage)}: no voxel skin material; the tank stays as meshes.", this);
                return;
            }

            // Its own Start would build VoxelBodies, which need a VoxelWorld: the stage keeps it off.
            model.enabled = false;
            HangarSkinUseCase.Build(model, material, voxelSize, _skinMeshes);
        }

        private void DestroySkin()
        {
            foreach (Mesh mesh in _skinMeshes)
                if (mesh != null)
                    Destroy(mesh);
            _skinMeshes.Clear();
        }
    }
}
