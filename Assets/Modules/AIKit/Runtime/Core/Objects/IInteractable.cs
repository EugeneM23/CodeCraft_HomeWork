using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// What an object of the world offers a character (handbook §24.1: the object advertises its purposes): to take it
    /// (a rock, a box, a pile hands one out), to use it (man a turret), to load it (fill a turret with the box in the
    /// hands), to occupy it (stand behind a cover, on a perch, at a place of the village).
    /// </summary>
    public enum Offer
    {
        Take,
        Use,
        Load,
        Occupy
    }

    /// <summary>
    /// Whose objects a search looks at (handbook §24, the context chain "the author's list → the zone → anything"):
    /// Mine — only the objects that name the character among their owners; Mine Or Public — the character's own, and
    /// only when it owns none of the kind, the public ones (nobody's); Anyone — its own first, then public, then
    /// anybody's. A character that owns objects of the kind never falls through to the others' (Mine, Mine Or Public):
    /// its own taken, it waits instead of going elsewhere.
    /// </summary>
    public enum Ownership
    {
        Mine,
        MineOrPublic,
        Anyone
    }

    /// <summary>
    /// An object of the world a character can interact with — a smart object (handbook §24.1): the object says what it
    /// offers to whom right now, where the character stands to use it and how near counts as there, and it owns the
    /// scenario of the interaction (what happens: a rock becomes the weapon in the hand, a box is carried, a cover is
    /// occupied). The graph never decides what an interaction does: its Interact block runs the object's scenario.
    /// The per-character state of a scenario lives in the Interact block's memory (an Interaction), never in the object
    /// — only what is the object's own (the rounds of a turret, who stands behind a cover) is.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>The object's own say now: it offers this to the user (a turret: Use while it has rounds and is free).</summary>
        bool Offers(Offer offer, AgentContext user);

        /// <summary>
        /// One user at a time goes for it (a rock, a turret, a cover): a search claims it, the others look for another.
        /// False — it serves everybody at once (a pile, a depot).
        /// </summary>
        bool OneAtATime(Offer offer);

        /// <summary>Where the user stands to interact (behind a cover — away from the threat; at a turret — its seat).</summary>
        Vector3 PlaceFor(Offer offer, AgentContext user, Transform threat);

        /// <summary>Metres from the place within which the user counts as there.</summary>
        float Reach { get; }

        /// <summary>The scenario starts for the user; false — it cannot now (taken in the last moment, nothing to load).</summary>
        bool Begin(Interaction interaction);

        /// <summary>One step of the scenario: Running, Success (done), Failure (it could not be done).</summary>
        TaskStatus Tick(Interaction interaction, float dt);

        /// <summary>
        /// The scenario is over: finished (Finished), or torn away (Interrupted — another decision, the graph stopped). An
        /// action asked for is ended, what was half done is undone.
        /// </summary>
        void End(Interaction interaction, ExitReason reason);
    }

    /// <summary>
    /// One character's interaction with an object, kept by the Interact block for that character (one per agent): what
    /// the scenario of the object needs to remember between its steps.
    /// </summary>
    public sealed class Interaction
    {
        /// <summary>The character interacting.</summary>
        public AgentContext User;

        public Offer Offer;

        /// <summary>Whom the user keeps an eye on (a cover is taken against it; the user faces it); null — nobody.</summary>
        public Transform Threat;

        /// <summary>Seconds since the scenario began.</summary>
        public float Time;

        /// <summary>The scenario's own step counter (0 at the start).</summary>
        public int Step;

        /// <summary>The scenario asked for an action of the body (it is ended when the scenario ends).</summary>
        public bool ShowsAction;

        /// <summary>A thing the scenario made or moved (the rock a pile handed out); null — none.</summary>
        public Transform Item;

        /// <summary>The block running the scenario holds the body's facing this step: the scenario may turn the user.</summary>
        public bool MayFace;

        internal void Reset(AgentContext user, Offer offer, Transform threat)
        {
            User = user;
            Offer = offer;
            Threat = threat;
            Time = 0f;
            Step = 0;
            ShowsAction = false;
            Item = null;
            MayFace = false;
        }
    }
}
