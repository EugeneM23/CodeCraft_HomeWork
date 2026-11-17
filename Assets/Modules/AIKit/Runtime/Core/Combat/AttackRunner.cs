using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// The character carries a move out with the weapon in its hand (handbook §20.1: the graph asks, the body acts and
    /// answers; the weapon is data — §16). The Attack node commands and waits; this does the rest by the weapon's
    /// numbers: the body stops, faces the target and asks for the move's action; a weapon that shoots asks the target for
    /// a place to shoot from first (its Shot's pool); after the wind-up the blow lands on the target in reach — or the
    /// projectile leaves (the body's IProjectileLauncher, straight or a lob by the shot's Arc; none — it lands at once);
    /// then the rest. A weapon taken from the world (a rock, a turret) spends one of its moves with every move delivered,
    /// and its own model flies when the shot says so (the rock). The reach is counted from where the swing started (a
    /// blow that steps forward with its clip). One character's attack in progress is a WeaponAttack.
    /// </summary>
    public static class AttackRunner
    {
        /// <summary>Seconds past the move's whole time a blow the presentation tells is waited for before it counts as lost.</summary>
        private const float MOMENT_GRACE = 0.25f;

        /// <summary>
        /// The move reaches the target from this place: near enough and, for a move that needs a clear line, with
        /// nothing in the way.
        /// </summary>
        public static bool InReach(AgentContext context, Vector3 from, WeaponMove move, Transform target) =>
            move != null && target != null && Vector3.Distance(from, target.position) <= move.Reach &&
            (!move.NeedsSight || Sees(context, target));

        /// <summary>
        /// The command: the body stops where it is, and the move starts if the target is in reach from here and the hands
        /// are free (else the attack is not started and its first step fails — no swing in the air, no blow with a box in
        /// the hands). A weapon that shoots starts the
        /// swing once it has a place to shoot from.
        /// </summary>
        public static void Begin(AgentContext context, WeaponAttack attack)
        {
            if (context == null || attack?.Weapon == null)
                return;

            context.Navigation?.Clear();
            context.Actuator?.Stop();
            attack.From = context.Position;
            // Hands that carry something (a box) do not strike: the graph puts the load down first (Drop)
            attack.Started = context.Hands?.Item == null && InReach(context, attack.From, attack.Move, attack.Target) &&
                             ShotFlies(context, attack);
            if (attack.Started && AskForPlace(context, attack, 0f) == TaskStatus.Success)
            {
                StartSwing(context, attack);
                context.AttackPhase = AbilityPhase.WindUp;           // the agent's status (step 90)
            }
        }

        /// <summary>
        /// One step of the attack. Running — the move goes on; Success — the move is over and the target is still in
        /// reach (the next one may follow); Failure — nothing to strike from here (out of reach, behind a cover, no
        /// place to shoot from): come closer, or do something else.
        /// </summary>
        public static TaskStatus Tick(AgentContext context, WeaponAttack attack, float dt)
        {
            if (context == null || attack?.Weapon == null || !attack.Started)
                return TaskStatus.Failure;

            if (!attack.Swinging)
            {
                TaskStatus place = AskForPlace(context, attack, dt);
                if (place == TaskStatus.Failure)
                    return TaskStatus.Failure;

                Face(context, attack);
                if (place == TaskStatus.Running)
                    return TaskStatus.Running;

                StartSwing(context, attack);
            }

            attack.Time += dt;
            WeaponMove move = attack.Move;
            context.AttackPhase = attack.Struck ? AbilityPhase.Recovery : AbilityPhase.WindUp;  // the agent's status (step 90)
            if (!attack.Struck)
            {
                Face(context, attack);
                bool missed = false;
                if (attack.ByMoment)
                {
                    // The presentation tells the frame of the blow (handbook §20.3). It may never come — the swing cut off
                    // the screen, or the move's whole time gone by: then no blow, decided here (the swing did not reach
                    // its frame)
                    if (!context.Actions.WasTold(attack.Request, ActionState.HIT))
                    {
                        bool gone = context.Actions.Serial != attack.Request || !context.Actions.IsShown;
                        if (!gone && attack.Time < attack.Length + MOMENT_GRACE)
                            return TaskStatus.Running;
                        missed = true;
                    }
                }
                else if (attack.Time < move.WindUp)
                {
                    return TaskStatus.Running;
                }

                attack.Struck = true;
                context.AttackPhase = AbilityPhase.Recovery;
                // A weapon taken out of the hand in the middle of the swing: no blow
                attack.Landed = !missed && context.Weapon == attack.Weapon &&
                                InReach(context, attack.From, move, attack.Target) &&
                                ShotFlies(context, attack);   // a shot that would hit a wall is held
                if (attack.Landed)
                {
                    Deliver(context, attack);
                    context.Rack?.SpendUse();                 // a weapon from the world spends a move (a rock: gone)
                }
            }

            if (attack.Time < attack.Length)
                return TaskStatus.Running;

            // The move is over. The target still in reach — the next one may follow
            return InReach(context, context.Position, move, attack.Target) && ShotFlies(context, attack)
                ? TaskStatus.Success
                : TaskStatus.Failure;
        }

        /// <summary>
        /// The attack is left — at its end or taken away: a move left before it is over drops its action at once, as a
        /// cancelled ability's (AbilityRunner.Cancel); a move that ran to its end lets its action finish on its own. A
        /// place to shoot from that it holds goes back.
        /// </summary>
        public static void End(AgentContext context, WeaponAttack attack)
        {
            if (attack == null)
                return;

            if (context != null)
                context.AttackPhase = AbilityPhase.None;            // the agent's status (step 90)

            if (attack.ShowsAction && attack.Move != null && attack.Time < attack.Length)
                context?.Actions.End(true);
            attack.ShowsAction = false;

            if (attack.Place != null && context?.Agent != null)
                attack.Place.Tokens.Release(context.Agent, attack.Pool);
            attack.Place = null;
        }

        /// <summary>
        /// A weapon that shoots: its projectile would reach the target from here, by its own line (from the hands, with
        /// room on each side; a lob over its arc — the body's launcher says). Anything else, or a body with no launcher
        /// (the shot lands at once) — yes.
        /// </summary>
        public static bool ShotFlies(AgentContext context, WeaponAttack attack)
        {
            WeaponAsset weapon = attack?.Weapon;
            if (context?.Launcher == null || weapon == null || !weapon.Shoots || attack.Target == null || attack.Move == null)
                return true;

            Transform attacker = Self(context);
            return attacker == null || context.Launcher.CanFly(LaunchOf(context, attack, attacker, false));
        }

        // The blow lands: a weapon that shoots lets its projectile go (the body flies it; a body with no launcher — it
        // lands at once), any other strikes the target in reach
        private static void Deliver(AgentContext context, WeaponAttack attack)
        {
            Transform attacker = Self(context);
            if (attack.Weapon.Shoots && attacker != null)
            {
                Projectiles.Launch(context.Launcher, LaunchOf(context, attack, attacker, true));
                return;
            }

            DirectStriker.Deliver(attacker, attack.Target, attack.Move.Damage);
        }

        /// <summary>
        /// The shot of the move: at the target, by the weapon's shot (straight or a lob). A weapon taken from the world
        /// shoots from its model when it stands where it is (a turret); one in the hand whose shot throws the model (a
        /// rock) lets that model fly — its last move the model itself, an earlier one a copy of it.
        /// </summary>
        private static ProjectileLaunch LaunchOf(AgentContext context, WeaponAttack attack, Transform attacker, bool letGo)
        {
            WeaponAsset weapon = attack.Weapon;
            WeaponShot shot = weapon.Shot;
            IWeaponRack rack = context.Rack;
            bool fromWorld = rack != null && rack.HoldsFromWorld && context.Weapon == weapon;
            Transform model = fromWorld ? rack.FromWorld : null;
            Transform origin = fromWorld && rack.IsMounted ? model : null;
            Transform carried = null;
            if (model != null && !rack.IsMounted && shot.ThrowsModel)
            {
                carried = !letGo || rack.UsesLeft == 1 ? model : CopyOf(model);
                if (letGo)
                    LetGo(carried);
            }

            return new ProjectileLaunch(attacker, attack.Target, attack.Target.position, attack.Move.Damage, shot,
                context.Sight, shot.Arc, 0f, carried, origin, null, 0);
        }

        // One of several throws of the model in the hand: a copy flies, the model stays in the hand
        private static Transform CopyOf(Transform model)
        {
            Transform copy = Object.Instantiate(model, model.position, model.rotation);
            copy.name = model.name;
            return copy;
        }

        // The model is let go: out of the hand at once — it is the flight's, whenever the body's launcher starts it (the
        // spent weapon does not use up what is no longer in the hand) — and, a thing that can be taken, offered to nobody
        // while it flies (PickableItem.IsFlying; it is gone where it lands)
        private static void LetGo(Transform model)
        {
            model.SetParent(null, true);
            if (model.TryGetComponent(out PickableItem item))
                item.IsFlying = true;
        }

        private static Transform Self(AgentContext context) =>
            context.Agent != null && context.Agent.EntityRoot != null ? context.Agent.EntityRoot : context.Self;

        // Before the swing: a place to shoot from, for a weapon that shoots and names a pool. Success — go on; Running —
        // still asking; Failure — no place now. The pool is held firmly (a shot that has started is not taken away by a
        // nearer one) and is not a place to fight from: holding it, the character still waits for a place for its blade
        // (unless a graph asks Take Attack Slot in the very group the shots are in: then its places are both)
        private static TaskStatus AskForPlace(AgentContext context, WeaponAttack attack, float dt)
        {
            WeaponAsset weapon = attack.Weapon;
            if (!weapon.Shoots || string.IsNullOrEmpty(weapon.Shot.Pool) || context?.Agent == null)
                return TaskStatus.Success;

            Engagement engagement = AttackSlots.EngagementOf(context, attack.Target);
            if (engagement == null)
                return TaskStatus.Success;                  // no group round the target: nothing limits the shot

            string pool = weapon.Shot.Pool;
            engagement.Tokens.HoldFirmly(pool);

            // A place of this group the agent holds already, not asked for by this move — Take Attack Slot gave it in the
            // shots' group — is shot from and stays the agent's: the move gives back only a place it asked for itself
            if (attack.Place == null && engagement.Tokens.Holds(context.Agent, pool))
                return TaskStatus.Success;

            attack.Place = engagement;
            attack.Pool = pool;
            TokenResult result = engagement.Tokens.Request(context.Agent, pool, 1, 0f, 0f);
            if (result == TokenResult.Granted)
                return TaskStatus.Success;

            attack.Asked += dt;
            if (result == TokenResult.Pending && attack.Asked < weapon.Shot.AskFor)
                return TaskStatus.Running;

            engagement.Tokens.Release(context.Agent, pool);  // not waiting in the queue: the chain asks again later
            attack.Place = null;
            return TaskStatus.Failure;
        }

        // The swing starts: the move's action is asked for; a presentation that has no such action (or none at all)
        // gets the plain swing instead — one request, never both. A blow that steps as its clip does lands on the target
        // but does not walk into it
        private static void StartSwing(AgentContext context, WeaponAttack attack)
        {
            attack.Swinging = true;
            attack.Time = 0f;
            ActionState actions = context.Actions;
            attack.ShowsAction = false;
            if (!string.IsNullOrEmpty(attack.Move.Action))
            {
                actions.Begin(attack.Move.Action);
                attack.ShowsAction = actions.CanShow(attack.Move.Action);
            }

            if (!attack.ShowsAction)
                actions.Swing();

            // A presentation that tells the frame of the blow: the blow waits for it (handbook §20.3)
            attack.Request = actions.Serial;
            attack.ByMoment = actions.WillTell(attack.ShowsAction ? attack.Move.Action : ActionState.SWING, ActionState.HIT);

            if (context.Actuator is IClipAimActuator clip && clip.ActionsByClip)
                clip.KeepClipOff(attack.Target);
        }

        // The body turns to the target while the swing winds up — when the node holds the facing
        private static void Face(AgentContext context, WeaponAttack attack)
        {
            if (attack.MayFace && attack.Target != null)
                context.Actuator?.Face(attack.Target.position - context.Position);
        }

        // A level line from the eyes: where the pivot of the target is (feet or centre) does not matter
        private static bool Sees(AgentContext context, Transform target)
        {
            if (context?.Sight == null)
                return true;

            Vector3 eye = context.EyePosition;
            Vector3 aim = target.position;
            aim.y = eye.y;
            return context.Sight.HasLineOfSight(eye, aim);
        }
    }
}
