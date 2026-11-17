using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// A turret a character mans (handbook §24.1, a smart object): it offers Use while it has rounds and nobody else
    /// mans it, and Load while it is not full. Its gun is a weapon "on a mount" (step 90, spec 10.6): manning it, the
    /// gunner takes the gun up as a weapon from the world (IWeaponRack.TakeUpFromWorld, mounted — the model stays where it
    /// stands) with the rounds of the turret as its uses, and the gunner's Attack decision fires it like any weapon — the
    /// turret never fires by itself. While manned, the rounds are in the gun (the gunner's rack counts them); spent, the
    /// gunner's own weapon is back and the turret is empty until somebody brings a box of ammo (Load: the box in the
    /// hands is used up, the turret is full). A gunner who leaves with rounds in the gun gives them back. The Fired event
    /// lets the game show the rounds (a tracer, a flash). A world object (handbook §24): it keeps its own state, the
    /// agents only ask it.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("AI Kit/Turret")]
    public sealed class Turret : WorldObject
    {
        [Tooltip("The turret's gun: the weapon the gunner fires while it mans the turret (its move: reach, damage, wind-up)")]
        public WeaponAsset Weapon;

        [Tooltip("The gun's object the gunner holds while it mans the turret; empty — the Head, else the turret itself")]
        public Transform Model;

        [Tooltip("Rounds a full turret holds")]
        [Min(1)] public int Capacity = 10;

        [Tooltip("Rounds in it now (not counting the rounds in the gun of a gunner who mans it)")]
        [Min(0)] [SerializeField] [UnityEngine.Serialization.FormerlySerializedAs("Ammo")] private int _ammo = 10;

        [Tooltip("Where the gunner stands; empty — Seat Distance behind the turret")]
        public Transform Seat;

        [Tooltip("Metres behind the turret the gunner stands when it has no Seat")]
        [Min(0f)] public float SeatDistance = 1.2f;

        [Tooltip("Metres from the seat a character counts as standing at it")]
        [Min(0.05f)] public float SeatReach = 0.6f;

        [Tooltip("What turns to the gunner's target (the barrel); empty — nothing turns")]
        public Transform Head;

        [Tooltip("Metres above the turret's foot the rounds leave from (when it has no Head)")]
        public float MuzzleHeight = 1.2f;

        [Tooltip("Seconds the loading of a box lasts")]
        [Min(0f)] public float LoadTime = 1f;

        [Tooltip("The action the body shows while it loads (a state of its animator); empty — none")]
        [ActionName] public string LoadAction = "";

        // The character manning it now (its rack holds the gun) and the rounds last seen in the gun
        private AgentContext _gunner;
        private int _inGun;

        /// <summary>A round left the gun: the turret, the gunner's target (may be null), and it was fired (always true).</summary>
        public event Action<Turret, Transform, bool> Fired;

        /// <summary>The object the gunner holds as the gun.</summary>
        public Transform GunModel => Model != null ? Model : Head != null ? Head : transform;

        /// <summary>Rounds in it now: those in the turret and those in the gun of a gunner who mans it.</summary>
        public int Ammo
        {
            get
            {
                Watch();
                return _ammo + (_gunner != null ? _inGun : 0);
            }
            set
            {
                Watch();
                _ammo = Mathf.Max(0, value - (_gunner != null ? _inGun : 0));
            }
        }

        public bool IsEmpty => Ammo <= 0;

        public bool IsFull => Ammo >= Capacity;

        /// <summary>The character manning it now; null — nobody.</summary>
        public AICharacter Gunner
        {
            get
            {
                Watch();
                return _gunner?.Agent;
            }
        }

        /// <summary>Where the gunner stands.</summary>
        public Vector3 SeatPosition => Seat != null ? Seat.position : transform.position - transform.forward * SeatDistance;

        /// <summary>Where the rounds leave from.</summary>
        public Vector3 Muzzle => Head != null ? Head.position : transform.position + Vector3.up * MuzzleHeight;

        /// <summary>The direction the head looks (the turret's own forward without a head), flat.</summary>
        public Vector3 Aim
        {
            get
            {
                Vector3 forward = Flat((Head != null ? Head : transform).forward);
                return forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            }
        }

        public override float Reach => SeatReach;

        /// <summary>Use: it has rounds, a gun, and nobody else mans it. Load: it is not full.</summary>
        public override bool Offers(Offer offer, AgentContext user)
        {
            if (!IsLive)
                return false;

            Watch();
            return offer switch
            {
                Offer.Use => Weapon != null && user?.Rack != null && (_gunner != null ? _gunner == user : _ammo > 0),
                Offer.Load => Ammo < Capacity,
                _ => false
            };
        }

        /// <summary>One gunner goes for it; a box may be brought by anybody (the gunner stays at it meanwhile).</summary>
        public override bool OneAtATime(Offer offer) => offer == Offer.Use;

        public override Vector3 PlaceFor(Offer offer, AgentContext user, Transform threat) => SeatPosition;

        public override bool Begin(Interaction interaction)
        {
            AgentContext user = interaction.User;
            if (interaction.Offer == Offer.Load)
            {
                if (user?.Hands?.Item == null || !Offers(Offer.Load, user))
                    return false;               // nothing to load it with

                ShowAction(interaction, LoadAction);
                return true;
            }

            if (interaction.Offer != Offer.Use)
                return false;

            Watch();
            if (_gunner != null && _gunner == user)
                return true;                    // manning it already: nothing more to do

            if (!Offers(Offer.Use, user))
                return false;

            // Both hands on the gun: a load in the hands is put down at the seat first
            Transform held = user.Hands?.LetGo();
            if (held != null)
                Items.PutDown(held, Walking.Ground(user, user.Position - Aim * DropTask.AHEAD));

            int rounds = _ammo;
            if (!user.Rack.TakeUpFromWorld(Weapon, GunModel, rounds, mounted: true))
                return false;

            _ammo = 0;                          // the rounds are in the gun now: the gunner's rack counts them
            _gunner = user;
            _inGun = rounds;
            TakeOccupant(user, interaction.Threat);
            return true;
        }

        public override TaskStatus Tick(Interaction interaction, float dt)
        {
            if (interaction.Offer == Offer.Use)
            {
                Face(interaction, interaction.User.Position + Aim);
                return TaskStatus.Success;      // manned: the Attack decision fires the gun from here
            }

            Face(interaction, transform.position);
            if (interaction.Time < LoadTime)
                return TaskStatus.Running;

            EndAction(interaction, false);
            IHands hands = interaction.User.Hands;
            if (hands?.Item == null)
                return TaskStatus.Failure;      // the box was lost on the way

            Items.UseUp(hands.LetGo());
            Load();
            return TaskStatus.Success;
        }

        public override void End(Interaction interaction, ExitReason reason) =>
            EndAction(interaction, reason != ExitReason.Finished);

        /// <summary>Fills the turret up (a box of ammo brought): with the rounds in a manned gun, Capacity in all.</summary>
        public void Load()
        {
            Watch();
            _ammo = Mathf.Max(_ammo, Capacity - (_gunner != null ? _inGun : 0));
        }

        /// <summary>A gunner mans it while its rack holds the gun (and it is about: a fallen one mans nothing).</summary>
        protected override bool StillThere(AgentContext user, Transform threat) =>
            IsAwake(user) && user.Rack != null && user.Rack.FromWorld == GunModel;

        /// <summary>Switched off (pooled, broken): its gunner leaves the gun, the rounds in it are the turret's again.</summary>
        protected override void OnDisable()
        {
            Watch();
            base.OnDisable();
        }

        /// <summary>
        /// The turret looks at its gunner (on every ask and every frame): the rounds that left the gun are fired (the Fired
        /// event, one per round); a gunner that walked off the seat or fell (switched off, its body still at the seat), or
        /// a turret switched off, leaves the gun on its mount (the gunner's rack lets it go) and the rounds in it come
        /// back; a gunner whose rack no longer holds the gun has left by itself — with its last round it fired the gun
        /// empty (the rack lets go of a spent gun itself), with more (it took its own weapon up), or fallen, the rounds
        /// come back.
        /// </summary>
        private void Watch()
        {
            if (_gunner == null)
                return;

            IWeaponRack rack = _gunner.Agent != null ? _gunner.Rack : null;
            if (rack != null && rack.FromWorld == GunModel)
            {
                int left = rack.UsesLeft;
                if (left >= 0 && left < _inGun)
                {
                    int fired = _inGun - left;
                    _inGun = left;
                    for (int i = 0; i < fired; i++)
                        Fired?.Invoke(this, _gunner.BrainTarget, true);
                }

                if (IsLive && IsAwake(_gunner) && IsAtPlace(Offer.Use, _gunner, null, LEAVE_SLACK))
                    return;

                rack.LetGoFromWorld();                  // walked off, fell, or the turret is gone: the gun stays where it stands
                Leave(roundsBack: true);
                return;
            }

            // The gun left the hand by itself. One round seen in it last — that round was fired and the spent gun let go;
            // a gunner that is not about any more (it fell, letting go of what its hand held) fired nothing: back
            Leave(roundsBack: _inGun > 1 || !IsAwake(_gunner));
        }

        // The gunner is gone from the gun: its rounds come back to the turret, or they were fired
        private void Leave(bool roundsBack)
        {
            AgentContext gone = _gunner;
            if (roundsBack)
            {
                _ammo += _inGun;
            }
            else
            {
                for (int i = 0; i < _inGun; i++)
                    Fired?.Invoke(this, gone.BrainTarget, true);
            }

            _gunner = null;
            _inGun = 0;
            LetGoOf(gone);
        }

        private void LateUpdate()
        {
            Watch();
            if (_gunner == null || Head == null)
                return;

            // The barrel turns with the gun: at the gunner's target
            Transform target = _gunner.BrainTarget;
            Vector3 to = target != null ? Flat(target.position - Head.position) : Vector3.zero;
            if (to.sqrMagnitude > 1e-4f)
                Head.rotation = Quaternion.LookRotation(to, Vector3.up);
        }
    }
}
