using Fusion;
using UnityEngine;

namespace BallArena
{
    /// <summary>
    /// Значения идут подряд от нуля и без [Flags]: для NetworkButtons значение — это индекс бита.
    /// </summary>
    public enum InputButton
    {
        Dash = 0,
    }

    /// <summary>
    /// Пакет ввода одного тика. Только блиттируемые поля — иначе ткач не сгенерирует сериализацию.
    /// </summary>
    public struct InputData : INetworkInput
    {
        public Vector2 Move;
        public NetworkButtons Buttons;
    }
}
