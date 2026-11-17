using System.Collections.Generic;

namespace AIKit
{
    // The weapons the character carries: the one in the hand (Weapon), the others on the back (IWeaponRack)
    public sealed partial class AICharacter
    {
        private readonly List<WeaponAsset> _carried = new();

        // The weapon whose model is in the hand now (null — both hands empty: between putting one away and taking another)
        private WeaponAsset _shownInHand;

        /// <summary>The weapons with their models (the Combat section).</summary>
        public List<CharacterWeapon> Weapons => _weapons;

        /// <summary>
        /// Every weapon the character carries: those of the Weapons list in its order, then the weapon in the hand if
        /// it is not on the list.
        /// </summary>
        public IReadOnlyList<WeaponAsset> Carried
        {
            get
            {
                _carried.Clear();
                for (int i = 0; i < _weapons.Count; i++)
                {
                    WeaponAsset weapon = _weapons[i]?.Weapon;
                    if (weapon != null && !_carried.Contains(weapon))
                        _carried.Add(weapon);
                }

                if (_weapon != null && !_carried.Contains(_weapon))
                    _carried.Add(_weapon);
                return _carried;
            }
        }

        /// <summary>
        /// Where the weapon in the hand is among the carried ones (0 — the first); the animator reads it as "Weapon".
        /// </summary>
        public int WeaponIndex
        {
            get
            {
                if (_weapon == null)
                    return 0;

                for (int i = 0; i < _weapons.Count; i++)
                {
                    if (_weapons[i]?.Weapon == _weapon)
                        return i;
                }

                return _weapons.Count;
            }
        }

        /// <summary>The carried weapon of this name; null — not carried.</summary>
        public WeaponAsset Find(string weapon)
        {
            if (string.IsNullOrEmpty(weapon))
                return null;

            IReadOnlyList<WeaponAsset> carried = Carried;
            for (int i = 0; i < carried.Count; i++)
            {
                if (carried[i].name == weapon)
                    return carried[i];
            }

            return null;
        }

        public void PutAway(WeaponAsset weapon)
        {
            if (weapon != null && _shownInHand == weapon)
                _shownInHand = null;
            ShowWeapons();
        }

        public void TakeUp(WeaponAsset weapon) => Weapon = weapon;

        /// <summary>
        /// The weapon whose clips the character's moves are shown with now (its Motions): the one in the hand, or the one
        /// being taken up (Switch Weapon asks for its clips before the draw). A fact of the character the presentation
        /// reads (the AI Animator swaps its clip set by it, before it shows any action); null — the animator's own clips.
        /// </summary>
        public WeaponAsset MotionsWeapon { get; private set; }

        /// <summary>The character's moves are shown with this weapon's clips from now on (its Motions).</summary>
        public void ShowMotionsOf(WeaponAsset weapon) => MotionsWeapon = weapon;

        /// <summary>The carried weapon after this one in the Weapons list, round to the first; null — none carried.</summary>
        public WeaponAsset After(WeaponAsset weapon)
        {
            IReadOnlyList<WeaponAsset> carried = Carried;
            if (carried.Count == 0)
                return null;

            for (int i = 0; i < carried.Count; i++)
            {
                if (carried[i] == weapon)
                    return carried[(i + 1) % carried.Count];
            }

            return carried[0];
        }

        // On a wall now: both hands are the wall's, every weapon on the back (the one fought with stays the Weapon)
        private bool _onWall;

        /// <summary>The body on a wall or off it (asked every step): the weapon goes on the back and back into the hand.</summary>
        private void StowWhileClimbing()
        {
            bool onWall = Context?.Navigation is IClimbingNavigator { ClimbDirection: not 0 };
            if (onWall == _onWall)
                return;

            _onWall = onWall;
            ShowWeapons();
        }

        // Every model where its weapon is: the one in the hand in the hand, the others on the back (all of them on a wall)
        private void ShowWeapons()
        {
            for (int i = 0; i < _weapons.Count; i++)
            {
                CharacterWeapon entry = _weapons[i];
                if (entry == null)
                    continue;

                // On a wall, carrying something in the hands (a box), or with a weapon taken from the world in the hand (a
                // rock, a turret), every weapon of its own is on the back
                bool inHand = !_onWall && Item == null && _worldWeapon == null && entry.Weapon != null &&
                              entry.Weapon == _shownInHand;
                if (entry.InHand != null && entry.InHand.activeSelf != inHand)
                    entry.InHand.SetActive(inHand);
                if (entry.OnBack != null && entry.OnBack.activeSelf == inHand)
                    entry.OnBack.SetActive(!inHand);
            }
        }
    }
}
