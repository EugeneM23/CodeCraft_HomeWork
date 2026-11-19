using System;
using System.Collections.Generic;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>The lower run of one track: where it ends front and rear, and its links in between.</summary>
    [Serializable]
    public sealed class TankTrackRig
    {
        [Tooltip("The idler at the front of the track.")]
        public Transform Front;

        [Tooltip("The sprocket at the rear of the track.")]
        public Transform Rear;

        [Tooltip("Lower-run links, front to rear: one more than there are road wheels on this side.")]
        public Transform[] Links;

        [Tooltip("Radius (m) of the wheel at Front: it turns with the track. 0 — it does not turn.")]
        public float FrontRadius;

        [Tooltip("Radius (m) of the wheel at Rear: it turns with the track. 0 — it does not turn.")]
        public float RearRadius;

        [Tooltip("The whole track as a loop of links (children of one transform, its space the hull's): " +
                 "round Front, over the top, round Rear and under the wheels; they run with the ground. Empty — none.")]
        public Transform[] Treads;
    }

    /// <summary>
    /// Shows the suspension working. Each road wheel sits where its spring holds it and turns with
    /// the ground going by; on the spot the two sides turn against each other. The lower run of
    /// each track is a chain of links from the idler, under every wheel, to the sprocket, so it
    /// bends wherever the wheels go.
    ///
    /// Spring lengths come from the physics step; drawn as they are they would step at 50 Hz while
    /// the interpolated hull moves smoothly, so the drawn length eases towards the measured one.
    /// </summary>
    public sealed class TankTrackView : IEntityInit, IEntityTick
    {
        /// <summary>How fast (1/s) a drawn wheel catches up with its spring.</summary>
        private const float FOLLOW = 30f;

        /// <summary>Links are drawn this much longer than the gap, so the chain never shows seams.</summary>
        private const float LINK_OVERLAP = 0.04f;

        /// <summary>A drawn wheel this close (m) to its spring's length has caught up with it.</summary>
        private const float CAUGHT_UP = 1e-5f;

        private readonly TankTrackRig _left;
        private readonly TankTrackRig _right;

        // The last frame drew a sleeping hull (a parked tank: TankSuspensionBehaviour) at rest, every
        // wheel caught up with its spring, the hull where the frame before had left it. Asleep, the
        // body has no speed, does not move and its springs are not updated, so the same 44 poses
        // would be written again every frame: they are skipped until it wakes.
        private bool _settled;
        private Vector3 _hullPosition;
        private Quaternion _hullRotation;

        private Transform _hull;
        private Rigidbody _body;
        private TankSuspension _suspension;

        private TankWheel[] _leftWheels;
        private TankWheel[] _rightWheels;
        private float[] _drawnLength;
        private float[] _spin;
        private Vector2 _linkSection;
        private readonly List<Vector3> _points = new();

        /// <summary>Extra track speed (m/s) drawn at full wheel spin, on top of the ground going by.</summary>
        private const float SPIN_SPEED = 9f;

        private readonly TankWheelSpinBehaviour _wheelSpin;

        public TankTrackView(TankTrackRig left, TankTrackRig right, TankWheelSpinBehaviour wheelSpin = null)
        {
            _left = left;
            _right = right;
            _wheelSpin = wheelSpin;
        }

        public void Init(IEntity entity)
        {
            _hull = entity.GetTransform();
            _body = entity.GetRiggedBody();
            _suspension = entity.GetSuspension();

            TankWheel[] wheels = _suspension.Wheels;
            _drawnLength = new float[wheels.Length];
            _spin = new float[wheels.Length];

            for (int i = 0; i < wheels.Length; i++)
                _drawnLength[i] = wheels[i].Length;

            _leftWheels = Side(wheels, -1);
            _rightWheels = Side(wheels, 1);
            _leftLoop = new TreadLoop(_leftWheels.Length);
            _rightLoop = new TreadLoop(_rightWheels.Length);

            Transform anyLink = _left?.Links is { Length: > 0 } ? _left.Links[0] : null;
            _linkSection = anyLink != null ? new Vector2(anyLink.localScale.x, anyLink.localScale.y) : new Vector2(0.4f, 0.05f);
        }

        /// <summary>One side's wheels, front to rear.</summary>
        private static TankWheel[] Side(TankWheel[] wheels, int side)
        {
            List<TankWheel> list = new();
            foreach (TankWheel wheel in wheels)
                if (wheel.Side == side)
                    list.Add(wheel);

            list.Sort((a, b) => b.Mount.localPosition.z.CompareTo(a.Mount.localPosition.z));
            return list.ToArray();
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            bool asleep = _body.IsSleeping();
            if (asleep && _settled)
                return;

            Vector3 up = _hull.up;
            float follow = 1f - Mathf.Exp(-FOLLOW * deltaTime);

            float speed = Vector3.Dot(_body.linearVelocity, _hull.forward);
            float yawRate = Vector3.Dot(_body.angularVelocity, up);
            bool caughtUp = true;

            // Shift on the gas: the tracks run faster than the ground goes by.
            float slip = _wheelSpin != null ? _wheelSpin.Spin * _wheelSpin.Direction * SPIN_SPEED : 0f;

            TankWheel[] wheels = _suspension.Wheels;
            for (int i = 0; i < wheels.Length; i++)
            {
                TankWheel wheel = wheels[i];
                float target = Mathf.Clamp(wheel.Length, _suspension.MinLength, _suspension.MaxLength);
                _drawnLength[i] = Mathf.Lerp(_drawnLength[i], target, follow);
                caughtUp &= Mathf.Abs(target - _drawnLength[i]) <= CAUGHT_UP;

                wheel.Visual.position = wheel.Mount.position - up * _drawnLength[i];

                // A point on the left track moves forward faster in a right turn, and the reverse.
                float trackSpeed = speed + slip - wheel.Side * yawRate * Mathf.Abs(wheel.Mount.localPosition.x);
                _spin[i] = Mathf.Repeat(_spin[i] + trackSpeed * deltaTime / _suspension.WheelRadius * Mathf.Rad2Deg, 360f);
                wheel.Visual.localRotation = Quaternion.Euler(_spin[i], 0f, 0f);
            }

            LayLinks(_left, _leftWheels, up);
            LayLinks(_right, _rightWheels, up);

            // Each side's track runs as far as its ground goes by (and further on full spin): the sprocket and
            // idler turn with it and the tread loop moves round by it.
            _leftRun += SideSpeed(_leftWheels, -1, speed, slip, yawRate) * deltaTime;
            _rightRun += SideSpeed(_rightWheels, 1, speed, slip, yawRate) * deltaTime;
            TurnEnds(_left, _leftRun);
            TurnEnds(_right, _rightRun);
            LayTreads(_left, _leftWheels, _leftRun, _leftLoop);
            LayTreads(_right, _rightWheels, _rightRun, _rightLoop);

            // Interpolation may still carry the hull a frame after the body falls asleep: settled only
            // once a whole frame went by with it standing where it was.
            _hull.GetPositionAndRotation(out Vector3 hullPosition, out Quaternion hullRotation);
            _settled = asleep && caughtUp && speed == 0f && yawRate == 0f && slip == 0f &&
                       hullPosition.Equals(_hullPosition) && hullRotation.Equals(_hullRotation);
            _hullPosition = hullPosition;
            _hullRotation = hullRotation;
        }

        // ---------------------------------------------------------------- sprocket, idler and the tread loop

        /// <summary>Points on an arc round the sprocket or the idler.</summary>
        private const int ARC_STEPS = 8;

        private float _leftRun;
        private float _rightRun;
        private TreadLoop _leftLoop;
        private TreadLoop _rightLoop;

        /// <summary>One side's loop path, rebuilt every frame into arrays made once.</summary>
        private sealed class TreadLoop
        {
            public readonly Vector3[] Points;
            public readonly float[] Along;
            public int Count;

            public TreadLoop(int wheels)
            {
                int capacity = 2 * (ARC_STEPS + 1) + wheels + 1;
                Points = new Vector3[capacity];
                Along = new float[capacity];
            }

            public void Clear() => Count = 0;

            public void Add(Vector3 p)
            {
                if (Count < Points.Length)
                    Points[Count++] = p;
            }
        }

        /// <summary>How fast one side's track runs along the hull (m/s), as its wheels are spun.</summary>
        private float SideSpeed(TankWheel[] wheels, int side, float speed, float slip, float yawRate)
        {
            float halfTrack = wheels.Length > 0 ? Mathf.Abs(wheels[0].Mount.localPosition.x) : 0f;
            return speed + slip - side * yawRate * halfTrack;
        }

        private static void TurnEnds(TankTrackRig rig, float run)
        {
            if (rig == null)
                return;
            if (rig.Front != null && rig.FrontRadius > 0f)
                rig.Front.localRotation = Quaternion.Euler(Mathf.Repeat(run / rig.FrontRadius * Mathf.Rad2Deg, 360f), 0f, 0f);
            if (rig.Rear != null && rig.RearRadius > 0f)
                rig.Rear.localRotation = Quaternion.Euler(Mathf.Repeat(run / rig.RearRadius * Mathf.Rad2Deg, 360f), 0f, 0f);
        }

        /// <summary>
        /// Lays the tread loop: from the top of the sprocket back over the top to the idler, round the idler,
        /// forward under the wheels (where they stand on their springs), round the sprocket and up to the start.
        /// The links sit evenly along it, shifted back by how far the track has run: on the ground they stand
        /// still while the tank goes over them.
        /// </summary>
        private void LayTreads(TankTrackRig rig, TankWheel[] wheels, float run, TreadLoop loop)
        {
            if (rig == null || rig.Treads == null || rig.Treads.Length == 0 || rig.Front == null || rig.Rear == null)
                return;

            Transform space = rig.Treads[0].parent;
            float half = rig.Treads[0].localScale.y * 0.5f;
            float frontR = Mathf.Max(rig.FrontRadius, 0.05f) + half;
            float rearR = Mathf.Max(rig.RearRadius, 0.05f) + half;
            Vector3 front = space.InverseTransformPoint(rig.Front.position);
            Vector3 rear = space.InverseTransformPoint(rig.Rear.position);
            Vector3 down = space.InverseTransformDirection(-_hull.up);
            float under = _suspension.WheelRadius - half;

            loop.Clear();
            for (int k = 0; k <= ARC_STEPS; k++)
            {
                // Round the idler, from its top backward to its bottom.
                float a = Mathf.PI * k / ARC_STEPS;
                if (k == 0)
                    loop.Add(front + new Vector3(0f, frontR, 0f));
                loop.Add(rear + new Vector3(0f, Mathf.Cos(a), -Mathf.Sin(a)) * rearR);
            }

            for (int i = wheels.Length - 1; i >= 0; i--)
                loop.Add(space.InverseTransformPoint(wheels[i].Visual.position) + down * under);

            for (int k = 0; k < ARC_STEPS; k++)
            {
                // Round the sprocket, from its bottom forward and up (the top is the loop's first point).
                float a = Mathf.PI * k / ARC_STEPS;
                loop.Add(front + new Vector3(0f, -Mathf.Cos(a), Mathf.Sin(a)) * frontR);
            }

            int n = loop.Count;
            float length = 0f;
            for (int i = 0; i < n; i++)
            {
                loop.Along[i] = length;
                length += Vector3.Distance(loop.Points[i], loop.Points[(i + 1) % n]);
            }

            if (length < 1e-3f)
                return;

            int links = rig.Treads.Length;
            float pitch = length / links;
            // On top the loop runs backward; a tank going forward moves its top run forward, so the shift is negative.
            float start = Mathf.Repeat(-run, length);
            int segment = 0;
            for (int i = 0; i < links; i++)
            {
                float s = Mathf.Repeat(start + i * pitch, length);
                // Links go round in order, so the segment search walks on (and wraps once).
                int guard = 0;
                while (guard++ < 2 * n && !(s >= loop.Along[segment] && s < (segment + 1 < n ? loop.Along[segment + 1] : length)))
                    segment = (segment + 1) % n;

                Vector3 a = loop.Points[segment];
                Vector3 b = loop.Points[(segment + 1) % n];
                float span = (segment + 1 < n ? loop.Along[segment + 1] : length) - loop.Along[segment];
                float t = span > 1e-5f ? (s - loop.Along[segment]) / span : 0f;
                Vector3 tangent = b - a;
                if (tangent.sqrMagnitude < 1e-8f)
                    continue;
                tangent.Normalize();
                Vector3 outward = Vector3.Cross(Vector3.right, tangent);
                rig.Treads[i].SetLocalPositionAndRotation(Vector3.Lerp(a, b, t), Quaternion.LookRotation(tangent, outward));
            }
        }

        private void LayLinks(TankTrackRig rig, TankWheel[] wheels, Vector3 up)
        {
            if (rig == null || rig.Links == null || rig.Front == null || rig.Rear == null)
                return;

            // The cast sphere is the wheel with its track on: a link's bottom is where the sphere
            // meets the ground, its centre half a link up from there.
            float under = _suspension.WheelRadius - _linkSection.y * 0.5f;

            _points.Clear();
            _points.Add(rig.Front.position);
            foreach (TankWheel wheel in wheels)
                _points.Add(wheel.Visual.position - up * under);
            _points.Add(rig.Rear.position);

            int links = Mathf.Min(rig.Links.Length, _points.Count - 1);
            for (int i = 0; i < links; i++)
            {
                Transform link = rig.Links[i];
                Vector3 a = _points[i];
                Vector3 b = _points[i + 1];
                Vector3 span = b - a;
                float length = span.magnitude;
                if (length < 1e-4f)
                    continue;

                link.SetPositionAndRotation((a + b) * 0.5f, Quaternion.LookRotation(span / length, up));
                link.localScale = new Vector3(_linkSection.x, _linkSection.y, length + LINK_OVERLAP);
            }
        }
    }
}
