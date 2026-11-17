using System.Collections.Generic;

namespace AIKit.Tests
{
    /// <summary>
    /// Every action request of a character, as a test needs them (step 90, C22: the model asks its ActionState, it never
    /// calls the presentation): it listens to ActionState.Asked, so two requests in one step are both seen, and it is
    /// the character's view — it answers what it has (Knows: null — every action) and whether the request is still on
    /// screen (IsPlaying: true from a request it has until an End, or until the test says so; AlwaysShowing — a
    /// presentation still busy with what it showed before, whatever is asked).
    /// </summary>
    internal sealed class ActionLog : IActionView
    {
        /// <summary>The names asked for, in order.</summary>
        public readonly List<string> Played = new();

        /// <summary>Plain swings asked for.</summary>
        public int Swings;

        /// <summary>Ends at once (the work dropped).</summary>
        public int Drops;

        /// <summary>Gentle ends (the work put down).</summary>
        public int Endings;

        /// <summary>The actions it has; null — every one.</summary>
        public HashSet<string> Knows;

        /// <summary>A presentation that is always showing something, whatever is asked or ended.</summary>
        public bool AlwaysShowing;

        /// <summary>The last request is on screen; a test may end it (false), as a clip that has run out.</summary>
        public bool IsPlaying { get; set; }

        /// <summary>Becomes the view and the listener of this context's actions; known — the actions it has (none given: every one).</summary>
        public ActionLog(AgentContext context, params string[] known)
        {
            Knows = known is { Length: > 0 } ? new HashSet<string>(known) : null;
            context.Actions.View = this;
            context.Actions.Asked += Heard;
        }

        public bool Has(string action) => Knows == null || Knows.Contains(action);

        public bool IsShowing(int serial) => AlwaysShowing || IsPlaying;

        private void Heard(ActionState state)
        {
            switch (state.Current)
            {
                case ActionState.Kind.Action:
                    Played.Add(state.Name);
                    IsPlaying = Has(state.Name);
                    break;
                case ActionState.Kind.Swing:
                    Swings++;
                    break;
                default:
                    if (state.EndsGently)
                        Endings++;
                    else
                        Drops++;
                    IsPlaying = false;
                    break;
            }
        }
    }
}
