using System.Collections.Generic;

namespace AIKit.Tests
{
    /// <summary>
    /// The presentation as a test sees it (step 90, C22): it is the view of a character's actions (IActionView), reads
    /// the character's ActionState the way the AI Animator does — what was asked since it last looked — and answers
    /// what it has and whether the request is still on screen. Every question and every count reads the state first, so
    /// nothing asked between two steps is missed but two requests in one step (only the last one is shown, as by the
    /// AI Animator). A test says which actions it has (Knows: null — every one) and how long they last: Showing — the
    /// request is on screen until the test says otherwise; HasEnding — a gentle end shows its ending.
    /// </summary>
    internal sealed class CharacterActionsWatch : IActionView
    {
        private readonly ActionState _state;
        private readonly List<string> _played = new();
        private int _seen;
        private int _endings;
        private int _drops;
        private int _swings;
        private bool _showing;

        /// <summary>Becomes the view of this context's actions.</summary>
        public CharacterActionsWatch(AgentContext context)
        {
            _state = context.Actions;
            _seen = _state.Serial;
            _state.View = this;
        }

        /// <summary>The actions it has (null — every one): a request of another is not shown.</summary>
        public HashSet<string> Knows;

        /// <summary>A gentle end shows the action's ending, on screen until the test says otherwise.</summary>
        public bool HasEnding;

        /// <summary>The names asked for, in order.</summary>
        public List<string> Played
        {
            get
            {
                Read();
                return _played;
            }
        }

        /// <summary>Gentle ends (the work put down).</summary>
        public int Endings
        {
            get
            {
                Read();
                return _endings;
            }
        }

        /// <summary>Ends at once (the work dropped).</summary>
        public int Drops
        {
            get
            {
                Read();
                return _drops;
            }
        }

        /// <summary>Plain swings asked for.</summary>
        public int Swings
        {
            get
            {
                Read();
                return _swings;
            }
        }

        /// <summary>The last request is on screen; a test ends it (false) as a clip that has run out.</summary>
        public bool IsPlaying
        {
            get
            {
                Read();
                return _showing;
            }
            set
            {
                Read();
                _showing = value;
            }
        }

        public bool Has(string action) => Knows == null || Knows.Contains(action);

        public bool IsShowing(int serial)
        {
            Read();
            return serial >= _seen && _showing;
        }

        /// <summary>What was asked since the last look (only the last request, as the presentation sees it).</summary>
        public void Read()
        {
            if (_state.Serial == _seen)
                return;

            _seen = _state.Serial;
            switch (_state.Current)
            {
                case ActionState.Kind.Action:
                    _played.Add(_state.Name);
                    _showing = Has(_state.Name);
                    break;
                case ActionState.Kind.Swing:
                    _swings++;
                    _showing = false;
                    break;
                default:
                    if (_state.EndsGently)
                    {
                        _endings++;
                        _showing = _showing && HasEnding;
                    }
                    else
                    {
                        _drops++;
                        _showing = false;
                    }

                    break;
            }
        }
    }
}
