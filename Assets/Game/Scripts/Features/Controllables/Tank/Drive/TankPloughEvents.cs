using System;
using UnityEngine;

namespace Game
{
    /// <summary>A tank's nose ploughed into the ground: point, strength 0..1. For sound.</summary>
    public static class TankPloughEvents
    {
        public static event Action<Vector3, float> Ploughed;

        public static void RaisePloughed(Vector3 point, float strength) => Ploughed?.Invoke(point, strength);
    }
}
