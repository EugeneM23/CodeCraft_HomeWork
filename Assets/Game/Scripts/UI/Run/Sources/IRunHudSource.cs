using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// What the battle UI (HUD, skill choice, pause, results) reads about the current run. The UI's own narrow
    /// view: today a stub over the sandbox, after the core's contracts an adapter over IRunService and the
    /// enemy-death event. Counters are read on Changed or a few times a second, never each frame.
    /// </summary>
    public interface IRunHudSource
    {
        /// <summary>A counter changed (kills, marks, level, arena, skills…): the HUD refreshes its texts.</summary>
        event Action Changed;

        /// <summary>
        /// A new level: the game is to stand while the player picks one of these (1–3 offers). May be raised from
        /// inside ChooseSkill when another level-up is pending. The list may be reused by the source: it always
        /// holds the current offer. Subscribe only to show the cards and call ChooseSkill: while anyone listens a
        /// level-up waits for the choice; with nobody listening the run takes a skill by itself.
        /// </summary>
        event Action<IReadOnlyList<SkillOffer>> SkillOffered;

        /// <summary>The run is over (victory, death, or the player left to the hangar).</summary>
        event Action<RunSummary> RunEnded;

        /// <summary>1-based arena of the run, and how many there are (5 in milestone 1).</summary>
        int ArenaIndex { get; }
        int ArenaCount { get; }

        /// <summary>The hints of the current arena (the HUD shows them for a while when the arena starts).</summary>
        ArenaHint ArenaHints { get; }

        int Level { get; }
        int Xp { get; }
        int XpToNextLevel { get; }

        int Marks { get; }
        int Samples { get; }

        int Kills { get; }
        int Crushes { get; }
        /// <summary>Crushes in the current series; 0 when none runs.</summary>
        int CrushStreak { get; }
        int ElitesKilled { get; }
        float ElapsedTime { get; }

        bool EliteAlive { get; }
        /// <summary>0..1, while EliteAlive.</summary>
        float EliteHealth01 { get; }

        /// <summary>The arena is cleared and its gate stands open at GatePosition (world).</summary>
        bool GateOpen { get; }
        Vector3 GatePosition { get; }

        /// <summary>The skills taken in this run, weapons first.</summary>
        IReadOnlyList<RunSkill> Skills { get; }

        /// <summary>The special module (E): 1 ready, 0..1 recharging; negative when the tank has none.</summary>
        float Special01 { get; }

        /// <summary>
        /// The offer waiting for a choice (raised before the UI subscribed, or not chosen yet); empty when none.
        /// </summary>
        IReadOnlyList<SkillOffer> PendingOffer { get; }

        /// <summary>The player picked SkillOffered[index]; the game goes on unless another level-up is offered.</summary>
        void ChooseSkill(int index);

        /// <summary>
        /// Leaves from the pause: ends the run as abandoned (RunEnded, keeping what was collected) AND goes to the
        /// hangar. The caller does not navigate itself.
        /// </summary>
        void ExitToHangar();
    }
}
