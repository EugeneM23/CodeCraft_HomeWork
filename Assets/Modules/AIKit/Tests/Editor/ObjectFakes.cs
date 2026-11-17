using System.Collections.Generic;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// A rack that takes weapons from the world (a rock, a turret's gun) — the stand-in for the moves lane's rack in the
    /// tests of the objects: the carried weapons are the character's; a weapon from the world is the weapon in the hand
    /// until its uses are spent (its object used up — made inactive here) or it is let go, then the previous one is back.
    /// It writes down every take-up (the weapon, the object, the uses, mounted or not).
    /// </summary>
    internal sealed class WorldRack : IWeaponRack
    {
        public readonly struct Taken
        {
            public readonly WeaponAsset Weapon;
            public readonly Transform Model;
            public readonly int Uses;
            public readonly bool Mounted;

            public Taken(WeaponAsset weapon, Transform model, int uses, bool mounted)
            {
                Weapon = weapon;
                Model = model;
                Uses = uses;
                Mounted = mounted;
            }
        }

        private readonly AICharacter _character;
        private Transform _fromWorld;
        private WeaponAsset _previous;
        private int _uses;
        private bool _mounted;

        public readonly List<Taken> TakenUp = new();

        public WorldRack(AICharacter character) => _character = character;

        public IReadOnlyList<WeaponAsset> Carried => _character.Carried;

        public WeaponAsset Find(string weapon) => _character.Find(weapon);

        public void PutAway(WeaponAsset weapon) => _character.PutAway(weapon);

        public void ShowMotionsOf(WeaponAsset weapon) => _character.ShowMotionsOf(weapon);

        public WeaponAsset After(WeaponAsset weapon) => _character.After(weapon);

        public void TakeUp(WeaponAsset weapon) => _character.TakeUp(weapon);

        public bool TakeUpFromWorld(WeaponAsset weapon, Transform model, int uses, bool mounted)
        {
            if (FromWorld != null || weapon == null || model == null)
                return false;

            TakenUp.Add(new Taken(weapon, model, uses, mounted));
            _previous = _character.Weapon;
            _fromWorld = model;
            _uses = uses > 0 ? uses : -1;
            _mounted = mounted;
            _character.Weapon = weapon;
            return true;
        }

        public Transform FromWorld => _fromWorld != null ? _fromWorld : null;

        public int UsesLeft => FromWorld != null ? _uses : -1;

        public void SpendUse()
        {
            if (FromWorld == null || _uses < 0)
                return;

            _uses--;
            if (_uses > 0)
                return;

            Transform spent = _fromWorld;
            Back();
            if (!_mounted && spent != null)
                spent.gameObject.SetActive(false);  // a thing thrown is used up; a mounted gun stays where it stands
        }

        public Transform LetGoFromWorld()
        {
            Transform held = FromWorld;
            if (held != null)
                Back();
            return held;
        }

        private void Back()
        {
            _fromWorld = null;
            _uses = 0;
            _character.Weapon = _previous;
            _previous = null;
        }
    }

    /// <summary>The character holds a weapon taken from the world (the stand-in for "Is (Me) Holds Picked Weapon").</summary>
    internal sealed class HoldsWorldWeapon : AgentCondition
    {
        /// <summary>The opposite answer (a test's stand-in for "Is Not").</summary>
        public bool Not;

        protected override bool Evaluate(AgentContext context) => Not != (context?.Rack?.FromWorld != null);
    }

    /// <summary>The character occupies an object of the world (the stand-in for "Is (Me) Occupying").</summary>
    internal sealed class OccupiesSomething : AgentCondition
    {
        /// <summary>The opposite answer (a test's stand-in for "Is Not").</summary>
        public bool Not;

        protected override bool Evaluate(AgentContext context) => Not != (context?.Occupied != null);
    }

    /// <summary>The character's hands hold something (the stand-in for "Is (Me) Carrying").</summary>
    internal sealed class CarriesSomething : AgentCondition
    {
        /// <summary>The opposite answer (a test's stand-in for "Is Not").</summary>
        public bool Not;

        protected override bool Evaluate(AgentContext context) => Not != (context?.Hands?.Item != null);
    }

    /// <summary>The character is in cover (the stand-in for "Is (Me) In Cover").</summary>
    internal sealed class IsInCover : AgentCondition
    {
        /// <summary>The opposite answer (a test's stand-in for "Is Not").</summary>
        public bool Not;

        protected override bool Evaluate(AgentContext context) => Not != (context != null && context.InCover);
    }

    /// <summary>A world object of a test, with its turning on and off reachable (OnEnable / OnDisable).</summary>
    internal sealed class SwitchedObject : WorldObject
    {
        public void TurnOn() => OnEnable();

        public void TurnOff() => OnDisable();

        public override bool Offers(Offer offer, AgentContext user) => offer == Offer.Occupy;
    }
}
