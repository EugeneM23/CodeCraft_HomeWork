using UnityEngine;

namespace Saving.Tests
{
    /// <summary>
    /// A script that remembers it has set itself up, the way many do: a private flag next to what the setup found.
    /// The flag is not the game's state — brought back on a fresh object it would make the script skip its setup.
    /// </summary>
    public sealed class SetupFlagProbe : MonoBehaviour
    {
        public bool PublicInitialized;                  // public: a setting of the Inspector, saved as it is
        [SerializeField] private bool _serializedInit;  // serialized by Unity: the same

#pragma warning disable CS0414 // read through reflection only
        private bool isInitialized;
        private bool _isSetup;
        private bool m_Subscribed;
        private bool _armed;              // state of the game
        private float _initialSpeed = 2f; // not a flag: a value
#pragma warning restore CS0414

        public bool Initialized { get; private set; }
        public bool Ready { get; private set; }

        public bool SerializedInit
        {
            get => _serializedInit;
            set => _serializedInit = value;
        }

        public bool IsInitialized => isInitialized;
        public bool IsSetup => _isSetup;
        public bool Subscribed => m_Subscribed;
        public bool Armed => _armed;

        /// <summary>The script's own setup has run (or not) and the game has armed it (or not).</summary>
        public void Set(bool setUp, bool armed)
        {
            isInitialized = setUp;
            _isSetup = setUp;
            m_Subscribed = setUp;
            Initialized = setUp;
            _armed = armed;
            Ready = armed;
        }
    }
}
