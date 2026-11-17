using UnityEngine;

namespace Saving.Tests
{
    /// <summary>A ScriptableObject saved as a whole by ObjectSerializer: Unity's field rules apply.</summary>
    public sealed class MembersAsset : ScriptableObject
    {
        public int Coins;
        [SerializeField] private string _title = "none";
        private int _hidden;

        public int Hidden
        {
            get => _hidden;
            set => _hidden = value;
        }

        public string Title
        {
            get => _title;
            set => _title = value;
        }
    }
}
