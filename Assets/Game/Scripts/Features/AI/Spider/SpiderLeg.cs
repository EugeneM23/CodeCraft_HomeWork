using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// One leg of <see cref="SpiderBoss"/>: hip, femur, tibia and claw out of cubes. The claw's tip is planted at a point
    /// a raycast found (ground, wall, roof) and stays there while the body walks on; the knee and ankle come from a
    /// 2-bone analytic IK (law of cosines), the claw itself points into the surface along its normal.
    /// </summary>
    internal sealed class SpiderLeg
    {
        private const float FEMUR = 1.5f;
        private const float TIBIA = 1.6f;
        private const float CLAW = 0.55f;
        private const float FEMUR_THICK = 0.26f;
        private const float TIBIA_THICK = 0.19f;
        private const float CLAW_THICK = 0.11f;
        private const float KNEE_KNOB = 0.27f;

        /// <summary>Hip to ankle at full stretch: past this the foot is torn off and stepped anew.</summary>
        public const float REACH = FEMUR + TIBIA;

        public readonly Vector3 HipLocal;
        public readonly Vector3 RestLocal;
        public readonly int Group;

        public Vector3 Foot;
        public Vector3 Normal = Vector3.up;
        public bool Planted;
        public bool Stepping;

        private Vector3 _from, _to, _fromNormal, _toNormal;
        private float _t;

        private readonly Transform _femur, _tibia, _claw, _knee, _ankle;

        public SpiderLeg(Transform parent, Vector3 hipLocal, Vector3 restLocal, int group, Material material, Material claw)
        {
            HipLocal = hipLocal;
            RestLocal = restLocal;
            Group = group;
            _femur = Cube(parent, "Femur", material);
            _tibia = Cube(parent, "Tibia", material);
            _claw = Cube(parent, "Claw", claw);
            _knee = Cube(parent, "Knee", material);
            _ankle = Cube(parent, "Ankle", material);
            _knee.localScale = Vector3.one * KNEE_KNOB;
            _ankle.localScale = Vector3.one * (KNEE_KNOB * 0.7f);
        }

        /// <summary>Lifts the foot off and carries it along an arc to <paramref name="to"/>, planted there on landing.</summary>
        public void Step(Vector3 to, Vector3 normal, Vector3 bodyUp)
        {
            _from = Foot;
            _fromNormal = Planted ? Normal : bodyUp;
            _to = to;
            _toNormal = normal;
            _t = 0f;
            Stepping = true;
            Planted = false;
        }

        /// <summary>Moves a stepping foot on; true on the frame it lands.</summary>
        public bool Advance(float dt, float stepTime, float stepHeight, Vector3 bodyUp)
        {
            if (!Stepping)
                return false;
            _t += dt / stepTime;
            if (_t >= 1f)
            {
                Foot = _to;
                Normal = _toNormal;
                Stepping = false;
                Planted = true;
                return true;
            }
            float s = Mathf.SmoothStep(0f, 1f, _t);
            // Up and over along the mean of the two surfaces and the body: a foot going from the ground onto a wall
            // swings out round the corner, not through it; it comes down onto the surface at the end, claw first.
            Vector3 lift = (_fromNormal + _toNormal + bodyUp).normalized;
            Foot = Vector3.Lerp(_from, _to, s) + lift * (Mathf.Sin(_t * Mathf.PI) * stepHeight);
            Normal = Vector3.Slerp(_fromNormal, _toNormal, s);
            return false;
        }

        /// <summary>Hangs the foot in the air (no surface in reach), not planted.</summary>
        public void Dangle(Vector3 at, Vector3 bodyUp)
        {
            Foot = Vector3.Lerp(Foot, at, 0.2f);
            Normal = bodyUp;
            Planted = false;
            Stepping = false;
        }

        /// <summary>IK: the cubes from the hip to the planted claw.</summary>
        public void Pose(Transform body, Vector3 bodyUp)
        {
            Vector3 hip = body.TransformPoint(HipLocal);
            Vector3 outward = Vector3.ProjectOnPlane(hip - body.position, bodyUp).normalized;

            // The claw goes into the surface steeply: from the tip back out along the normal, leaning a little to the hip.
            Vector3 toHip = Vector3.ProjectOnPlane(hip - Foot, Normal);
            Vector3 clawDir = (Normal * 0.85f + (toHip.sqrMagnitude > 1e-4f ? toHip.normalized : Vector3.zero) * 0.4f).normalized;
            Vector3 ankle = Foot + clawDir * CLAW;

            // Two bones, hip → ankle, the knee bending up and out (a spider's high knees).
            Vector3 pole = bodyUp + outward * 0.6f;
            Vector3 tibiaEnd = SolveTwoBone(hip, ankle, FEMUR, TIBIA, pole, out Vector3 knee);

            Place(_femur, hip, knee, FEMUR_THICK, bodyUp);
            Place(_tibia, knee, tibiaEnd, TIBIA_THICK, bodyUp);
            // Out of reach (a long step, a foot torn away): the claw stretches to the tip, the tip stays on the surface.
            Place(_claw, tibiaEnd, Foot, CLAW_THICK, bodyUp);
            _knee.position = knee;
            _knee.rotation = body.rotation;
            _ankle.position = tibiaEnd;
            _ankle.rotation = body.rotation;
        }

        /// <summary>
        /// Analytic 2-bone IK: the knee of a chain of <paramref name="a"/> and <paramref name="b"/> from
        /// <paramref name="root"/> to <paramref name="target"/>, bent towards <paramref name="pole"/>. Returns the chain's
        /// end — the target itself when in reach, else as far towards it as the bones go.
        /// </summary>
        public static Vector3 SolveTwoBone(Vector3 root, Vector3 target, float a, float b, Vector3 pole, out Vector3 knee)
        {
            Vector3 span = target - root;
            float d = span.magnitude;
            Vector3 dir = d > 1e-5f ? span / d : Vector3.down;
            float dc = Mathf.Clamp(d, Mathf.Abs(a - b) + 1e-3f, a + b - 1e-4f);
            float cos = (a * a + dc * dc - b * b) / (2f * a * dc);
            float sin = Mathf.Sqrt(Mathf.Max(0f, 1f - cos * cos));
            Vector3 bend = Vector3.ProjectOnPlane(pole, dir);
            if (bend.sqrMagnitude < 1e-6f)
                bend = Vector3.ProjectOnPlane(Vector3.up, dir);
            if (bend.sqrMagnitude < 1e-6f)
                bend = Vector3.ProjectOnPlane(Vector3.forward, dir);
            bend.Normalize();
            knee = root + dir * (a * cos) + bend * (a * sin);
            return root + dir * dc;
        }

        private static void Place(Transform segment, Vector3 a, Vector3 b, float thick, Vector3 up)
        {
            Vector3 span = b - a;
            float length = span.magnitude;
            segment.position = (a + b) * 0.5f;
            if (length > 1e-4f)
                segment.rotation = Quaternion.LookRotation(span / length, Mathf.Abs(Vector3.Dot(span / length, up)) > 0.98f ? Vector3.forward : up);
            segment.localScale = new Vector3(thick, thick, Mathf.Max(length, 0.01f));
        }

        private static Transform Cube(Transform parent, string name, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go.transform;
        }
    }
}
