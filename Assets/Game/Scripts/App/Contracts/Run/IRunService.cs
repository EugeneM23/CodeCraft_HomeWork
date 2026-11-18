using System;
using System.Collections.Generic;

namespace Game
{
    /// <summary>
    /// The run: a chapter's arenas one after another (GDD §3), its experience, level, skills and loot. One per level,
    /// in the GameContext (<c>GameContext.Instance.GetRun()</c>, RunAPI); it lives only as long as the level — nothing
    /// of it is saved except the summary at the end (GDD §9).
    ///
    /// Who calls what:
    /// <list type="bullet">
    /// <item>core — StartRun (on entering the level), the shutter and the lull timers, Victory after the last arena;</item>
    /// <item>arena team — MarkArenaReady (the arena is built), MarkGateOpened, MarkArenaLeft (the tank is through the gate);</item>
    /// <item>AI — MarkArenaCleared (the director's last wave is dead), deaths go to <see cref="IEnemyDeaths"/>;</item>
    /// <item>combat — AddExperience (an orb collected), EndRun(Defeat) when the tank dies;</item>
    /// <item>menu — ChooseSkill (1 of 3 cards), EndRun(Abandoned) from the pause.</item>
    /// </list>
    /// Calls that do not fit the state (MarkGateOpened in a fight, ChooseSkill without an offer) are ignored and return
    /// false. Events fire on the main thread, in the order of the list below.
    /// </summary>
    public interface IRunService
    {
        // ---------- Events ----------

        /// <summary>A run has begun; arena 1 follows at once.</summary>
        event Action RunStarted;

        /// <summary>
        /// Arena n (1-based) begins, behind the closed shutter (state Preparing): build it now, then MarkArenaReady.
        /// </summary>
        event Action<int> ArenaStarted;

        /// <summary>The lull is over: the waves may come (AI director starts here).</summary>
        event Action<int> FightStarted;

        /// <summary>The last enemy of arena n is dead: open the gate (arena team).</summary>
        event Action<int> ArenaCleared;

        event Action<int> GateOpened;

        /// <summary>The tank is through the gate of arena n: the orbs fly in, the shutter closes.</summary>
        event Action<int> ArenaLeft;

        /// <summary>The shutter is closing (true) or opening (false); the menu draws it, about 0.5 s each way.</summary>
        event Action<bool> ShutterChanged;

        /// <summary>A new level (the level number). An offer of skills is ready: the menu pauses and shows it.</summary>
        event Action<int> LevelUp;

        /// <summary>A skill was taken or levelled up (the skill, its new level 1..5).</summary>
        event Action<SkillId, int> SkillChanged;

        /// <summary>Marks or samples of the run changed (the HUD counters).</summary>
        event Action LootChanged;

        event Action<RunState> StateChanged;

        event Action<RunResult> RunEnded;

        // ---------- State ----------

        RunState State { get; }

        bool IsRunning { get; }

        ChapterId Chapter { get; }

        /// <summary>The current arena, 1-based; 0 before the run.</summary>
        int ArenaNumber { get; }

        /// <summary>Arenas in this run (5 in milestone 1, 20 in a full chapter).</summary>
        int ArenaCount { get; }

        ArenaId Arena { get; }

        /// <summary>The current arena is an elite arena (5, 10, 15) or the boss's.</summary>
        bool IsEliteArena { get; }

        /// <summary>
        /// Multiplier of enemy health and damage now: the chapter's (×1 / ×1.6 / ×2.4) times 1.06 per arena after the first.
        /// </summary>
        float EnemyStrength { get; }

        /// <summary>Seconds of play in this run, pauses not counted.</summary>
        float TimeSec { get; }

        /// <summary>The run's level, from 1.</summary>
        int Level { get; }

        /// <summary>Experience gathered towards the next level.</summary>
        int Experience { get; }

        /// <summary>Experience the current level needs: 10 + 12·(n − 1).</summary>
        int ExperienceToNextLevel { get; }

        /// <summary>Level-ups waiting for a choice (several orbs can bring two at once).</summary>
        int PendingLevelUps { get; }

        /// <summary>The 3 skills on offer (or fewer when little is left), empty with no level-up pending.</summary>
        IReadOnlyList<SkillId> SkillOffer { get; }

        /// <summary>Skills taken, in the order they were taken.</summary>
        IReadOnlyList<SkillId> Skills { get; }

        /// <summary>0 — not taken; 1..5 otherwise.</summary>
        int GetSkillLevel(SkillId skill);

        int Marks { get; }

        int Samples { get; }

        int Kills { get; }

        int Crushes { get; }

        int ElitesKilled { get; }

        /// <summary>The summary once Ended; default before.</summary>
        RunResult Result { get; }

        // ---------- Commands ----------

        /// <summary>Starts a run of the chapter with the given tank. Core calls it when the level is entered.</summary>
        bool StartRun(ChapterId chapter, TankId tank);

        /// <summary>Arena team: the arena is built and the tank is placed; the shutter opens. Without it the run waits up to 5 s.</summary>
        bool MarkArenaReady();

        /// <summary>AI director: every wave of the arena is dead.</summary>
        bool MarkArenaCleared();

        /// <summary>Arena team: the gate is open.</summary>
        bool MarkGateOpened();

        /// <summary>Arena team: the tank has driven through the gate.</summary>
        bool MarkArenaLeft();

        /// <summary>Combat: experience collected (an orb). Luck and other bonuses are applied by the caller.</summary>
        void AddExperience(int amount);

        /// <summary>Menu: the skill picked from <see cref="SkillOffer"/>; the next pending level-up, if any, offers again.</summary>
        bool ChooseSkill(SkillId skill);

        /// <summary>Combat: Defeat when the tank dies; menu: Abandoned from the pause. Victory is the core's own.</summary>
        bool EndRun(RunOutcome outcome);
    }
}
