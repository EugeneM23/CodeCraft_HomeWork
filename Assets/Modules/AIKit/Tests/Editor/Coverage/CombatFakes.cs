using System.Collections.Generic;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// A body's launcher that only writes the launches down (nothing flies). TakeOut — it takes a model from the hand out of
    /// it, as the 3D launcher does; off — it leaves the model where it is (a launcher that starts the flight later).
    /// </summary>
    internal sealed class CombatCaptureLauncher : IProjectileLauncher
    {
        public readonly List<ProjectileLaunch> Launches = new();

        public bool TakeOut;

        public void Launch(in ProjectileLaunch launch)
        {
            Launches.Add(launch);
            if (TakeOut && launch.Carried != null)
                launch.Carried.SetParent(null, true);
        }
    }

    /// <summary>A cue that makes a new thing for every throw and writes down which ones were ended, in order.</summary>
    internal sealed class CombatTokenCue : AbilityCue
    {
        public readonly List<object> Made = new();
        public readonly List<object> Ended = new();

        public override object Begin(in AbilityImpact at)
        {
            var made = new object();
            Made.Add(made);
            return made;
        }

        public override void End(object made) => Ended.Add(made);
    }

    /// <summary>What a projectile's landing was told, every time.</summary>
    internal sealed class CombatLandingProbe : IProjectileLanding
    {
        public readonly List<Vector3> Points = new();
        public readonly List<Transform> Struck = new();

        public int Count => Points.Count;

        public void Landed(in ProjectileLaunch launch, Vector3 point, Transform struck)
        {
            Points.Add(point);
            Struck.Add(struck);
        }
    }

    /// <summary>A walk probe with a wall this far ahead in every direction.</summary>
    internal sealed class CombatWallAhead : IWalkProbe
    {
        private readonly float _free;

        public CombatWallAhead(float free) => _free = free;

        public float FreeDistance(Vector3 from, Vector3 direction, float maxDistance) => Mathf.Min(_free, maxDistance);
    }

    /// <summary>A sight probe the test switches: every line clear, or none.</summary>
    internal sealed class CombatSwitchSight : ISightProbe
    {
        public bool Clear = true;

        public bool HasLineOfSight(Vector3 from, Vector3 to) => Clear;
    }
}
