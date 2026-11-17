using UnityEngine;

namespace Saving.Demo
{
    /// <summary>
    /// A script without a single [Save]: its Saveable saves it in the All Fields mode, so the public fields below
    /// come back after a load. Transform and SpriteRenderer (position, color) are saved by their built-in serializers.
    /// </summary>
    public sealed class DemoBox : MonoBehaviour
    {
        public int Moves;
        public string LastColorName = "white";

        private static readonly (string Name, Color Color)[] Colors =
        {
            ("red", new Color(0.9f, 0.3f, 0.3f)), ("green", new Color(0.3f, 0.8f, 0.4f)),
            ("blue", new Color(0.3f, 0.5f, 0.95f)), ("yellow", new Color(0.95f, 0.85f, 0.3f))
        };

        /// <summary>For a button: jumps somewhere else and takes another color.</summary>
        public void Shuffle()
        {
            Moves++;
            transform.localPosition = new Vector3(Random.Range(-6f, 6f), Random.Range(-1.5f, 2.5f), 0f);
            (string name, Color color) = Colors[Random.Range(0, Colors.Length)];
            LastColorName = name;
            if (TryGetComponent(out SpriteRenderer sprite))
                sprite.color = color;
        }
    }
}
