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
            /// <summary>The second and later crush of a streak, on top of monsters.crushed.</summary>
            public const string CRUSH_STREAK = "tank.crush_streak";
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
            /// <summary>Length of the crush streak, 0..1, set just before each crush_streak sound.</summary>
            public const string CRUSH_STREAK_LEVEL = "tank.crush_streak_level";
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
            /// <summary>A burrow opens: the ground tears and monsters climb out.</summary>
            public const string BURROW_OPEN = "monsters.burrow_open";
        }

        /// <summary>The run through the arenas: arena start and end, gates, experience. Level-up and skills are UI sounds: they play over the pause.</summary>
        public static class Run
        {
            public const string BANK = "run";

            public const string ARENA_START = "run.arena_start";
            public const string ARENA_CLEARED = "run.arena_cleared";
            public const string GATE_OPEN = "run.gate_open";
            /// <summary>Driving through the gate and the curtain to the next arena.</summary>
            public const string TRANSITION = "run.transition";
            public const string XP_PICKUP = "run.xp_pickup";
            public const string ELITE_APPEAR = "run.elite_appear";

            /// <summary>Length of the pickup streak, 0..1: raises the pickup's pitch a semitone a step.</summary>
            public const string XP_STREAK = "run.xp_streak";
        }

        /// <summary>The Crawler, the small fast horde monster.</summary>
        public static class Crawler
        {
            public const string BANK = "crawler";

            /// <summary>The pack running, one loop for all of them; its volume follows crawler.alive.</summary>
            public const string PACK_LOOP = "crawler.pack_loop";
            public const string BITE = "crawler.bite";
            public const string DEATH = "crawler.death";

            /// <summary>Live Crawlers against the arena's limit, 0..1.</summary>
            public const string ALIVE = "crawler.alive";
        }

        /// <summary>The Spitter, the horde monster that spits acid from range.</summary>
        public static class Spitter
        {
            public const string BANK = "spitter";

            public const string SPIT = "spitter.spit";
            public const string ACID_HIT = "spitter.acid_hit";
            public const string DEATH = "spitter.death";
        }

        /// <summary>The elite Regax of the fifth arena.</summary>
        public static class Elite
        {
            public const string BANK = "elite";

            public const string ROAR = "elite.roar";
            public const string STEP = "elite.step";
            public const string DASH = "elite.dash";
            public const string JUMP = "elite.jump";
            public const string LAND = "elite.land";
            public const string CLAWS = "elite.claws";
            /// <summary>Tears a piece out of a house.</summary>
            public const string TEAR = "elite.tear";
            public const string THROW = "elite.throw";
            public const string HURT = "elite.hurt";
            /// <summary>Finished off by the tank's crush at low health.</summary>
            public const string FINISHER = "elite.finisher";
            public const string DEATH = "elite.death";
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
            public const string CLOSE = "close";
            /// <summary>A module is put into a slot.</summary>
            public const string INSTALL = "install";
            public const string UPGRADE = "upgrade";
            /// <summary>One tick of the marks counting up on the results screen.</summary>
            public const string MARKS_TICK = "marks_tick";
            public const string SAMPLES_TICK = "samples_tick";
            public const string PAUSE_ON = "pause_on";
            public const string PAUSE_OFF = "pause_off";
            /// <summary>A new level: played as the skill choice opens over the paused battle.</summary>
            public const string LEVEL_UP = "level_up";
            public const string SKILL_CHOICE = "skill_choice";
            public const string SKILL_TAKEN = "skill_taken";
        }
    }
}
