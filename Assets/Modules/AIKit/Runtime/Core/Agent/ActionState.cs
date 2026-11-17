namespace AIKit
{
    /// <summary>
    /// What the character is doing on the spot right now, as the character's own state (step 90, the user's "very
    /// important" C22; handbook §20.1 — the body writes its state, the presentation reads it and writes nothing back): an
    /// action by name (dig, a blow, a turn, a hit), the plain swing of an attack, or nothing. Nodes and runners ask for an
    /// action here; they never call the presentation. A presentation layer (AI Animator) reads Current / Name / Serial once
    /// a frame and shows it; its only way back is the answers of <see cref="IActionView"/>, an interface the model
    /// declares and the presentation implements. Without a view the character works the same: nothing is shown, and
    /// whoever asked waits by its own numbers.
    /// </summary>
    public sealed class ActionState
    {
        public enum Kind
        {
            /// <summary>Nothing on the spot (locomotion). After End: the ending, if asked for gently, may still play.</summary>
            None,
            /// <summary>An action by name (a state of the presentation of that name).</summary>
            Action,
            /// <summary>The plain swing of an attack (a move with no action of its own): the blow comes after its wind-up.</summary>
            Swing,
        }

        /// <summary>What is asked for now.</summary>
        public Kind Current { get; private set; }

        /// <summary>The action asked for (Kind.Action), else null.</summary>
        public string Name { get; private set; }

        /// <summary>
        /// Grows with every Begin, Swing and End: a reader sees each change once, even the same action asked twice in a
        /// row (a second blow of the same move).
        /// </summary>
        public int Serial { get; private set; }

        /// <summary>The last End asked to show the action's ending (put the work down); false — dropped at once.</summary>
        public bool EndsGently { get; private set; }

        /// <summary>The presentation's answers (AI Animator sets itself here when it wakes up); null — nothing shows.</summary>
        public IActionView View { get; set; }

        /// <summary>Something shows the character's actions (a presentation is its view).</summary>
        public bool HasView => View != null;

        /// <summary>
        /// Raised on every request (Begin, Swing, End), after the state has changed — for tests and debug tools that need
        /// every request, even two in one frame. The presentation does not listen: it reads the state once a frame.
        /// </summary>
        public event System.Action<ActionState> Asked;

        /// <summary>The name the plain swing of an attack is shown by (a presentation's action for Swing).</summary>
        public const string SWING = "Attack";

        /// <summary>The moment of a blow: the frame where it lands (handbook §20.3).</summary>
        public const string HIT = "Hit";

        // The moments the presentation told of the request on screen (cleared with every new request)
        private readonly System.Collections.Generic.List<string> _told = new();

        /// <summary>Asks for the action. A name the presentation does not know is not an error: nothing is shown.</summary>
        public void Begin(string action)
        {
            Serial++;
            Current = Kind.Action;
            Name = action;
            EndsGently = false;
            _told.Clear();
            Asked?.Invoke(this);
        }

        /// <summary>Asks for the plain swing of an attack.</summary>
        public void Swing()
        {
            Serial++;
            Current = Kind.Swing;
            Name = null;
            EndsGently = false;
            _told.Clear();
            Asked?.Invoke(this);
        }

        /// <summary>
        /// Ends the action: gently — the presentation shows its ending first (the work put down); atOnce — dropped (the
        /// character has to run).
        /// </summary>
        public void End(bool atOnce)
        {
            Serial++;
            Current = Kind.None;
            Name = null;
            EndsGently = !atOnce;
            _told.Clear();
            Asked?.Invoke(this);
        }

        /// <summary>
        /// The presentation tells a moment of the request with this serial — a frame of what it shows (handbook §20.3:
        /// "Hit" — the blow lands now). A moment of an older request is not heard. The model is told; it never asks the
        /// presentation to do anything.
        /// </summary>
        public void Tell(int serial, string moment)
        {
            if (serial != Serial || string.IsNullOrEmpty(moment) || _told.Contains(moment))
                return;
            _told.Add(moment);
        }

        /// <summary>The presentation told this moment of the request with this serial.</summary>
        public bool WasTold(int serial, string moment) => serial == Serial && _told.Contains(moment);

        /// <summary>
        /// The presentation will tell this moment of this action (the swing: SWING), so whoever asked may wait for it
        /// instead of its own numbers. No view, or a view that tells no moments — false.
        /// </summary>
        public bool WillTell(string action, string moment) =>
            View is IActionMoments moments && !string.IsNullOrEmpty(action) && moments.Tells(action, moment);

        /// <summary>
        /// The presentation can show this action (it has a state of that name). Ask it right after Begin to know whether
        /// to wait for the clip or by your own numbers; IsShown is for waiting.
        /// </summary>
        public bool CanShow(string action)
        {
            return View != null && !string.IsNullOrEmpty(action) && View.Has(action);
        }

        /// <summary>
        /// The last request is still on screen — the action, or its ending after a gentle End. A request the view has not
        /// read yet counts as shown; no view — false.
        /// </summary>
        public bool IsShown
        {
            get
            {
                return View != null && View.IsShowing(Serial);
            }
        }
    }

    /// <summary>
    /// The presentation's answers to the model (step 90, C22): the model declares what it may ask, the presentation (AI
    /// Animator) implements it. The model never tells the presentation anything — it publishes ActionState, the
    /// presentation reads it.
    /// </summary>
    public interface IActionView
    {
        /// <summary>The presentation has an action of this name (an animator state, a tagged clip).</summary>
        bool Has(string action);

        /// <summary>
        /// The request with this serial (ActionState.Serial) is still on screen — the action, or its ending after a gentle
        /// End. A serial the presentation has not read yet counts as on screen.
        /// </summary>
        bool IsShowing(int serial);
    }

    /// <summary>
    /// A presentation that tells the model the moments of its actions (ActionState.Tell — a frame of the clip: "Hit") says
    /// here which ones it will tell, so the model may wait for them instead of its own numbers (handbook §20.3). Optional
    /// next to IActionView: a view without it tells nothing.
    /// </summary>
    public interface IActionMoments
    {
        /// <summary>This moment of this action (the swing: ActionState.SWING) will be told.</summary>
        bool Tells(string action, string moment);
    }
}
