using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// All the configs of the game in one asset (GDD §14: catalogs are ScriptableObjects, the save keeps only ids):
    /// tanks, modules, skills, chapters. In the AppContext: <c>AppContext.Instance.GetGameCatalog()</c>. Lookups by id
    /// walk short lists; call them on events, not every frame.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Game Catalog", fileName = "GameCatalog")]
    public sealed class GameCatalog : ScriptableObject
    {
        [SerializeField] private List<TankConfig> _tanks = new();
        [SerializeField] private List<ModuleConfig> _modules = new();
        [SerializeField] private List<SkillConfig> _skills = new();
        [Tooltip("In the order they open")]
        [SerializeField] private List<ChapterConfig> _chapters = new();

        public IReadOnlyList<TankConfig> Tanks => _tanks;
        public IReadOnlyList<ModuleConfig> Modules => _modules;
        public IReadOnlyList<SkillConfig> Skills => _skills;
        public IReadOnlyList<ChapterConfig> Chapters => _chapters;

        public TankConfig FindTank(TankId id)
        {
            foreach (TankConfig tank in _tanks)
            {
                if (tank != null && tank.Id == id)
                    return tank;
            }

            return null;
        }

        public ModuleConfig FindModule(ModuleId id)
        {
            foreach (ModuleConfig module in _modules)
            {
                if (module != null && module.Id == id)
                    return module;
            }

            return null;
        }

        public SkillConfig FindSkill(SkillId id)
        {
            foreach (SkillConfig skill in _skills)
            {
                if (skill != null && skill.Id == id)
                    return skill;
            }

            return null;
        }

        public ChapterConfig FindChapter(ChapterId id)
        {
            foreach (ChapterConfig chapter in _chapters)
            {
                if (chapter != null && chapter.Id == id)
                    return chapter;
            }

            return null;
        }

        /// <summary>Setup for tests and the catalog builder.</summary>
        public void Configure(IEnumerable<TankConfig> tanks, IEnumerable<ModuleConfig> modules,
            IEnumerable<SkillConfig> skills, IEnumerable<ChapterConfig> chapters)
        {
            _tanks = new List<TankConfig>(tanks);
            _modules = new List<ModuleConfig>(modules);
            _skills = new List<SkillConfig>(skills);
            _chapters = new List<ChapterConfig>(chapters);
        }
    }
}
