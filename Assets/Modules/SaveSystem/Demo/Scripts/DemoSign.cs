using TMPro;
using UnityEngine;

namespace Saving.Demo
{
    /// <summary>
    /// Changes the text of a TextMeshPro sign: its Saveable saves the TextMeshPro component in the Selected mode with
    /// just two members picked in the Inspector, text and color — a component the game does not own.
    /// </summary>
    public sealed class DemoSign : MonoBehaviour
    {
        private static readonly string[] Words = { "Hello", "Checkpoint", "Treasure", "Danger!", "Saved?" };

        [SerializeField] private TMP_Text _text;

        /// <summary>For a button.</summary>
        public void Change()
        {
            if (_text == null)
                return;

            _text.text = Words[Random.Range(0, Words.Length)];
            _text.color = Color.HSVToRGB(Random.value, 0.5f, 1f);
        }
    }
}
