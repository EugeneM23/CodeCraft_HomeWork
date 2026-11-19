using UnityEngine;

namespace Game
{
    /// <summary>
    /// STUB: the clock of StubRunHudSource (a plain class made by AppServices). Lives over scene loads while
    /// Play runs; each frame hands the stub the time and lets it find the battle scene's GameContext.
    /// Goes away with the stub when the core's IRunService adapter replaces it.
    /// </summary>
    internal sealed class StubRunHudSourceRunner : MonoBehaviour
    {
        public StubRunHudSource Source;

        private void Update() // perf-ok: ticks during pause (stub run: binds scenes, offers the next skill after a choice)
        {
            Source?.Tick(Time.deltaTime);
        }

        private void OnDestroy() => Source?.Unbind();
    }
}
