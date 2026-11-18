using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The arena the tank fights on: builds it, gives the AI director its burrows and bounds, opens
    /// the gate. Lives on the chapter scene; found as <c>GameContext.Instance.GetArena()</c>.
    /// </summary>
    public interface IArenaService
    {
        /// <summary>A new arena stands (old one gone): rebuild navigation, place what belongs on it.</summary>
        event Action<ArenaPlan> Built;

        /// <summary>A burrow tore the ground open at this point (for the sound and the horde).</summary>
        event Action<Vector3> BurrowOpened;

        /// <summary>The gate can now be broken through.</summary>
        event Action GateOpened;

        /// <summary>The tank went through the gate (the arena also tells the run: IRunService.MarkArenaLeft).</summary>
        event Action GatePassed;

        bool IsBuilt { get; }
        ArenaPlan Plan { get; }
        ArenaDefinition Definition { get; }

        /// <summary>The playable square: 40 × 40 m, from the ground up 20 m.</summary>
        Bounds Bounds { get; }

        /// <summary>Where the tank starts, on the ground.</summary>
        Vector3 Entry { get; }

        /// <summary>Burrow points round the edge, on the ground.</summary>
        IReadOnlyList<Vector3> BurrowPoints { get; }

        /// <summary>Spots behind houses where Spitters take cover; may be empty.</summary>
        IReadOnlyList<Vector3> CoverPoints { get; }

        bool IsGateOpen { get; }

        /// <summary>Middle of the gate wall, on the ground: the HUD points the way out at it.</summary>
        Vector3 GatePosition { get; }

        /// <summary>
        /// Takes down the arena that stands, builds this one (its definition found by id in the
        /// chapter) and puts the tank at its entry. Done in the call, no frames later. Heavy (tens of
        /// ms): the core calls it with the run's Seed + n while the shutter is closed.
        /// </summary>
        /// <returns>False if the chapter has no arena with this id.</returns>
        bool Build(ArenaId arena, int seed);

        /// <summary>A random burrow no nearer the tank than 15 m; false if every one is too near.</summary>
        bool TryPickBurrow(Vector3 tank, out int burrow);

        /// <summary>
        /// Tears the ground open at a burrow (once: an open burrow just gives its point again) and
        /// returns where the horde comes out.
        /// </summary>
        Vector3 OpenBurrow(int burrow);

        /// <summary>Picks a burrow at least 15 m from the tank and opens it: false if none is that far.</summary>
        bool TryOpenBurrow(Vector3 tank, out Vector3 point);

        /// <summary>
        /// The gate stops holding the tank back. Called by the arena itself on IRunService.ArenaCleared;
        /// public for scenes without a run (the sandbox, tests).
        /// </summary>
        void OpenGate();
    }
}
