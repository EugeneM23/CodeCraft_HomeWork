using UnityEngine;

namespace Game
{
    /// <summary>
    /// Which budget the game renders and simulates with: the desktop look, or the mobile one.
    /// </summary>
    /// <remarks>
    /// Phones are recognised by platform. The editor has no phone, so a quality level named
    /// <see cref="MOBILE_QUALITY_NAME"/> stands in for one there: switching to it runs the whole
    /// mobile path without a device build.
    ///
    /// The level name is looked up once per level change, not on every read:
    /// <see cref="QualitySettings.names"/> builds a new array on each access, and this property is
    /// read on spawn paths (every pooled instance the pools create).
    /// </remarks>
    public static class PerformanceTier
    {
        public const string MOBILE_QUALITY_NAME = "Mobile";

        private static int _cachedLevel = -1;
        private static bool _cachedIsMobileLevel;

        /// <summary>
        /// True in Android and iOS players, and wherever the active quality level is named
        /// <see cref="MOBILE_QUALITY_NAME"/>.
        /// </summary>
        public static bool IsMobile
        {
            get
            {
                if (Application.isMobilePlatform)
                    return true;

                int level = QualitySettings.GetQualityLevel();
                if (level != _cachedLevel)
                {
                    string[] names = QualitySettings.names;
                    _cachedIsMobileLevel = level >= 0 && level < names.Length && names[level] == MOBILE_QUALITY_NAME;
                    _cachedLevel = level;
                }

                return _cachedIsMobileLevel;
            }
        }
    }
}
