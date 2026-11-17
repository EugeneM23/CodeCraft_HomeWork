using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>The shape a squad stands in: ranks facing one way, or rings round one target.</summary>
    public enum SquadShape
    {
        /// <summary>Ranks of Files each, the front rank first, all facing one way (handbook §20a.1: a line).</summary>
        Phalanx,

        /// <summary>Rings round a point — the front rank the inner ring, the next one behind it (a lone target surrounded).</summary>
        Ring
    }

    /// <summary>
    /// A squad: characters that march as one — the formation of an AI Squad (handbook §20a: a formation is slots round a
    /// point of its own; §21: the coordinator; §22.1: a role is the member's own behaviour plus what only the squad knows).
    /// The squad moves only the formation, never a body: its command graph (the AI Squad's) decides what the formation
    /// does and its blocks lead it — move the anchor, turn it, lay it out as ranks or rings; each member's own graph decides
    /// whether it keeps its slot (Follow Squad → Keep Formation) or does something more important of its own (kites,
    /// climbs, fetches a rock) and comes back. What the squad knows:
    /// - what it fights: everything its members see (their Brains hand it in), the focus the one nearest to the ranks,
    ///   kept for a while and given up only for a clearly nearer one (§21.2: no flicker between targets);
    /// - where each member stands: a slot of the formation (a goal to walk to, not a place it is put at, §20a.2) — the
    ///   ranks dealt by where the members stand, a fallen member's place taken by the one behind it (holes are filled
    ///   from the back, front first), the formation as fast as its members keep up (it slows while they lag);
    /// - who may strike: a turn at a foe for the front rank (the inner ring) — at most StrikersPerFoe at one foe at once
    ///   (§18: tokens, not everybody at once).
    /// The anchor is a point of the squad, not a member: the formation survives its losses. The squad reads nothing from
    /// the world but its banner and its members (invariant: a task does not scan the world).
    /// One step: Begin (members, focus) → the command graph thinks and its blocks lead → End (slots).
    /// </summary>
    public sealed class Squad
    {
        /// <summary>Seconds from the first member during which the ranks are dealt anew as members come.</summary>
        public const float FORMING = 1.5f;

        /// <summary>Seconds a member's sighting is kept after its Brain last handed one in.</summary>
        public const float SIGHT_MEMORY = 0.5f;

        /// <summary>Seconds the focus is kept at least, and how many times nearer a new one must be to take it (§21.2).</summary>
        public const float FOCUS_HOLD = 2f;
        public const float FOCUS_BETTER = 1.5f;

        /// <summary>A turn wider than this many degrees is an about-face: the rear rank becomes the front one.</summary>
        public const float ABOUT_FACE = 120f;

        /// <summary>Metres off its slot within which a member counts as standing in the ranks.</summary>
        public const float IN_PLACE = 1.5f;

        /// <summary>Seconds a turn to strike is held at most (a blow takes less): one not given back is dropped.</summary>
        public const float STRIKE_TURN = 4f;

        /// <summary>Seconds after its blow a member waits before its next turn — the others at the same foe get theirs.</summary>
        public const float STRIKE_REST = 0.6f;

        /// <summary>Seconds the squad counts as led after a block of its command graph last led the formation.</summary>
        public const float LED_FOR = 0.5f;

        private sealed class Member
        {
            public AgentContext Context;
            public int Slot = -1;
            public int Ring;
            public float Angle;
            public Vector3 Position;
            public Transform Seen;
            public float SeenAt = float.NegativeInfinity;
            public Vector3 Point;
            public bool Front;
            public Transform StrikeFoe;
            public float StrikeSince = -1f;
            public float RestUntil;
            public float Local;                  // dealing: its place along a sorting axis
        }

        private readonly List<Member> _members = new();
        private readonly List<Transform> _foes = new();
        private readonly List<Member> _sorted = new();
        private readonly HashSet<int> _held = new();
        private readonly List<SquadDecisionTask> _leftDecisions = new();     // when each decision was last left
        private readonly List<float> _leftAt = new();

        private bool _placed;
        private bool _redeal = true;
        private bool _joined;
        private bool _left;
        private float _bornAt = -1f;
        private float _focusAt;
        private float _ledAt = float.NegativeInfinity;
        private Member _middle;

        public Squad(Transform banner)
        {
            Banner = banner;
        }

        /// <summary>The squad's object (its AI Squad): where the squad forms up and which way.</summary>
        public Transform Banner { get; }

        /// <summary>The squad's clock: the sum of its steps.</summary>
        public float Now { get; private set; }

        public int Count => _members.Count;

        public SquadShape Shape { get; private set; } = SquadShape.Phalanx;

        /// <summary>The phalanx: the middle of its front rank. The rings: their centre.</summary>
        public Vector3 Anchor { get; private set; }

        /// <summary>Which way the phalanx faces (flat, unit).</summary>
        public Vector3 Facing { get; private set; } = Vector3.forward;

        /// <summary>How the anchor moves now, metres per second (members' slots move with it).</summary>
        public Vector3 Velocity { get; private set; }

        /// <summary>The radius of the inner ring (the front rank's) while the squad stands in rings.</summary>
        public float RingRadius { get; private set; }

        /// <summary>
        /// The member who stands in the middle of the rings (Form Ring's Middle: the mayor at a meeting); null — nobody,
        /// every member is on a ring.
        /// </summary>
        public Transform Middle => _middle != null ? TransformOf(_middle.Context) : null;

        /// <summary>The member in the middle stands on its place (no one in the middle — true).</summary>
        public bool MiddleInPlace => _middle == null || Flat(_middle.Point - _middle.Position).magnitude <= IN_PLACE;

        /// <summary>The foe the squad goes for (its focus); null — it sees nobody.</summary>
        public Transform Target { get; private set; }

        /// <summary>Everything the members see now (each foe once).</summary>
        public IReadOnlyList<Transform> Foes => _foes;

        /// <summary>The part of the members within IN_PLACE of their slots (1 — all in the ranks).</summary>
        public float Cohesion { get; private set; } = 1f;

        /// <summary>A block of the command graph leads the formation now: the members have a formation to keep.</summary>
        public bool IsLed => Now - _ledAt <= LED_FOR;

        // The formation's numbers: the Squad Brain of the command graph sets them every think (Configure)
        public int Files { get; private set; } = 10;

        public float Spacing { get; private set; } = 1.8f;

        public float RankGap { get; private set; } = 2f;

        public int StrikersPerFoe { get; private set; } = 3;

        /// <summary>How many ranks the members fill (the last one may be short).</summary>
        public int Ranks => _members.Count == 0 ? 0 : (_members.Count + Files - 1) / Files;

        /// <summary>From the front rank to the last one, metres.</summary>
        public float Depth => Mathf.Max(0, Ranks - 1) * RankGap;

        /// <summary>The middle of the formation: of the phalanx (between its ranks) or of the rings.</summary>
        public Vector3 Centre => Shape == SquadShape.Ring ? Anchor : Anchor - Facing * (Depth * 0.5f);

        /// <summary>
        /// How fast the formation may move, as a part of a block's speed: full while the members stand in the ranks,
        /// slower as they lag behind (the speed of the slowest, §20a.2), never quite stopped (a member that cannot come
        /// — or that is busy with something of its own — must not freeze the squad).
        /// </summary>
        public float PaceFactor => Mathf.Lerp(0.2f, 1f, Mathf.InverseLerp(0.4f, 0.85f, Cohesion));

        /// <summary>To the right of the phalanx's facing.</summary>
        public Vector3 Right => new(Facing.z, 0f, -Facing.x);

        /// <summary>The formation's numbers (the Squad Brain's settings): a rank, the gaps, the strikers at one foe.</summary>
        public void Configure(int files, float spacing, float rankGap, int strikersPerFoe)
        {
            files = Mathf.Max(1, files);
            if (files != Files)
                _redeal = true;
            Files = files;
            Spacing = Mathf.Max(0.5f, spacing);
            RankGap = Mathf.Max(0.5f, rankGap);
            StrikersPerFoe = Mathf.Max(1, strikersPerFoe);
        }

        // ---------- the members ----------

        /// <summary>The character is a member of the squad (its AI Squad lists it, and it is awake).</summary>
        public void Join(AgentContext context)
        {
            if (context == null)
                return;

            if (Find(context) == null)
            {
                _members.Add(new Member { Context = context, Position = context.Position });
                _joined = true;
                if (_bornAt < 0f)
                    _bornAt = Now;
            }

            context.Squad = this;
        }

        /// <summary>The member leaves the squad (it fell, it was switched off): the one behind it takes its slot.</summary>
        public void Leave(AgentContext context)
        {
            Member member = Find(context);
            if (member == null)
                return;

            _members.Remove(member);
            if (member == _middle)
                _middle = null;
            _left = true;
            if (context.Squad == this)
                context.Squad = null;
        }

        /// <summary>
        /// Every member whose character is gone (destroyed) or is not in the list any more leaves (the AI Squad's list is
        /// who marches: change it freely) — its slot goes to the one behind it.
        /// </summary>
        public void KeepOnly(List<AICharacter> listed)
        {
            for (int i = _members.Count - 1; i >= 0; i--)
            {
                AgentContext context = _members[i].Context;
                AICharacter character = context.Agent;
                if (character == null || listed == null || !listed.Contains(character))
                    Leave(context);
            }
        }

        /// <summary>Every member leaves (the squad is switched off).</summary>
        public void LeaveAll()
        {
            for (int i = _members.Count - 1; i >= 0; i--)
                Leave(_members[i].Context);
        }

        /// <summary>A member's Brain hands in what it sees (its Target pin) once per think.</summary>
        public void Saw(AgentContext context, Transform seen)
        {
            Member member = Find(context);
            if (member == null)
                return;

            member.Seen = seen;
            member.SeenAt = Now;
        }

        /// <summary>The agent is a member now.</summary>
        public bool Has(AgentContext context) => Find(context) != null;

        /// <summary>
        /// The member's slot this step: where to stand, which way to look and how the slot moves; front — it stands in
        /// the front rank (the inner ring). False — it is no member, or the ranks are not dealt yet.
        /// </summary>
        public bool SlotOf(AgentContext context, out Vector3 point, out Vector3 look, out Vector3 velocity, out bool front)
        {
            Member member = Find(context);
            if (member == null || member.Slot < 0)
            {
                point = look = velocity = Vector3.zero;
                front = false;
                return false;
            }

            point = member.Point;
            look = member == _middle ? FlatUnit(Banner != null ? Banner.forward : Facing, Facing)
                : Shape == SquadShape.Ring ? Flat(Anchor - member.Point) : Facing;
            if (look.sqrMagnitude < 1e-6f)
                look = Facing;
            look.Normalize();
            velocity = Velocity;
            front = member.Front;
            return true;
        }

        /// <summary>The member's slot index (rank × Files + file); -1 — none.</summary>
        public int SlotIndexOf(AgentContext context) => Find(context)?.Slot ?? -1;

        /// <summary>
        /// The member asks for its turn to strike the foe: it has one already, or fewer than StrikersPerFoe of the squad
        /// strike that foe now — not right after its own blow. frontOnly — only a member of the front rank (the inner ring).
        /// </summary>
        public bool MayStrike(AgentContext context, Transform foe, bool frontOnly = true)
        {
            Member member = Find(context);
            if (member == null || foe == null || frontOnly && !member.Front)
                return false;

            if (member.StrikeSince >= 0f && member.StrikeFoe == foe)
                return true;

            if (Now < member.RestUntil)
                return false;

            if (StrikersAt(foe) >= StrikersPerFoe)
                return false;

            member.StrikeFoe = foe;
            member.StrikeSince = Now;
            return true;
        }

        /// <summary>The member's blow is over (it is back in the ranks): its turn goes to the next one.</summary>
        public void EndStrike(AgentContext context)
        {
            Member member = Find(context);
            if (member == null || member.StrikeSince < 0f)
                return;

            member.StrikeFoe = null;
            member.StrikeSince = -1f;
            member.RestUntil = Now + STRIKE_REST;
        }

        /// <summary>How many members of the squad have a turn to strike this foe now.</summary>
        public int StrikersAt(Transform foe)
        {
            int count = 0;
            for (int i = 0; i < _members.Count; i++)
            {
                if (foe != null && _members[i].StrikeSince >= 0f && _members[i].StrikeFoe == foe)
                    count++;
            }

            return count;
        }

        // The member whose character is this object (null — none, or no object)
        private Member Find(Transform member)
        {
            if (member == null)
                return null;

            for (int i = 0; i < _members.Count; i++)
            {
                if (TransformOf(_members[i].Context) == member)
                    return _members[i];
            }

            return null;
        }

        private static Transform TransformOf(AgentContext context) =>
            context?.Agent != null ? context.Agent.EntityRoot : context?.Self;

        private Member Find(AgentContext context)
        {
            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i].Context == context)
                    return _members[i];
            }

            return null;
        }

        // ---------- the step ----------

        /// <summary>The step begins: the clock, where the members stand, what they see, the focus.</summary>
        public void Begin(float dt)
        {
            Now += dt;
            Velocity = Vector3.zero;
            for (int i = 0; i < _members.Count; i++)
                _members[i].Position = _members[i].Context.Position;

            if (!_placed && Banner != null)
            {
                _placed = true;
                Anchor = Banner.position;
                Facing = FlatUnit(Banner.forward, Vector3.forward);
            }

            ChooseFocus();
        }

        /// <summary>The step ends: the slots are dealt and placed where the blocks led the formation, turns expire.</summary>
        public void End()
        {
            Deal();
            Place();
            ExpireTurns();
        }

        // What the members see, and the focus: kept while somebody sees it, given up for one clearly nearer to the ranks
        private void ChooseFocus()
        {
            _foes.Clear();
            for (int i = 0; i < _members.Count; i++)
            {
                Transform seen = _members[i].Seen;
                if (seen != null && Now - _members[i].SeenAt <= SIGHT_MEMORY && seen.gameObject.activeInHierarchy &&
                    !_foes.Contains(seen))
                    _foes.Add(seen);
            }

            if (Target != null && !_foes.Contains(Target))
                Target = null;

            Vector3 centre = Centre;
            Transform nearest = null;
            float best = float.MaxValue;
            for (int i = 0; i < _foes.Count; i++)
            {
                float distance = Flat(_foes[i].position - centre).magnitude;
                if (distance < best)
                {
                    best = distance;
                    nearest = _foes[i];
                }
            }

            if (Target == null)
            {
                Target = nearest;
                _focusAt = Now;
                return;
            }

            if (nearest != Target && Now - _focusAt >= FOCUS_HOLD &&
                best * FOCUS_BETTER < Flat(Target.position - centre).magnitude)
            {
                Target = nearest;
                _focusAt = Now;
            }
        }

        // ---------- what the command graph's blocks do with the formation ----------

        /// <summary>A block of the command graph leads the formation this step (the members have something to keep).</summary>
        public void Led() => _ledAt = Now;

        /// <summary>
        /// The command graph's decision stopped being in charge (its "again after" counts from now). Each decision keeps its
        /// own time: another one leaving meanwhile does not wipe it.
        /// </summary>
        public void MarkLeft(SquadDecisionTask decision)
        {
            if (decision == null)
                return;

            int at = _leftDecisions.IndexOf(decision);
            if (at < 0)
            {
                _leftDecisions.Add(decision);
                _leftAt.Add(Now);
            }
            else
            {
                _leftAt[at] = Now;
            }
        }

        /// <summary>Seconds since the squad last left this decision (infinity — it never has).</summary>
        public float SinceLeft(SquadDecisionTask decision)
        {
            int at = decision != null ? _leftDecisions.IndexOf(decision) : -1;
            return at >= 0 ? Now - _leftAt[at] : float.PositiveInfinity;
        }

        /// <summary>
        /// The squad stands in ranks (it does already — nothing changes). From rings: the phalanx forms round the middle of
        /// the members, facing the target (if any), and the ranks are dealt by where the members stand.
        /// </summary>
        public void FormPhalanx(Transform facing = null)
        {
            if (Shape == SquadShape.Phalanx)
                return;

            Vector3 middle = MembersMiddle();
            Shape = SquadShape.Phalanx;
            _middle = null;
            Facing = facing != null ? FlatUnit(facing.position - middle, Facing) : Facing;
            Anchor = middle + Facing * (Depth * 0.5f);
            _redeal = true;
        }

        /// <summary>
        /// The squad stands in rings round the centre: the front rank (the members nearest to it) the inner ring of the
        /// radius, each rank behind one RankGap farther out. middle — a member who stands in the middle instead, facing
        /// the way the banner faces (null — nobody). Already in rings — only the radius (and who is in the middle) changes.
        /// </summary>
        public void FormRing(Vector3 centre, float radius, Transform middle = null)
        {
            Member inMiddle = Find(middle);
            if (inMiddle != _middle)
            {
                _middle = inMiddle;
                _redeal = true;
            }

            int onRings = _members.Count - (_middle != null ? 1 : 0);
            RingRadius = Mathf.Max(radius, Mathf.Min(Files, onRings) * Spacing / (2f * Mathf.PI));
            if (Shape == SquadShape.Ring)
                return;

            Shape = SquadShape.Ring;
            Anchor = new Vector3(centre.x, Anchor.y, centre.z);
            _redeal = true;
        }

        /// <summary>
        /// The phalanx turns toward the direction, at most degreesPerSecond, round its middle (the ranks wheel, they do not
        /// swing round the front). A turn wider than ABOUT_FACE is an about-face: the phalanx faces back at once and its
        /// last rank becomes the front one.
        /// </summary>
        public void Turn(Vector3 toward, float degreesPerSecond, float dt)
        {
            if (Shape != SquadShape.Phalanx)
                return;

            Vector3 want = Flat(toward);
            if (want.sqrMagnitude < 1e-4f)
                return;

            Vector3 centre = Centre;
            float angle = Vector3.SignedAngle(Facing, want, Vector3.up);
            if (Mathf.Abs(angle) > ABOUT_FACE)
            {
                Facing = -Facing;
                Anchor = centre + Facing * (Depth * 0.5f);
                _redeal = true;
                angle = Vector3.SignedAngle(Facing, want, Vector3.up);
            }

            float step = Mathf.Clamp(angle, -degreesPerSecond * dt, degreesPerSecond * dt);
            Facing = FlatUnit(Quaternion.AngleAxis(step, Vector3.up) * Facing, Facing);
            Anchor = centre + Facing * (Depth * 0.5f);
        }

        /// <summary>The anchor (the front's middle, the rings' centre) goes toward the point, at most speed metres per second.</summary>
        public void MoveAnchorTo(Vector3 point, float speed, float dt)
        {
            Vector3 offset = Flat(point - Anchor);
            float distance = offset.magnitude;
            if (distance < 1e-3f || speed <= 0f || dt <= 0f)
                return;

            float step = Mathf.Min(distance, speed * dt);
            Vector3 move = offset / distance * step;
            Anchor += move;
            Velocity = move / dt;
        }

        /// <summary>How far out of the outermost ring the point is (negative — inside it); the phalanx — out of its middle.</summary>
        public float OutOfRings(Vector3 point)
        {
            float outer = Shape == SquadShape.Ring ? RingRadius + Mathf.Max(0, Ranks - 1) * RankGap : 0f;
            return Flat(point - Centre).magnitude - outer;
        }

        /// <summary>How far ahead of the front rank the point is (negative — behind it), along the facing.</summary>
        public float AheadOfFront(Vector3 point) => Vector3.Dot(Flat(point - Anchor), Facing);

        /// <summary>How far the target is from the nearest member (flat) — "how near the squad it is".</summary>
        public float DistanceTo(Transform target)
        {
            if (target == null)
                return float.MaxValue;

            float best = float.MaxValue;
            for (int i = 0; i < _members.Count; i++)
                best = Mathf.Min(best, Flat(target.position - _members[i].Position).magnitude);
            return best;
        }

        /// <summary>How many of the foes the members see stand within radius of the target (the target included).</summary>
        public int FoesNear(Transform target, float radius)
        {
            if (target == null)
                return 0;

            int count = 0;
            for (int i = 0; i < _foes.Count; i++)
            {
                if (Flat(_foes[i].position - target.position).magnitude <= radius)
                    count++;
            }

            return count;
        }

        /// <summary>The middle of the foes within radius of the target.</summary>
        public Vector3 FoesMiddle(Transform target, float radius)
        {
            if (target == null)
                return Anchor;

            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < _foes.Count; i++)
            {
                if (Flat(_foes[i].position - target.position).magnitude > radius)
                    continue;

                sum += _foes[i].position;
                count++;
            }

            return count > 0 ? sum / count : target.position;
        }

        /// <summary>
        /// The nearest foe ahead of the front rank (within the phalanx's width and a spacing either side, no farther than
        /// within): what the front walks into. Null — none.
        /// </summary>
        public Transform NearestAhead(float within)
        {
            float halfWidth = (Mathf.Min(Files, _members.Count) - 1) * 0.5f * Spacing + Spacing;
            Transform nearest = null;
            float best = within;
            for (int i = 0; i < _foes.Count; i++)
            {
                Vector3 offset = Flat(_foes[i].position - Anchor);
                float ahead = Vector3.Dot(offset, Facing);
                float aside = Mathf.Abs(Vector3.Dot(offset, Right));
                if (ahead < -RankGap || aside > halfWidth || ahead > best)
                    continue;

                best = ahead;
                nearest = _foes[i];
            }

            return nearest;
        }

        // ---------- slots ----------

        private void Deal()
        {
            bool forming = _bornAt >= 0f && Now - _bornAt < FORMING;
            if (Shape == SquadShape.Ring)
            {
                if (_redeal || _joined || _left)
                    DealRings();
            }
            else if (_redeal || forming && _joined)
            {
                DealRanks();
            }
            else if (_joined || _left)
            {
                FillHoles();
            }

            _redeal = _joined = _left = false;
        }

        // The ranks dealt by where the members stand: the foremost Files of them (along the facing) the front rank, left
        // to right, and so on back — nobody crosses another to get to its place
        private void DealRanks()
        {
            _sorted.Clear();
            _sorted.AddRange(_members);
            for (int i = 0; i < _sorted.Count; i++)
                _sorted[i].Local = -Vector3.Dot(Flat(_sorted[i].Position - Anchor), Facing);
            _sorted.Sort((a, b) => a.Local.CompareTo(b.Local));

            int files = Files;
            for (int start = 0; start < _sorted.Count; start += files)
            {
                int count = Mathf.Min(files, _sorted.Count - start);
                List<Member> rank = _sorted.GetRange(start, count);
                for (int i = 0; i < rank.Count; i++)
                    rank[i].Local = Vector3.Dot(Flat(rank[i].Position - Anchor), Right);
                rank.Sort((a, b) => a.Local.CompareTo(b.Local));
                for (int i = 0; i < rank.Count; i++)
                    rank[i].Slot = start + i;
            }

            _sorted.Clear();
        }

        // A member gone: the one behind its place steps up (the nearest file of the nearest rank behind), the hole goes
        // back rank by rank to the last one, which closes up (handbook §20a.2: slots reassigned by priority — a hole in
        // the middle of the front reads as a bug). A newcomer takes the place left at the back.
        private void FillHoles()
        {
            int count = _members.Count;
            int files = Files;
            for (int guard = 0; guard < count * 2 + 4; guard++)
            {
                _held.Clear();
                for (int i = 0; i < _members.Count; i++)
                {
                    if (_members[i].Slot >= 0 && _members[i].Slot < count)
                        _held.Add(_members[i].Slot);
                }

                int hole = -1;
                for (int slot = 0; slot < count && hole < 0; slot++)
                {
                    if (!_held.Contains(slot))
                        hole = slot;
                }

                if (hole < 0)
                    return;

                int holeRank = hole / files;
                int holeFile = hole % files;
                Member best = null;
                float bestScore = float.MaxValue;
                for (int i = 0; i < _members.Count; i++)
                {
                    Member member = _members[i];
                    if (member.Slot >= 0 && member.Slot < count && member.Slot <= hole)
                        continue;           // in front of the hole or beside it, on its own place

                    float score;
                    if (member.Slot < 0)
                    {
                        score = 100000f;    // a newcomer: only when nobody of the squad can step up
                    }
                    else
                    {
                        int rank = member.Slot / files;
                        int file = member.Slot % files;
                        score = rank == holeRank ? 50000f + file : (rank - holeRank) * 1000f + Mathf.Abs(file - holeFile);
                    }

                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = member;
                    }
                }

                if (best == null)
                    return;

                best.Slot = hole;
            }
        }

        // The rings: the members nearest to the centre the inner ring (as many as a rank), the rest the next rings out; on
        // each ring the places are spread evenly, in the order the members stand round the centre, turned to fit where
        // they are — nobody crosses another, and everybody walks the least
        private void DealRings()
        {
            _sorted.Clear();
            _sorted.AddRange(_members);
            if (_middle != null)
            {
                _sorted.Remove(_middle);
                _middle.Ring = -1;                  // not on a ring: in the middle
                _middle.Slot = _members.Count - 1;
            }

            for (int i = 0; i < _sorted.Count; i++)
                _sorted[i].Local = Flat(_sorted[i].Position - Anchor).sqrMagnitude;
            _sorted.Sort((a, b) => a.Local.CompareTo(b.Local));

            int files = Files;
            for (int start = 0, ring = 0; start < _sorted.Count; start += files, ring++)
            {
                int count = Mathf.Min(files, _sorted.Count - start);
                List<Member> members = _sorted.GetRange(start, count);
                for (int i = 0; i < members.Count; i++)
                    members[i].Local = Bearing(members[i].Position - Anchor);
                members.Sort((a, b) => a.Local.CompareTo(b.Local));

                float gap = 360f / count;
                float sin = 0f;
                float cos = 0f;
                for (int i = 0; i < members.Count; i++)
                {
                    float offset = (members[i].Local - i * gap) * Mathf.Deg2Rad;
                    sin += Mathf.Sin(offset);
                    cos += Mathf.Cos(offset);
                }

                float turn = Mathf.Atan2(sin, cos) * Mathf.Rad2Deg;
                for (int i = 0; i < members.Count; i++)
                {
                    members[i].Ring = ring;
                    members[i].Angle = turn + i * gap;
                    members[i].Slot = start + i;
                }
            }

            _sorted.Clear();
        }

        // Every member's slot in the world this step, and how many stand in it
        private void Place()
        {
            int inPlace = 0;
            int count = _members.Count;
            for (int i = 0; i < count; i++)
            {
                Member member = _members[i];
                if (Shape == SquadShape.Ring && member == _middle)
                {
                    member.Point = Anchor;
                    member.Front = false;
                }
                else if (Shape == SquadShape.Ring)
                {
                    float radius = RingRadius + member.Ring * RankGap;
                    member.Point = Anchor + Quaternion.AngleAxis(member.Angle, Vector3.up) * Vector3.forward * radius;
                    member.Front = member.Ring == 0;
                }
                else if (member.Slot >= 0)
                {
                    Vector2 offset = PhalanxOffset(member.Slot, count, Files, Spacing, RankGap);
                    member.Point = Anchor + Right * offset.x + Facing * offset.y;
                    member.Front = member.Slot < Files;
                }
                else
                {
                    member.Point = member.Position;
                    member.Front = false;
                }

                member.Point.y = Anchor.y;
                if (Flat(member.Point - member.Position).magnitude <= IN_PLACE)
                    inPlace++;
            }

            Cohesion = count > 0 ? inPlace / (float)count : 1f;
        }

        private void ExpireTurns()
        {
            for (int i = 0; i < _members.Count; i++)
            {
                Member member = _members[i];
                if (member.StrikeSince >= 0f && (Now - member.StrikeSince > STRIKE_TURN || member.StrikeFoe == null ||
                                                 !member.StrikeFoe.gameObject.activeInHierarchy))
                {
                    member.StrikeFoe = null;
                    member.StrikeSince = -1f;
                }
            }
        }

        /// <summary>
        /// A slot of the phalanx in the formation's own frame: x across (right +), y along (0 — the front rank, the ranks
        /// behind it negative). Ranks of files, the last rank (it may be short) centred like the others.
        /// </summary>
        public static Vector2 PhalanxOffset(int slot, int count, int files, float spacing, float rankGap)
        {
            files = Mathf.Max(1, files);
            int rank = slot / files;
            int file = slot % files;
            int inRank = Mathf.Clamp(count - rank * files, 1, files);
            return new Vector2((file - (inRank - 1) * 0.5f) * spacing, -rank * rankGap);
        }

        private Vector3 MembersMiddle()
        {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < _members.Count; i++)
                sum += _members[i].Position;
            Vector3 middle = _members.Count > 0 ? sum / _members.Count : Anchor;
            middle.y = Anchor.y;
            return middle;
        }

        // Degrees round a point from world forward, 0…360
        private static float Bearing(Vector3 offset) => Mathf.Repeat(Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg, 360f);

        private static Vector3 Flat(Vector3 vector) => new(vector.x, 0f, vector.z);

        private static Vector3 FlatUnit(Vector3 vector, Vector3 fallback)
        {
            Vector3 flat = Flat(vector);
            return flat.sqrMagnitude > 1e-6f ? flat.normalized : fallback;
        }
    }
}
