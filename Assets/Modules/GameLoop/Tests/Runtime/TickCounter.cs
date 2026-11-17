using UnityEngine;

namespace Ticking.Tests
{
    /// <summary>A component on the loop that counts its ticks (tests of the module).</summary>
    public sealed class TickCounter : MonoBehaviour, IUpdateListener, IFixedUpdateListener, ILateUpdateListener
    {
        public int Updates { get; private set; }
        public int FixedUpdates { get; private set; }
        public int LateUpdates { get; private set; }

        public void OnUpdate(float deltaTime) => Updates++;

        public void OnFixedUpdate(float deltaTime) => FixedUpdates++;

        public void OnLateUpdate(float deltaTime) => LateUpdates++;
    }
}
