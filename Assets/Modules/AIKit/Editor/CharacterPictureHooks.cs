using System;
using UnityEditor;

namespace AIKit.Editor
{
    /// <summary>
    /// What the character's inspector asks of the assembly that shows characters (the AI Animator's editor, step 90 C22):
    /// the core's editor does not know the animation adapter, the adapter's editor plugs in by existing (found by type,
    /// like the setup steps). Measuring the climbing and jumping clips into the body (C20) is one of these.
    /// </summary>
    public interface ICharacterPictureHook
    {
        /// <summary>
        /// Measures the character's climbing and jumping clips into its body (its Traversal), with Undo. What was done,
        /// in words; null — there is nothing to measure with (no animator, no clips).
        /// </summary>
        string MeasureTraversal(AICharacter character);

        /// <summary>The body's measured numbers no longer match the clips (they changed since): measure again.</summary>
        bool TraversalIsStale(AICharacter character);

        /// <summary>What is wrong with how the character is shown, in plain words (null — nothing).</summary>
        string Problem(AICharacter character);
    }

    /// <summary>The hooks of the assemblies that show characters (none — the inspector offers nothing of theirs).</summary>
    public static class CharacterPictureHooks
    {
        private static ICharacterPictureHook[] _hooks;

        public static ICharacterPictureHook[] All
        {
            get
            {
                if (_hooks != null)
                    return _hooks;

                var found = new System.Collections.Generic.List<ICharacterPictureHook>();
                foreach (Type type in TypeCache.GetTypesDerivedFrom<ICharacterPictureHook>())
                {
                    if (!type.IsAbstract && !type.IsInterface && type.GetConstructor(Type.EmptyTypes) != null)
                        found.Add((ICharacterPictureHook)Activator.CreateInstance(type));
                }

                _hooks = found.ToArray();
                return _hooks;
            }
        }
    }
}
