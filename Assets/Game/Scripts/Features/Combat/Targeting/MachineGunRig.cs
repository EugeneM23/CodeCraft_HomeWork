using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Experiment: the tank's machine gun as a big gun of plain boxes on the turret roof that turns to what it shoots.
    /// Built from code at attach (<see cref="TargetingSetup.AttachTank"/>): a base turning about the turret's up (yaw),
    /// on it a body and a long barrel tilting up and down (pitch). The weapon's own fire point is moved to the muzzle
    /// and parented to the pitch pivot, so the bullets leave the new barrel; <see cref="Dispose"/> puts it back.
    /// The old small gun (the turret's MachineGun* meshes, kept out of the voxel skin by Tank_01's VoxelizedModel) is
    /// hidden while the rig is on. Boxes only, axis-aligned, no colliders: the hull's collider list is untouched.
    /// </summary>
    public sealed class MachineGunRig
    {
        private const string OLD_GUN_PREFIX = "MachineGun";
        private const float BASE_HEIGHT = 0.06f;
        private const float POST_HEIGHT = 0.16f;

        private readonly Transform _root;
        private readonly Transform _pitch;
        private readonly Transform _ejector;
        private readonly Transform _space;
        private readonly float _pivotHeight;

        private readonly Transform _firePoint;
        private readonly Transform _firePointParent;
        private readonly Vector3 _firePointPosition;
        private readonly Quaternion _firePointRotation;

        private readonly List<Renderer> _hidden = new();
        private readonly List<bool> _hiddenWasEnabled = new();

        private float _yaw;
        private float _pitchAngle;

        /// <summary>The trunnion (pitch pivot): where the gun aims from; the barrel's line runs through it.</summary>
        public Transform Pivot => _pitch;

        /// <summary>Where casings leave the gun: on the body's right side; its right is the way they fly.</summary>
        public Transform Ejector => _ejector;

        private MachineGunRig(Transform root, Transform pitch, Transform ejector, Transform space, float pivotHeight,
            Transform firePoint, Transform firePointParent, Vector3 firePointPosition, Quaternion firePointRotation)
        {
            _root = root;
            _pitch = pitch;
            _ejector = ejector;
            _space = space;
            _pivotHeight = pivotHeight;
            _firePoint = firePoint;
            _firePointParent = firePointParent;
            _firePointPosition = firePointPosition;
            _firePointRotation = firePointRotation;
        }

        /// <summary>
        /// Builds the gun in the space of the fire point's parent (the machine-gun entity's object, which turns with
        /// the turret) and moves the fire point to its muzzle. Null without a fire point.
        /// </summary>
        /// <param name="turretVisual">Where the old gun's meshes are looked for (its direct children); may be null.</param>
        public static MachineGunRig Build(Transform firePoint, Transform turretVisual, WeaponSettings settings)
        {
            if (firePoint == null || firePoint.parent == null)
                return null;

            Transform space = firePoint.parent;
            Material material = FindMaterial(turretVisual, space);
            int layer = turretVisual != null ? turretVisual.gameObject.layer : space.gameObject.layer;

            Vector3 body = settings.mgBodySize;
            Vector3 barrel = settings.mgBarrelSize;

            var root = new GameObject("MachineGunRig").transform;
            root.gameObject.layer = layer;
            root.SetParent(space, false);
            root.localPosition = settings.mgMount;
            root.localRotation = Quaternion.identity;
            float scale = Mathf.Max(0.05f, settings.mgScale);
            root.localScale = Vector3.one * scale;

            // The yaw part: a plate on the roof and a post.
            Box("Base", root, new Vector3(0f, BASE_HEIGHT * 0.5f, 0f), new Vector3(0.36f, BASE_HEIGHT, 0.36f), material, layer);
            Box("Post", root, new Vector3(0f, BASE_HEIGHT + POST_HEIGHT * 0.5f, 0f),
                new Vector3(0.12f, POST_HEIGHT, 0.12f), material, layer);

            // The pitch part: the trunnion at the body's middle height, the body sitting on the post.
            float pivotHeight = BASE_HEIGHT + POST_HEIGHT + body.y * 0.5f - 0.03f;
            var pitch = new GameObject("Pitch").transform;
            pitch.gameObject.layer = layer;
            pitch.SetParent(root, false);
            pitch.localPosition = new Vector3(0f, pivotHeight, 0f);
            pitch.localRotation = Quaternion.identity;

            // The body runs from a third behind the trunnion to two thirds before it.
            float bodyRear = -body.z * 0.35f;
            float bodyFront = bodyRear + body.z;
            float bodyMid = (bodyRear + bodyFront) * 0.5f;
            Box("Body", pitch, new Vector3(0f, 0f, bodyMid), body, material, layer);
            Box("Cover", pitch, new Vector3(0f, body.y * 0.5f + 0.03f, bodyMid - body.z * 0.1f),
                new Vector3(body.x * 0.6f, 0.06f, body.z * 0.6f), material, layer);
            Box("AmmoBox", pitch, new Vector3(-(body.x * 0.5f + 0.11f), -0.03f, bodyMid),
                new Vector3(0.22f, 0.26f, 0.32f), material, layer);
            Box("Grip", pitch, new Vector3(0f, -0.03f, bodyRear - 0.06f), new Vector3(0.12f, 0.15f, 0.12f), material, layer);

            // The barrel: a thick jacket, a thinner bore past it, a muzzle at the tip.
            const float BORE_LENGTH = 0.25f;
            const float MUZZLE_LENGTH = 0.12f;
            Box("Jacket", pitch, new Vector3(0f, 0f, bodyFront + barrel.z * 0.5f), barrel, material, layer);
            float boreStart = bodyFront + barrel.z;
            Box("Bore", pitch, new Vector3(0f, 0f, boreStart + BORE_LENGTH * 0.5f),
                new Vector3(barrel.x * 0.55f, barrel.y * 0.55f, BORE_LENGTH), material, layer);
            float muzzleStart = boreStart + BORE_LENGTH;
            Box("Muzzle", pitch, new Vector3(0f, 0f, muzzleStart + MUZZLE_LENGTH * 0.5f),
                new Vector3(barrel.x * 0.85f, barrel.y * 0.85f, MUZZLE_LENGTH), material, layer);

            var ejector = new GameObject("Ejector").transform;
            ejector.SetParent(pitch, false);
            ejector.localPosition = new Vector3(body.x * 0.5f + 0.03f, body.y * 0.15f, bodyMid + body.z * 0.1f);
            ejector.localRotation = Quaternion.identity;

            var rig = new MachineGunRig(root, pitch, ejector, space, pivotHeight * scale, firePoint, firePoint.parent,
                firePoint.localPosition, firePoint.localRotation);

            firePoint.SetParent(pitch, false);
            firePoint.localPosition = new Vector3(0f, 0f, muzzleStart + MUZZLE_LENGTH + 0.03f);
            firePoint.localRotation = Quaternion.identity;

            rig.HideOldGun(turretVisual);
            return rig;
        }

        /// <summary>
        /// Turns toward <paramref name="point"/> (world) at the given rates, the pitch clamped to ±maxPitch. True when
        /// the gun points within <paramref name="fireAngle"/> degrees of it.
        /// </summary>
        public bool Turn(Vector3 point, float maxPitch, float deltaTime, float yawSpeed, float pitchSpeed, float fireAngle)
        {
            Vector3 local = _space.InverseTransformPoint(point) - _root.localPosition;
            local.y -= _pivotHeight;
            float flat = Mathf.Sqrt(local.x * local.x + local.z * local.z);
            if (flat < 0.001f)
                return false;

            float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Clamp(-Mathf.Atan2(local.y, flat) * Mathf.Rad2Deg, -maxPitch, maxPitch);
            Step(yaw, pitch, deltaTime, yawSpeed, pitchSpeed);

            return Mathf.Abs(Mathf.DeltaAngle(_yaw, yaw)) <= fireAngle && Mathf.Abs(_pitchAngle - pitch) <= fireAngle;
        }

        /// <summary>Nothing to shoot: eases back to the turret's forward.</summary>
        public void Rest(float deltaTime, float yawSpeed, float pitchSpeed) =>
            Step(0f, 0f, deltaTime, yawSpeed * 0.5f, pitchSpeed * 0.5f);

        /// <summary>Puts the fire point back where it was, shows the old gun again and removes the boxes.</summary>
        public void Dispose()
        {
            if (_firePoint != null)
            {
                _firePoint.SetParent(_firePointParent, false);
                _firePoint.localPosition = _firePointPosition;
                _firePoint.localRotation = _firePointRotation;
            }

            for (int i = 0; i < _hidden.Count; i++)
                if (_hidden[i] != null)
                    _hidden[i].enabled = _hiddenWasEnabled[i];
            _hidden.Clear();
            _hiddenWasEnabled.Clear();

            if (_root != null)
                Object.Destroy(_root.gameObject);
        }

        /// <summary>Seconds the gun takes to settle on a new direction: it eases in and out instead of snapping.</summary>
        private const float SMOOTH_TIME = 0.12f;

        private float _yawVelocity;
        private float _pitchVelocity;

        private void Step(float yaw, float pitch, float deltaTime, float yawSpeed, float pitchSpeed)
        {
            if (deltaTime <= 0f)
                return;

            // Damped, capped at the turn rates: a new target or a jump of the aim point no longer jerks the gun.
            _yaw = Mathf.SmoothDampAngle(_yaw, yaw, ref _yawVelocity, SMOOTH_TIME, yawSpeed, deltaTime);
            _pitchAngle = Mathf.SmoothDamp(_pitchAngle, pitch, ref _pitchVelocity, SMOOTH_TIME, pitchSpeed, deltaTime);
            _root.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            _pitch.localRotation = Quaternion.Euler(_pitchAngle, 0f, 0f);
        }

        private void HideOldGun(Transform turretVisual)
        {
            if (turretVisual == null)
                return;

            foreach (Transform child in turretVisual)
            {
                if (!child.name.StartsWith(OLD_GUN_PREFIX) || !child.TryGetComponent(out MeshRenderer renderer))
                    continue;
                _hidden.Add(renderer);
                _hiddenWasEnabled.Add(renderer.enabled);
                renderer.enabled = false;
            }
        }

        /// <summary>The old gun's steel (MT_Tank_Steel on Tank_01), else any material on the turret.</summary>
        private static Material FindMaterial(Transform turretVisual, Transform space)
        {
            Transform from = turretVisual != null ? turretVisual : space;
            foreach (Transform child in from)
                if (child.name.StartsWith(OLD_GUN_PREFIX) && child.TryGetComponent(out MeshRenderer renderer) &&
                    renderer.sharedMaterial != null)
                    return renderer.sharedMaterial;

            foreach (MeshRenderer renderer in from.GetComponentsInChildren<MeshRenderer>(true))
                if (renderer.sharedMaterial != null)
                    return renderer.sharedMaterial;
            return null;
        }

        private static void Box(string name, Transform parent, Vector3 center, Vector3 size, Material material, int layer)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            // At once: a deferred Destroy leaves the collider in the tank's body until the frame ends.
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.layer = layer;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = center;
            box.transform.localRotation = Quaternion.identity;
            box.transform.localScale = size;
            if (material != null)
                box.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
