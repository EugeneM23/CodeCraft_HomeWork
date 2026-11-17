using TMPro;
using UnityEngine;

namespace Saving.Demo
{
    /// <summary>
    /// Plain code saving a number without any setup: Values.Set / Values.Get on the save system in play. The value
    /// lives in the slot, so it is saved, loaded and reset by a new game together with everything else.
    /// </summary>
    public sealed class DemoCoins : MonoBehaviour
    {
        private const string KEY = "coins";

        [SerializeField] private TMP_Text _label;

        private static ISaveLoadService Saves => SaveLoadSystem.Current;

        public int Coins => Saves?.Values.Get(KEY, 0) ?? 0;

        /// <summary>For a button.</summary>
        public void AddCoin() => Saves?.Values.Set(KEY, Coins + 1);

        private void Update()
        {
            if (_label != null)
                _label.text = $"Coins: {Coins}";
        }
    }
}
