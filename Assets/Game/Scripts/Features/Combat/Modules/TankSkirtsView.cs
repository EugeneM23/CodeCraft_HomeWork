using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The screens (ArmorSkirts) on the model: two thin plain boxes along the hull's sides, hung on the hull's visual
    /// so they sway with it. No colliders: the tank's physics and hits stay as they are. Made when the loadout is
    /// applied with screens, gone without them.
    /// </summary>
    public sealed class TankSkirtsView
    {
        /// <summary>Plate thickness (m).</summary>
        private const float THICKNESS = 0.06f;

        /// <summary>Gap (m) between the hull box's side and a plate.</summary>
        private const float GAP = 0.04f;

        /// <summary>Share of the hull's length and height a plate covers.</summary>
        private const float LENGTH_SHARE = 0.85f;
        private const float HEIGHT_SHARE = 0.45f;

        /// <summary>How far down the hull's side the plate's middle sits, as a share of the hull's height.</summary>
        private const float DROP_SHARE = 0.15f;

        private GameObject _left;
        private GameObject _right;

        public bool IsShown => _left != null;

        /// <summary>Puts the plates on (once); false without a hull to hang them on.</summary>
        public bool Show(IEntity tank)
        {
            if (IsShown)
                return true;

            if (tank == null || !tank.TryGetRiggedBody(out Rigidbody body) || body == null)
                return false;

            Transform parent = tank.TryGetHullVisual(out Transform hull) && hull != null ? hull : body.transform;
            BoxCollider box = BiggestBox(body);
            if (box == null)
                return false;

            Material material = FindMaterial(parent);
            _left = MakePlate(box, parent, material, -1f);
            _right = MakePlate(box, parent, material, 1f);
            return true;
        }

        public void Hide()
        {
            if (_left != null)
                Object.Destroy(_left);
            if (_right != null)
                Object.Destroy(_right);
            _left = null;
            _right = null;
        }

        private static GameObject MakePlate(BoxCollider box, Transform parent, Material material, float side)
        {
            Transform frame = box.transform;
            Vector3 size = Vector3.Scale(box.size, frame.lossyScale);

            // Made away from the tank and its collider removed at once: a collider under the tank's body even for a
            // frame would join its compound collider and shift its centre of mass.
            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = side < 0f ? "SkirtLeft" : "SkirtRight";
            Collider collider = plate.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);

            Vector3 local = box.center + new Vector3(
                side * (box.size.x * 0.5f + (GAP + THICKNESS * 0.5f) / Mathf.Max(1e-4f, frame.lossyScale.x)),
                -box.size.y * DROP_SHARE,
                0f);
            plate.transform.SetPositionAndRotation(frame.TransformPoint(local), frame.rotation);
            plate.transform.localScale = new Vector3(THICKNESS, size.y * HEIGHT_SHARE, size.z * LENGTH_SHARE);
            plate.transform.SetParent(parent, true);
            plate.layer = parent.gameObject.layer;

            if (material != null && plate.TryGetComponent(out MeshRenderer renderer))
                renderer.sharedMaterial = material;

            return plate;
        }

        /// <summary>A material the tank's own model already uses; null keeps the cube's default.</summary>
        private static Material FindMaterial(Transform hull)
        {
            MeshRenderer renderer = hull.GetComponentInChildren<MeshRenderer>(true);
            return renderer != null ? renderer.sharedMaterial : null;
        }

        /// <summary>The hull's own box: the biggest solid box collider on the tank's body (as TankCrushBehaviour has it).</summary>
        private static BoxCollider BiggestBox(Rigidbody body)
        {
            BoxCollider biggest = null;
            float volume = 0f;
            foreach (BoxCollider box in body.GetComponents<BoxCollider>())
            {
                if (box.isTrigger)
                    continue;
                Vector3 size = Vector3.Scale(box.size, box.transform.lossyScale);
                float v = Mathf.Abs(size.x * size.y * size.z);
                if (v > volume)
                {
                    volume = v;
                    biggest = box;
                }
            }

            return biggest;
        }
    }
}
