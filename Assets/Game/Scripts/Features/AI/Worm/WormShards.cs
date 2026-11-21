using System.Collections;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// Prototype (the user, after a photo of an earthquake-broken road): the worm's rift breaks the ground into flat,
    /// angular slabs. Along the rift they heave up, the edge at the rift lifted and the far edge still in the ground,
    /// one side of the rift higher than the other (a step).
    /// The pieces pop up as the rift passes and sink away after a while, so they do not pile up.
    /// </summary>
    public sealed class WormShards : MonoBehaviour
    {
        private const float LIFE = 20f;             // s a slab stays before it sinks
        private const float SINK = 3f;
        private const float POP = 0.12f;            // s it takes to heave up
        private const float THICK = 0.25f;     // m: two or three of the ground's voxels
        private const float HIGH_SIDE = 1f;         // the side of the rift that heaves higher (the step)

        private static WormShards s_root;
        private static Material[] s_slabs;

        /// <summary>
        /// A slab heaved up beside the rift at <paramref name="at"/>, which runs along <paramref name="along"/>, on
        /// <paramref name="side"/> (+1 right, −1 left), sized by <paramref name="scale"/>.
        /// </summary>
        public static void Heave(Vector3 at, Vector3 along, float side, float scale = 1f)
        {
            if (WormPrototype.Covered(at))
                return; // under a house: the crack runs on beneath it, nothing heaves up
            Root().StartCoroutine(s_root.Slab(at, along, side, scale));
        }

        private static WormShards Root()
        {
            if (s_root != null)
                return s_root;
            s_root = new GameObject("[WormShards]").AddComponent<WormShards>();
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Material Make(Color c) => lit != null ? new Material(lit) { color = c } : new Material(Shader.Find("Standard")) { color = c };
            s_slabs = new[]
            {
                Make(new Color(0.30f, 0.29f, 0.28f)), // asphalt
                Make(new Color(0.36f, 0.34f, 0.31f)),
                Make(new Color(0.33f, 0.27f, 0.21f)), // packed earth
                Make(new Color(0.26f, 0.25f, 0.25f))
            };
            return s_root;
        }

        private IEnumerator Slab(Vector3 at, Vector3 along, float side, float scale)
        {
            Vector3 dir = along.sqrMagnitude > 1e-4f ? along.normalized : Vector3.forward;
            Vector3 across = Vector3.Cross(Vector3.up, dir) * side;
            bool high = side * HIGH_SIDE > 0f;

            // Flat and angular: longer along the rift than across, a little skewed off it.
            float length = Random.Range(1f, 2.2f) * scale;
            float width = Random.Range(0.7f, 1.4f) * scale;
            float skew = Random.Range(-25f, 25f);
            float tilt = Random.Range(high ? 18f : 8f, high ? 38f : 20f); // the rift's edge up, the far edge down

            // Hinged on its far edge, in the ground; the near edge, at the rift, lifts.
            Quaternion flat = Quaternion.LookRotation(Quaternion.AngleAxis(skew, Vector3.up) * dir, Vector3.up);
            Vector3 hinge = at + across * (width * 0.5f + 0.15f) + Vector3.down * (THICK * 0.5f);

            // Our own ground (the user): built of its voxels, coloured as it lay there flat.
            Vector3 flatCentre = hinge + flat * (Vector3.right * (-side * width * 0.5f));
            Transform slab = VoxelChunkMesh.SlabBody(new Vector3(width, THICK, length), flatCentre, flat, transform);
            Vector3 axis = flat * Vector3.forward;
            Quaternion lifted = Quaternion.AngleAxis(tilt * -side, axis) * flat;
            // A corner knocked off now and then: a second, smaller piece standing on the first.
            if (Random.value < 0.3f)
            {
                Transform chip = VoxelChunkMesh.SlabBody(new Vector3(width * 0.4f, THICK, length * 0.35f), flatCentre, flat, transform);
                StartCoroutine(Sink(chip, at + across * (width * 0.3f) + Vector3.up * 0.1f,
                    Quaternion.AngleAxis(Random.Range(30f, 60f) * -side, axis) * flat * Quaternion.Euler(0f, Random.Range(-40f, 40f), 0f)));
            }

            for (float t = 0f; t < POP; t += Time.deltaTime)
            {
                Place(slab, hinge, Quaternion.Slerp(flat, lifted, t / POP), width, side);
                yield return null;
            }
            Place(slab, hinge, lifted, width, side);
            yield return Sink(slab, slab.position, slab.rotation);
        }

        // The slab's far edge sits on the hinge; it swings up about it.
        private static void Place(Transform slab, Vector3 hinge, Quaternion rotation, float width, float side)
        {
            Vector3 toCentre = rotation * (Vector3.right * (-side * width * 0.5f));
            slab.SetPositionAndRotation(hinge + toCentre, rotation);
        }

        private IEnumerator Sink(Transform piece, Vector3 position, Quaternion rotation)
        {
            piece.SetPositionAndRotation(position, rotation);
            yield return new WaitForSeconds(LIFE);
            Vector3 down = position + Vector3.down * 0.8f;
            for (float t = 0f; t < SINK; t += Time.deltaTime)
            {
                if (piece == null)
                    yield break;
                piece.position = Vector3.Lerp(position, down, t / SINK);
                yield return null;
            }
            if (piece != null)
                Destroy(piece.gameObject);
        }

        // ---- The burst (the user: epic, the ground left sticking up) ----

        private const float ERUPT_LIFE = 40f;       // s the crater's lip stays

        /// <summary>
        /// The ground torn open round a hole of <paramref name="radius"/>: a ring of big slabs heaved up steeply, their
        /// inner edges up and the outer ones still in the ground; boulders of earth in and round the hole; chunks thrown
        /// up that come down about it. <paramref name="scale"/> 1 — the worm coming out. <paramref name="inward"/> — the
        /// lip caves in (the dive): its slabs lean down into the hole instead of up out of it.
        /// </summary>
        public static void Erupt(Vector3 at, float radius, float scale = 1f, bool inward = false)
        {
            WormShards root = Root();
            int slabs = Mathf.RoundToInt(Random.Range(8, 13) * Mathf.Lerp(0.6f, 1f, scale));
            float a0 = Random.value * 360f;
            for (int i = 0; i < slabs; i++)
            {
                float a = a0 + i * 360f / slabs + Random.Range(-12f, 12f);
                Vector3 outward = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                root.StartCoroutine(root.LipSlab(at, outward, radius, scale, inward));
            }

            int rocks = Mathf.RoundToInt(Random.Range(6, 10) * scale);
            for (int i = 0; i < rocks; i++)
            {
                Vector3 p = at + Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.forward * Random.Range(0f, radius * 1.1f);
                root.StartCoroutine(root.Boulder(p, Random.Range(0.35f, 0.9f) * scale, false));
            }

            int thrown = Mathf.RoundToInt(Random.Range(5, 9) * scale);
            for (int i = 0; i < thrown; i++)
                root.StartCoroutine(root.Boulder(at, Random.Range(0.3f, 0.7f) * scale, true));
        }

        // A slab of the crater's lip: hinged on its outer edge, its inner edge thrown up steeply over the hole.
        private IEnumerator LipSlab(Vector3 at, Vector3 outward, float radius, float scale, bool inward)
        {
            float length = Random.Range(1.4f, 2.8f) * scale;   // across the ring
            float width = Random.Range(1f, 1.8f) * scale;      // round it
            float tilt = Random.Range(35f, 75f);
            Vector3 hingePoint = at + outward * (radius + length * Random.Range(0.6f, 0.9f));
            if (WormPrototype.Ground(hingePoint, out Vector3 g) && Mathf.Abs(g.y - at.y) < 1.5f)
                hingePoint = g;
            if (WormPrototype.Covered(hingePoint))
                yield break; // a house stands there: no slab heaves up into it
            hingePoint += Vector3.down * 0.15f;

            // Facing in: its length runs from the hinge towards the hole; it swings up about its right axis.
            Quaternion flat = Quaternion.LookRotation(-outward, Vector3.up) * Quaternion.Euler(0f, Random.Range(-15f, 15f), 0f);
            // Our own ground, coloured as it lay flat there before the burst.
            Transform slab = VoxelChunkMesh.SlabBody(new Vector3(width, THICK * 1.6f * scale, length),
                hingePoint + flat * (Vector3.forward * (length * 0.5f)), flat, transform);
            // Out of the ground (the burst) the inner edge is thrown up; caving in (the dive, the user) it drops into the hole.
            float lean = inward ? Random.Range(25f, 55f) : -tilt;
            Quaternion up = flat * Quaternion.Euler(lean, 0f, Random.Range(-10f, 10f));
            const float pop = 0.18f;
            for (float t = 0f; t < pop; t += Time.deltaTime)
            {
                // Overshooting a little before it settles.
                float k = t / pop;
                Quaternion r = Quaternion.SlerpUnclamped(flat, up, 1f + 0.25f * Mathf.Sin(k * Mathf.PI) - (1f - k) * (1f - k));
                slab.SetPositionAndRotation(hingePoint + r * (Vector3.forward * (length * 0.5f)), r);
                yield return null;
            }
            slab.SetPositionAndRotation(hingePoint + up * (Vector3.forward * (length * 0.5f)), up);
            yield return SinkAfter(slab, ERUPT_LIFE);
        }

        // A lump of earth: lying in the hole, or thrown up out of it to come down a few metres off.
        private IEnumerator Boulder(Vector3 at, float size, bool thrown)
        {
            Transform rock = VoxelChunkMesh.LumpBody(size, at, transform);
            rock.rotation = Random.rotation;

            Vector3 land = at;
            if (thrown)
                land = at + Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.forward * Random.Range(3f, 8f);
            if (WormPrototype.Ground(land, out Vector3 g) && Mathf.Abs(g.y - at.y) < 3f)
                land = g;
            if (WormPrototype.Covered(land))
            {
                Destroy(rock.gameObject); // it would land in a house
                yield break;
            }
            land += Vector3.up * (size * 0.3f);

            if (thrown)
            {
                float flight = Random.Range(0.7f, 1.2f);
                float height = Random.Range(4f, 9f);
                Quaternion spin = Quaternion.Euler(Random.Range(-400f, 400f), Random.Range(-400f, 400f), 0f);
                for (float t = 0f; t < flight; t += Time.deltaTime)
                {
                    float k = t / flight;
                    rock.position = Vector3.Lerp(at, land, k) + Vector3.up * (4f * height * k * (1f - k));
                    rock.rotation = Quaternion.SlerpUnclamped(Quaternion.identity, spin, Time.deltaTime) * rock.rotation;
                    yield return null;
                }
            }
            rock.position = land;
            yield return SinkAfter(rock, ERUPT_LIFE);
        }

        private IEnumerator SinkAfter(Transform piece, float life)
        {
            yield return new WaitForSeconds(life);
            Vector3 from = piece != null ? piece.position : default;
            for (float t = 0f; t < SINK && piece != null; t += Time.deltaTime)
            {
                piece.position = from + Vector3.down * (t / SINK * 1.5f);
                yield return null;
            }
            if (piece != null)
                Destroy(piece.gameObject);
        }
    }
}
