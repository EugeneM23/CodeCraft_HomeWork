namespace AIKit
{
    /// <summary>Who moves the body, by strength: a stronger claim takes an axis from a weaker one.</summary>
    public enum MoveAuthority
    {
        None,

        /// <summary>Small corrections (facing, separation).</summary>
        Steering,

        /// <summary>Following a path to a point.</summary>
        Path,

        /// <summary>Holding a distance band around a target.</summary>
        Strafe,

        /// <summary>Passing an edge that cannot be interrupted (a jump, a ladder).</summary>
        Traversal,

        /// <summary>An ability plays: nobody moves the body but the ability.</summary>
        Frozen
    }

    /// <summary>An axis of the body a writer claims.</summary>
    public enum BodyAxis
    {
        Move,
        Face
    }

    /// <summary>
    /// One owner per axis of the body (invariant 4, handbook §14.3): a writer claims an axis with its authority before
    /// it writes, checks every tick that the claim is still valid and writes only then; a stronger claim takes the axis
    /// from a weaker one (an equal one does not — the first keeps it), the loser simply stops writing. Release on exit.
    /// </summary>
    public sealed class MovementArbiter
    {
        private readonly object[] _owners = new object[2];
        private readonly MoveAuthority[] _authority = new MoveAuthority[2];

        public object OwnerOf(BodyAxis axis) => _owners[(int)axis];

        public MoveAuthority AuthorityOf(BodyAxis axis) => _owners[(int)axis] != null ? _authority[(int)axis] : MoveAuthority.None;

        /// <summary>Claims (or keeps) the axis; false — a claim at least as strong holds it.</summary>
        public bool TryClaim(object owner, BodyAxis axis, MoveAuthority authority)
        {
            if (owner == null)
                return false;

            int i = (int)axis;
            if (_owners[i] == owner)
            {
                _authority[i] = authority;
                return true;
            }

            if (_owners[i] != null && authority <= _authority[i])
                return false;

            _owners[i] = owner;
            _authority[i] = authority;
            return true;
        }

        /// <summary>Both axes at once (an ability freezing the body).</summary>
        public bool TryClaimBoth(object owner, MoveAuthority authority) =>
            TryClaim(owner, BodyAxis.Move, authority) & TryClaim(owner, BodyAxis.Face, authority);

        public bool IsValid(object owner, BodyAxis axis) => owner != null && _owners[(int)axis] == owner;

        /// <summary>Lets go of every axis the owner holds.</summary>
        public void Release(object owner)
        {
            for (int i = 0; i < _owners.Length; i++)
            {
                if (_owners[i] == owner)
                {
                    _owners[i] = null;
                    _authority[i] = MoveAuthority.None;
                }
            }
        }
    }
}
