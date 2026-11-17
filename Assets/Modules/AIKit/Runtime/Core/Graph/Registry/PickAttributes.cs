using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// A text field of a node that names a move of the character's weapon (WeaponAsset): the editor offers the moves the
    /// weapon has, so a name is picked, not typed — a move named otherwise is seen while editing, not in Play.
    /// </summary>
    public sealed class WeaponMoveNameAttribute : PropertyAttribute
    {
    }

    /// <summary>
    /// A text field of a node that names one of the weapons the character carries (by the asset's name): the editor
    /// offers them, so a weapon is picked, not typed.
    /// </summary>
    public sealed class WeaponNameAttribute : PropertyAttribute
    {
    }

    /// <summary>A text field of a node that names a Unity tag: the editor offers the project's tags.</summary>
    public sealed class TagNameAttribute : PropertyAttribute
    {
    }

    /// <summary>
    /// A text field of a node that names an action the body shows (a state of the animator with the tag Action, the
    /// action of a weapon move or a dash): the editor offers the actions found in the project (step 90, C5). The model
    /// knows only the name; the list is the editor's.
    /// </summary>
    public sealed class ActionNameAttribute : PropertyAttribute
    {
    }

    /// <summary>
    /// A text field of a node that names a group of attack places at a target (empty — the common one): the editor offers
    /// the groups the project uses (shots' pools, Attack Slot Capacity, other graphs).
    /// </summary>
    public sealed class PoolNameAttribute : PropertyAttribute
    {
    }

    /// <summary>A text field that is free text by nature (a Debug Log message): the editor draws a text field.</summary>
    public sealed class FreeTextAttribute : PropertyAttribute
    {
    }

    /// <summary>
    /// A text field of a node that names a type (by its full name): the editor offers the types derived from Base that are
    /// not abstract — the component a search looks for, for instance.
    /// </summary>
    public sealed class TypeNameAttribute : PropertyAttribute
    {
        public TypeNameAttribute(System.Type baseType) => Base = baseType;

        public System.Type Base { get; }

        /// <summary>
        /// The components a Try Get Component may look for (step 91): every public one of the game and of Unity (not the editor's),
        /// and those the game marked [AIReadable] — first in the list.
        /// </summary>
        public bool AnyComponent { get; set; }

        /// <summary>What the empty value means, said in the list ("(any type)"); null — "(none)".</summary>
        public string Empty { get; set; }
    }

    /// <summary>
    /// A text field of a node that names a member of the component wired into the node (Try Get Component) (step 91): the editor offers
    /// the values of the type a graph may read (AIReadables: its public numbers, flags, vectors by axis, and what the game
    /// marked [AIReadable]) — numbers or flags, as the node reads (IReadsMembers).
    /// </summary>
    public sealed class MemberNameAttribute : PropertyAttribute
    {
    }

    /// <summary>A node that reads a member of a type (Get, Is with a component wired in): what its member list is made of.</summary>
    public interface IReadsMembers
    {
        /// <summary>The type whose members are offered; null — nothing wired (the field is not drawn).</summary>
        System.Type MemberSource { get; }

        /// <summary>It reads flags (an Is), not numbers (a Get).</summary>
        bool ReadsFlags { get; }

        /// <summary>The canvas draws this setting now (some settings make sense only with, or only without, a component wired in).</summary>
        bool Shows(string field);
    }
}
