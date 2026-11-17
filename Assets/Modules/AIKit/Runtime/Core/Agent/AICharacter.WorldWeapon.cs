using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// A weapon taken from the world for a while (step 90, IWeaponRack): a rock picked up, a turret manned. It is not a
    /// switch of weapons — the rule "only Switch Weapon changes the weapon" is about the character's own weapons, and it
    /// stands: the own weapon in the hand goes on the back exactly as when the hands carry a box, the weapon from the
    /// world is the one the Attack node strikes with (Context.Weapon) — its model in the hand, or, mounted, where it
    /// stands — and when its moves are spent (counted here, never in the asset) or it is let go, the own weapon is back
    /// in the hand. Carried, After, Find, WeaponIndex and the Switch / Change Weapon nodes never see it. Its moves are
    /// shown with its own clips (MotionsWeapon) while it is in the hand. The character switched off — dead, the lab's way
    /// — lets go of it: a rock lies where it fell, a manned gun is left on its mount.
    /// </summary>
    public sealed partial class AICharacter
    {
        private WeaponAsset _worldWeapon;
        private Transform _worldModel;
        private bool _worldMounted;
        private int _worldUses = -1;            // moves left; below 0 — no end

        public bool TakeUpFromWorld(WeaponAsset weapon, Transform model, int uses, bool mounted)
        {
            if (weapon == null || _worldWeapon != null || Item != null)
                return false;

            _worldWeapon = weapon;
            _worldModel = model;
            _worldMounted = mounted;
            int count = uses > 0 ? uses : weapon.Uses;
            _worldUses = count > 0 ? count : -1;
            if (model != null && !mounted)
                PutInTheHand(model);

            if (Context != null)
                Context.Weapon = weapon;
            MotionsWeapon = weapon;                 // its moves are shown with its own clips (its Motions)
            ShowWeapons();                          // the own weapon goes on the back, as for a box in the hands
            return true;
        }

        public Transform FromWorld => _worldWeapon != null ? _worldModel : null;

        public bool HoldsFromWorld => _worldWeapon != null;

        public bool IsMounted => _worldWeapon != null && _worldMounted;

        public WeaponAsset Own => _weapon;

        /// <summary>The weapon taken from the world in the hand now; null — none (the own weapon is in the hand).</summary>
        public WeaponAsset WorldWeapon => _worldWeapon;

        public int UsesLeft => _worldWeapon != null ? _worldUses : -1;

        public void SpendUse()
        {
            if (_worldWeapon == null || _worldUses < 0)
                return;

            _worldUses--;
            if (_worldUses > 0)
                return;

            // Spent: a model still in the hand is used up (one that flew is the flight's — gone where it lands); a
            // mounted gun stays where it stands, only let go
            Transform model = _worldModel;
            bool mounted = _worldMounted;
            LetGoOfTheWorldWeapon();
            if (!mounted && model != null && model.IsChildOf(transform))
                Projectiles.UseUp(model);
        }

        public Transform LetGoFromWorld()
        {
            if (_worldWeapon == null)
                return null;

            Transform model = _worldModel;
            bool mounted = _worldMounted;
            LetGoOfTheWorldWeapon();
            if (!mounted && model != null && model.IsChildOf(transform))
                Items.PutDown(model, transform.position + transform.forward * 0.5f);   // put down where it stands
            return model;
        }

        private void LetGoOfTheWorldWeapon()
        {
            ForgetTheWorldWeapon();
            ShowWeapons();
        }

        // The weapon from the world is no longer the character's: the own weapon is back in the hand, with its clips
        private void ForgetTheWorldWeapon()
        {
            _worldWeapon = null;
            _worldModel = null;
            _worldMounted = false;
            _worldUses = -1;
            if (Context != null)
                Context.Weapon = _weapon;
            MotionsWeapon = _weapon;
        }

        // Switched off (OnDisable). Only the component switched off while its object stays — how the lab says a character
        // died — lets go as LetGoFromWorld does (IWeaponRack: "the character dies"): the rock is put down for another to
        // take, a manned gun is left on its mount and its turret gets the rounds back. An object that goes inactive or
        // is destroyed cannot have its hierarchy changed now: the weapon stops being the character's (a gun's turret sees
        // its gunner gone), a rock in the hand goes with the hand
        private void LetGoWhenSwitchedOff()
        {
            if (_worldWeapon == null)
                return;

            if (!enabled && gameObject.activeInHierarchy)
                LetGoFromWorld();
            else
                ForgetTheWorldWeapon();
        }

        // The model goes where the hand holds a weapon (the parent of an In Hand model — the hand's bone); a character
        // with no such model holds it where it carries a thing
        private void PutInTheHand(Transform model)
        {
            Transform hand = null;
            for (int i = 0; i < _weapons.Count && hand == null; i++)
            {
                GameObject inHand = _weapons[i]?.InHand;
                if (inHand != null && inHand.transform.parent != null && _weapons[i].Weapon == _weapon)
                    hand = inHand.transform.parent;
            }

            for (int i = 0; i < _weapons.Count && hand == null; i++)
            {
                GameObject inHand = _weapons[i]?.InHand;
                if (inHand != null && inHand.transform.parent != null)
                    hand = inHand.transform.parent;
            }

            if (hand != null)
            {
                model.SetParent(hand, false);
                model.localPosition = Vector3.zero;
            }
            else
            {
                model.SetParent(_carryPoint != null ? _carryPoint : transform, false);
                model.localPosition = _carryPoint != null ? Vector3.zero : _carryOffset;
            }

            model.localRotation = Quaternion.identity;
        }
    }
}
