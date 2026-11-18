using System;
using UnityEngine;

namespace Game.Voxels.Tests
{
    /// <summary>
    /// Calls <see cref="Step"/> every physics step before any other script's FixedUpdate: for tests that
    /// hold a tank's stick (the player's controller writes it in LateTick) and look at every step.
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    public sealed class FixedStepProbe : MonoBehaviour
    {
        public Action Step;

        private void FixedUpdate() => Step?.Invoke();
    }
}
