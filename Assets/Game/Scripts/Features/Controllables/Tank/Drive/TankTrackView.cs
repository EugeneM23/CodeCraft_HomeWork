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

            // Interpolation may still carry the hull a frame after the body falls asleep: settled only
            // once a whole frame went by with it standing where it was.
            _hull.GetPositionAndRotation(out Vector3 hullPosition, out Quaternion hullRotation);
            _settled = asleep && caughtUp && speed == 0f && yawRate == 0f && slip == 0f &&
                       hullPosition.Equals(_hullPosition) && hullRotation.Equals(_hullRotation);
            _hullPosition = hullPosition;
            _hullRotation = hullRotation;
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
