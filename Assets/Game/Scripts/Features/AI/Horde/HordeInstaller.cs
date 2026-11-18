using Atomic.Elements;
using Atomic.Entities;
using Game.Horde;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// Makes a <see cref="MonoEntity"/> the level's horde: the horde keys of <see cref="AIAPI"/>,
    /// a <c>Damage</c> value (the source of the hits on the tank) and <see cref="HordeBehaviour"/>.
    /// </summary>
    public sealed class HordeInstaller : MonoEntityInstaller
    {
        [SerializeField, Tooltip("Baked art: Tools/Horde/Bake Regax VAT writes Assets/Game/Horde/Art/RegaxHordeArtConfig.")]
        private HordeArtConfig _art;

        [SerializeField]
        private HordeOptions _options = new();

        /// <summary>The behaviour made by the last <see cref="Install"/>.</summary>
        public HordeBehaviour Behaviour { get; private set; }

        public HordeOptions Options => _options;

        /// <summary>Sets the art and options before <see cref="Install"/> (for a horde made from code).</summary>
        public void Configure(HordeArtConfig art, HordeOptions options)
        {
            _art = art;
            _options = options ?? new HordeOptions();
        }

        public override void Install(IEntity entity)
        {
            if (_art == null || _art.mesh == null || _art.material == null)
            {
                Debug.LogError("HordeInstaller: no baked art (run Tools/Horde/Bake Regax VAT).", this);
                return;
            }

            entity.AddHordeTag();
            entity.AddHordeAlive(new ReactiveVariable<int>(0));
            entity.AddHordeKills(new ReactiveVariable<int>(0));
            entity.AddHordePopulation(new ReactiveVariable<int>(_options.population));
            entity.AddHordeMonsterKilled(new Event<Vector3>());
            entity.AddHordeAudio(GameHordeAudio.Instance);
            entity.AddDamage(new ReactiveVariable<int>(0));
            Behaviour = new HordeBehaviour(_art, _options);
            entity.AddBehaviour(Behaviour);
            entity.AddBehaviour(new HordeCombatBridge(Behaviour)); // combat: horde targets and deaths (temporary bridge)
        }
    }
}
