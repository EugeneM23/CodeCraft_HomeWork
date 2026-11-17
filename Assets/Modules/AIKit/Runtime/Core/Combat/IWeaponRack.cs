using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// The weapons a character carries and where their models are (the character keeps it: AICharacter). The Switch
    /// Weapon node puts one away and takes another up; the weapon in the hand is AgentContext.Weapon.
    /// </summary>
    public interface IWeaponRack
    {
        /// <summary>Every weapon the character carries, the one in the hand among them.</summary>
        IReadOnlyList<WeaponAsset> Carried { get; }

        /// <summary>The carried weapon of this name (the asset's name); null — not carried.</summary>
        WeaponAsset Find(string weapon);

        /// <summary>The weapon's model goes on the back; the hand is empty if it held it (the weapon in the hand stays until another is taken up).</summary>
        void PutAway(WeaponAsset weapon);

        /// <summary>
        /// The character's moves are shown with this weapon's clips from now on (its Motions) — asked before the weapon is
        /// taken up, so its draw is shown with its own clips. A fact of the character: the presentation (AI Animator)
        /// reads it and swaps its clip set before it shows the next action; nothing is told to it.
        /// </summary>
        void ShowMotionsOf(WeaponAsset weapon);

        /// <summary>The carried weapon after this one, round to the first; null — none carried.</summary>
        WeaponAsset After(WeaponAsset weapon);

        /// <summary>The weapon is in the hand from now on: its model, and the moves the Attack node strikes with.</summary>
        void TakeUp(WeaponAsset weapon);

        /// <summary>
        /// A weapon taken from the world for a while (step 90): a rock picked up — its model is that object, put in the
        /// hand — or a mounted gun manned — its model stays where it stands (mounted). It is not one of the carried
        /// weapons (Carried, After, Change / Switch Weapon never see it); the character's own weapon goes on the back, as
        /// when the hands carry a box (this is no switch of weapons), and comes back into the hand when this one is spent
        /// or let go. uses — how many moves it has (0 — the weapon's own Uses; 0 there too — no end). False — the hand
        /// cannot take it now (it holds another weapon from the world, or carries a thing).
        /// </summary>
        bool TakeUpFromWorld(WeaponAsset weapon, Transform model, int uses, bool mounted);

        /// <summary>The object of the weapon taken from the world that is in the hand now; null — none.</summary>
        Transform FromWorld { get; }

        /// <summary>A weapon taken from the world is in the hand now (its object may be none).</summary>
        bool HoldsFromWorld => FromWorld != null;

        /// <summary>The weapon taken from the world stands where it is (a turret): its shot leaves from its model.</summary>
        bool IsMounted => false;

        /// <summary>
        /// The character's own weapon in the hand — behind a weapon taken from the world while one is held: what Switch
        /// and Change Weapon work with. Null — the rack does not tell (the weapon in the hand is the own one).
        /// </summary>
        WeaponAsset Own => null;

        /// <summary>Moves left of the weapon in the hand; below 0 — no end.</summary>
        int UsesLeft { get; }

        /// <summary>
        /// One move of the weapon in the hand was delivered (the move runner calls it): a weapon with uses spends one; a
        /// weapon taken from the world with none left is gone (its object is used up) and the previous one is back.
        /// </summary>
        void SpendUse();

        /// <summary>
        /// The weapon taken from the world leaves the hand unspent (switched away, dropped, the character dies or leaves
        /// the gun): a picked thing is put down where the character stands, a mounted gun is left; the previous weapon is
        /// back. Its object; null — none was held.
        /// </summary>
        Transform LetGoFromWorld();
    }

    /// <summary>
    /// A weapon a character carries, with its models in the scene: the one in the hand and the one on the back — the
    /// character shows one of them (the pack's own way: a bow in the hand, a bow on the back). Either may be left
    /// empty: nothing is shown there.
    /// </summary>
    [System.Serializable]
    public sealed class CharacterWeapon
    {
        public WeaponAsset Weapon;

        [Tooltip("The model in the hand (under the hand's bone): shown while the weapon is in the hand")]
        public GameObject InHand;

        [Tooltip("The model on the back: shown while the weapon is not in the hand")]
        public GameObject OnBack;
    }
}
