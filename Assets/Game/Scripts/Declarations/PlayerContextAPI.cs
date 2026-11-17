using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Player context API definition. Former "header: ContextAPI" yaml, now keyed on IPlayerContext.
    /// </summary>
    [EntityExtensionsAPI]
    public static partial class PlayerContextAPI
    {
        public static readonly ValueKey<IPlayerContext, IReactiveVariable<IEntity>> Character = new(nameof(Character));
        // The one source of the player's controls (Features/Controls/IPlayerInput).
        public static readonly ValueKey<IPlayerContext, Game.IPlayerInput> PlayerInput = new(nameof(PlayerInput));
        public static readonly ValueKey<IPlayerContext, IEntity> Camera = new(nameof(Camera));
        public static readonly ValueKey<IPlayerContext, IValue<Vector3>> CameraOffset = new(nameof(CameraOffset));
        public static readonly ValueKey<IPlayerContext, Transform> CameraRoot = new(nameof(CameraRoot));
        public static readonly ValueKey<IPlayerContext, IValue<int>> CameraSpeed = new(nameof(CameraSpeed));
        public static readonly ValueKey<IPlayerContext, IVariable<Vector3>> CameraLook = new(nameof(CameraLook));
        public static readonly ValueKey<IPlayerContext, Game.CameraLookSettings> CameraLookSettings = new(nameof(CameraLookSettings));
        public static readonly ValueKey<IPlayerContext, Game.CameraCollisionSettings> CameraCollisionSettings = new(nameof(CameraCollisionSettings));
    }
}
