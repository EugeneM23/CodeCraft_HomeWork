using System.Collections.Generic;

namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// The one table of the game's world sounds: which bank and key, which mixer channel, 2D or 3D, looped or
    /// not, how often it may repeat, which bank parameters exist. The editor sync (Tools/Audio/Sync Banks)
    /// builds the banks and event assets from it; the runtime reads the repeat limits. Adding a sound = a row
    /// here plus a constant in <see cref="AudioKeys"/>.
    /// </summary>
    public static class AudioLayout
    {
        public readonly struct Sound
        {
            public readonly string Id;          // "<bank>.<key>"
            public readonly AudioChannel Channel;
            public readonly bool Spatial;       // 3D at a point or following an object
            public readonly bool Loop;
            public readonly float MaxFrequency; // seconds between repeats, 0 = no limit
            public readonly float Volume;
            public readonly string DefaultClips; // clips used while the key folder is empty: paths under Assets/Game/Audio, ';'-separated, may be empty

            public Sound(string id, AudioChannel channel, bool spatial, bool loop = false, float maxFrequency = 0f,
                float volume = 1f, string defaultClips = "")
            {
                Id = id;
                Channel = channel;
                Spatial = spatial;
                Loop = loop;
                MaxFrequency = maxFrequency;
                Volume = volume;
                DefaultClips = defaultClips;
            }

            public string Bank => Id.Substring(0, Id.IndexOf('.'));
            public string Key => Id.Substring(Id.IndexOf('.') + 1);
        }

        public readonly struct Parameter
        {
            public readonly string Id;
            public readonly float Default;

            public Parameter(string id, float defaultValue = 0f)
            {
                Id = id;
                Default = defaultValue;
            }

            public string Bank => Id.Substring(0, Id.IndexOf('.'));
            public string Key => Id.Substring(Id.IndexOf('.') + 1);
        }

        public static readonly string[] Banks =
        {
            AudioKeys.Tank.BANK, AudioKeys.Weapons.BANK, AudioKeys.Destruction.BANK,
            AudioKeys.Monsters.BANK, AudioKeys.Horde.BANK, AudioKeys.World.BANK
        };

        private const AudioChannel FX = AudioChannel.Effects;
        private const AudioChannel AMB = AudioChannel.Ambient;

        private const string ZOMBIE_ATTACK = "Zombie/ZombieAttack1.wav;Zombie/ZombieAttack2.wav;Zombie/ZombieAttack3.wav;Zombie/ZombieAttack4.wav;Zombie/ZombieAttack6.wav;Zombie/ZombieAttack7.wav;Zombie/ZombieAttack10.wav";
        private const string ZOMBIE_HIT = "Zombie/ZombieTakeDamage1.wav;Zombie/ZombieTakeDamage2.wav;Zombie/ZombieTakeDamage3.wav;Zombie/ZombieTakeDamage4.wav;Zombie/ZombieTakeDamage7.wav;Zombie/ZombieTakeDamage8.wav;Zombie/ZombieTakeDamage10.wav;Zombie/ZombieTakeDamage11.wav;Zombie/ZombieTakeDamage12.wav;Zombie/ZombieTakeDamage13.wav";
        private const string ZOMBIE_DEATH = "Zombie/ZombieDeath1.wav;Zombie/ZombieDeath2.wav;Zombie/ZombieDeath3.wav;Zombie/ZombieDeath10.wav;Zombie/ZombieDeath12.wav;Zombie/ZombieDeath20.wav";
        private const string STEPS = "Character/Character (MoveStep1).wav;Character/Character (MoveStep2).wav;Character/Character (MoveStep3).wav;Character/Character (MoveStep4).wav";

        public static readonly Sound[] Sounds =
        {
            // Tank: follows the hull.
            new(AudioKeys.Tank.ENGINE_START, FX, true),
            new(AudioKeys.Tank.ENGINE_LOOP, FX, true, loop: true),
            new(AudioKeys.Tank.ENGINE_STOP, FX, true),
            new(AudioKeys.Tank.BOOST, FX, true, maxFrequency: 0.5f),
            new(AudioKeys.Tank.TRACKS_LOOP, FX, true, loop: true),
            new(AudioKeys.Tank.TRACKS_MUD_LOOP, FX, true, loop: true),
            new(AudioKeys.Tank.WHEEL_SPIN_LOOP, FX, true, loop: true),
            new(AudioKeys.Tank.TURRET_LOOP, FX, true, loop: true),
            new(AudioKeys.Tank.RAM, FX, true, maxFrequency: 0.15f, defaultClips: "Common/MeleeDamage.wav"),
            new(AudioKeys.Tank.LANDING, FX, true, maxFrequency: 0.3f, defaultClips: "Common/BodyFall2.wav"),
            new(AudioKeys.Tank.HIT, FX, true, maxFrequency: 0.1f, defaultClips: "Common/BulletDamage.wav"),
            new(AudioKeys.Tank.LOW_HEALTH, FX, false, defaultClips: "Common/Alarm4.wav"),
            new(AudioKeys.Tank.DESTROYED, FX, true, defaultClips: "etfx_explosion_magic.wav"),

            // Weapons.
            new(AudioKeys.Weapons.CANNON_SHOT, FX, true, defaultClips: "etfx_shoot_grenade.wav"),
            new(AudioKeys.Weapons.CANNON_RELOAD, FX, true),
            new(AudioKeys.Weapons.MG_SHOT, FX, true, maxFrequency: 0.05f, defaultClips: "Weapons/Fire (MachineGun).wav"),
            new(AudioKeys.Weapons.IMPACT_GROUND, FX, true, maxFrequency: 0.05f),
            new(AudioKeys.Weapons.IMPACT_VOXEL, FX, true, maxFrequency: 0.05f),
            new(AudioKeys.Weapons.IMPACT_FLESH, FX, true, maxFrequency: 0.05f, defaultClips: "Common/BulletDamage.wav"),
            new(AudioKeys.Weapons.AIRSTRIKE_CALL, FX, false, maxFrequency: 0.5f, defaultClips: "Common/Alarm.wav"),
            // Looped: the whistle lasts until GameSignalAudio stops it when the bomb explodes, not until the clip ends.
            new(AudioKeys.Weapons.AIRSTRIKE_WHISTLE, FX, true, loop: true),

            // Destruction.
            new(AudioKeys.Destruction.EXPLOSION_SMALL, FX, true, maxFrequency: 0.05f, defaultClips: "etfx_explosion_magic.wav"),
            new(AudioKeys.Destruction.EXPLOSION_LARGE, FX, true, defaultClips: "etfx_explosion_laser.wav"),
            new(AudioKeys.Destruction.VOXEL_BREAK, FX, true, maxFrequency: 0.05f),
            new(AudioKeys.Destruction.VOXEL_FRACTURE, FX, true, maxFrequency: 0.1f),
            new(AudioKeys.Destruction.DEBRIS_LAND, FX, true, maxFrequency: 0.08f),
            new(AudioKeys.Destruction.MUD_SPLASH, FX, true, maxFrequency: 0.05f),

            // Monsters (Regax).
            new(AudioKeys.Monsters.GROWL, FX, true, maxFrequency: 0.3f),
            new(AudioKeys.Monsters.ROAR, FX, true, maxFrequency: 0.5f, defaultClips: "Zombie/ZombieAttack1.wav"),
            new(AudioKeys.Monsters.STEP, FX, true, maxFrequency: 0.05f, volume: 0.6f, defaultClips: STEPS),
            new(AudioKeys.Monsters.ATTACK, FX, true, maxFrequency: 0.1f, defaultClips: ZOMBIE_ATTACK),
            new(AudioKeys.Monsters.HIT, FX, true, maxFrequency: 0.05f, defaultClips: ZOMBIE_HIT),
            new(AudioKeys.Monsters.DEATH, FX, true, maxFrequency: 0.05f, defaultClips: ZOMBIE_DEATH),
            new(AudioKeys.Monsters.CRUSHED, FX, true, maxFrequency: 0.05f, defaultClips: "Common/MeleeDamage.wav"),
            new(AudioKeys.Monsters.GIBS, FX, true, maxFrequency: 0.05f, defaultClips: "Common/BodyFall1.wav"),

            // Horde: stubs until the AI team brings the horde.
            new(AudioKeys.Horde.CROWD_LOOP, FX, false, loop: true),
            new(AudioKeys.Horde.WAVE_START, FX, false, defaultClips: "Common/Alarm3.wav"),

            // World ambience.
            new(AudioKeys.World.WIND_LOOP, AMB, false, loop: true),
            new(AudioKeys.World.RAIN_LOOP, AMB, false, loop: true),
            new(AudioKeys.World.THUNDER, AMB, false, maxFrequency: 5f),
        };

        public static readonly Parameter[] Parameters =
        {
            new(AudioKeys.Tank.RPM), new(AudioKeys.Tank.THROTTLE), new(AudioKeys.Tank.SPEED),
            new(AudioKeys.Tank.MUD), new(AudioKeys.Tank.SLIP), new(AudioKeys.Tank.TURRET),
            new(AudioKeys.Horde.DENSITY), new(AudioKeys.World.RAIN),
        };

        public static readonly string[] UISounds =
        {
            AudioKeys.UI.HOVER, AudioKeys.UI.CLICK, AudioKeys.UI.BACK, AudioKeys.UI.OPEN,
            AudioKeys.UI.PURCHASE, AudioKeys.UI.ERROR, AudioKeys.UI.VICTORY, AudioKeys.UI.DEFEAT
        };

        private static Dictionary<string, Sound> s_byId;

        public static bool TryGet(string id, out Sound sound)
        {
            if (s_byId == null)
            {
                s_byId = new Dictionary<string, Sound>(Sounds.Length);
                foreach (Sound s in Sounds)
                    s_byId[s.Id] = s;
            }

            return s_byId.TryGetValue(id, out sound);
        }

        /// <summary>Repeat limit of a sound, 0 when it has none or is unknown.</summary>
        public static float MaxFrequency(string id) => TryGet(id, out Sound sound) ? sound.MaxFrequency : 0f;
    }
}
