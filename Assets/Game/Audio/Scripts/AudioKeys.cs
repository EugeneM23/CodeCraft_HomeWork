namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// Full ids of every sound of the game, "&lt;bank&gt;.&lt;key&gt;", and of the bank parameters.
    /// What each one is, its channel, 3D, loop and repeat limit: <see cref="AudioLayout"/>.
    /// The user drops clips into Assets/Game/Audio/Clips/&lt;bank&gt;/&lt;key&gt;/ (Docs/Audio/README.md).
    /// </summary>
    public static class AudioKeys
    {
        public static class Tank
        {
            public const string BANK = "tank";

            public const string ENGINE_START = "tank.engine_start";
            public const string ENGINE_LOOP = "tank.engine_loop";
            public const string ENGINE_STOP = "tank.engine_stop";
            public const string BOOST = "tank.boost";
            public const string TRACKS_LOOP = "tank.tracks_loop";
            public const string TRACKS_MUD_LOOP = "tank.tracks_mud_loop";
            public const string WHEEL_SPIN_LOOP = "tank.wheel_spin_loop";
            public const string TURRET_LOOP = "tank.turret_loop";
            public const string RAM = "tank.ram";
            public const string LANDING = "tank.landing";
            public const string PLOUGH = "tank.plough";
            public const string HIT = "tank.hit";
            public const string LOW_HEALTH = "tank.low_health";
            public const string DESTROYED = "tank.destroyed";

            // Parameters, all 0..1.
            public const string RPM = "tank.rpm";
            public const string THROTTLE = "tank.throttle";
            public const string SPEED = "tank.speed";
            public const string MUD = "tank.mud";
            public const string SLIP = "tank.slip";
            public const string TURRET = "tank.turret";
            /// <summary>How hard the nose dug in, set just before each plough sound.</summary>
            public const string PLOUGH_STRENGTH = "tank.plough_strength";
        }

        public static class Weapons
        {
            public const string BANK = "weapons";

            public const string CANNON_SHOT = "weapons.cannon_shot";
            public const string CANNON_RELOAD = "weapons.cannon_reload";
            public const string MG_SHOT = "weapons.mg_shot";
            public const string IMPACT_GROUND = "weapons.impact_ground";
            public const string IMPACT_VOXEL = "weapons.impact_voxel";
            public const string IMPACT_FLESH = "weapons.impact_flesh";
            public const string AIRSTRIKE_CALL = "weapons.airstrike_call";
            public const string AIRSTRIKE_WHISTLE = "weapons.airstrike_whistle";
        }

        public static class Destruction
        {
            public const string BANK = "destruction";

            public const string EXPLOSION_SMALL = "destruction.explosion_small";
            public const string EXPLOSION_LARGE = "destruction.explosion_large";
            public const string VOXEL_BREAK = "destruction.voxel_break";
            public const string VOXEL_FRACTURE = "destruction.voxel_fracture";
            public const string DEBRIS_LAND = "destruction.debris_land";
            public const string MUD_SPLASH = "destruction.mud_splash";
        }

        public static class Monsters
        {
            public const string BANK = "monsters";

            public const string GROWL = "monsters.growl";
            public const string ROAR = "monsters.roar";
            public const string STEP = "monsters.step";
            public const string ATTACK = "monsters.attack";
            public const string HIT = "monsters.hit";
            public const string DEATH = "monsters.death";
            public const string CRUSHED = "monsters.crushed";
            public const string GIBS = "monsters.gibs";
        }

        public static class Horde
        {
            public const string BANK = "horde";

            public const string CROWD_LOOP = "horde.crowd_loop";
            public const string WAVE_START = "horde.wave_start";

            public const string DENSITY = "horde.density";
        }

        public static class World
        {
            public const string BANK = "world";

            public const string WIND_LOOP = "world.wind_loop";
            public const string RAIN_LOOP = "world.rain_loop";
            public const string THUNDER = "world.thunder";

            public const string RAIN = "world.rain";
        }

        /// <summary>Keys of the UI sound catalog (UISoundPlayer), the same names as IUiSounds of the menu.</summary>
        public static class UI
        {
            public const string HOVER = "hover";
            public const string CLICK = "click";
            public const string BACK = "back";
            public const string OPEN = "open";
            public const string PURCHASE = "purchase";
            public const string ERROR = "error";
            public const string VICTORY = "victory";
            public const string DEFEAT = "defeat";
        }
    }
}
