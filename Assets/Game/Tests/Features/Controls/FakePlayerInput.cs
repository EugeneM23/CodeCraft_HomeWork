using UnityEngine;

namespace Game.Tests
{
    /// <summary>An IPlayerInput the tests set by hand.</summary>
    public sealed class FakePlayerInput : IPlayerInput
    {
        public Vector2 Drive { get; set; }
        public bool Boost { get; set; }
        public bool FireMain { get; set; }
        public bool FireSecondary { get; set; }
        public Vector2 Pointer { get; set; }
        public Vector2 Look { get; set; }
        public bool ResetLookPressed { get; set; }
        public bool FreeCursorPressed { get; set; }
        public bool HoldCursorPressed { get; set; }
        public bool SpecialPressed { get; set; }
    }
}
