using System;
using UnityEngine;

namespace Game
{
    [Serializable]
    public sealed class GraphicsSettingsData
    {
        /// <summary>0 — keep the current resolution (first start).</summary>
        public int width;
        public int height;
        public float refreshRateHz;
        public FullScreenMode windowMode = FullScreenMode.FullScreenWindow;
        /// <summary>-1 — keep the project's default quality level.</summary>
        public int qualityLevel = -1;
        public bool vSync = true;
        /// <summary>0 — no limit.</summary>
        public int frameRateLimit;
    }
}
