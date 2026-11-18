using UnityEngine;

namespace Game
{
    /// <summary>
    /// The tank's smoke screen (special module E). Spitters and the elite lose the tank when it is inside or the
    /// line to it passes through. GameContext key: SmokeScreen.
    /// </summary>
    public interface ISmokeScreen
    {
        bool IsActive { get; }

        bool Contains(Vector3 point);

        bool BlocksLine(Vector3 from, Vector3 to);
    }
}
