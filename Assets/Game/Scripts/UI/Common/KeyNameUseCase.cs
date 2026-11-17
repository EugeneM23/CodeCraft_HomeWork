using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// The name of a key for hints and the key table, independent of the keyboard layout: keyboard keys by
    /// their physical key ("W", "Left Shift", "Escape"; GetBindingDisplayString would give the letter of the
    /// current layout, a Cyrillic letter for W on a Russian one), mouse buttons from UiStrings (key.lmb…), a composite as its
    /// parts joined with "/" (W/A/S/D). Builds strings: for a refresh on change, not for a tick.
    /// </summary>
    public static class KeyNameUseCase
    {
        public const string EMPTY = "-";
        private const string PART_SEPARATOR = "/";

        private static readonly string[] MousePaths = { "<Mouse>/leftButton", "<Mouse>/rightButton", "<Mouse>/middleButton" };
        private static readonly string[] MouseKeys = { "key.lmb", "key.rmb", "key.mmb" };

        // Composite parts in reading order (W/A/S/D), whatever their order in the asset; other parts follow as they are.
        private static readonly string[] PartOrder = { "up", "left", "down", "right" };

        /// <summary>The name of a control path such as "&lt;Keyboard&gt;/w" (use the binding's effectivePath).</summary>
        public static string OfPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return EMPTY;

            for (int i = 0; i < MousePaths.Length; i++)
            {
                if (string.Equals(path, MousePaths[i], StringComparison.OrdinalIgnoreCase))
                    return UiStrings.Get(MouseKeys[i]);
            }

            string name = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            if (string.IsNullOrEmpty(name))
                return EMPTY;
            return name.Length == 1 ? name.ToUpperInvariant() : name;
        }

        /// <summary>The name of one binding; a composite is named by its parts ("W/A/S/D").</summary>
        public static string OfBinding(InputAction action, int bindingIndex)
        {
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
                return EMPTY;

            IReadOnlyList<InputBinding> bindings = action.bindings;
            InputBinding binding = bindings[bindingIndex];
            if (!binding.isComposite)
                return OfPath(binding.effectivePath);

            int first = bindingIndex + 1;
            int end = first;
            while (end < bindings.Count && bindings[end].isPartOfComposite)
                end++;

            StringBuilder text = new();
            for (int order = 0; order < PartOrder.Length; order++)
            {
                for (int i = first; i < end; i++)
                {
                    if (string.Equals(bindings[i].name, PartOrder[order], StringComparison.OrdinalIgnoreCase))
                        AppendPart(text, bindings[i]);
                }
            }

            for (int i = first; i < end; i++)
            {
                if (!IsOrderedPart(bindings[i].name))
                    AppendPart(text, bindings[i]);
            }

            return text.Length > 0 ? text.ToString() : EMPTY;
        }

        /// <summary>The name of the action's first plain binding, or of its first binding (a composite) when it has none.</summary>
        public static string OfAction(InputAction action)
        {
            if (action == null)
                return EMPTY;

            int index = KeyRebindUseCase.FindFirstPlainBinding(action);
            return OfBinding(action, index >= 0 ? index : 0);
        }

        private static bool IsOrderedPart(string name)
        {
            for (int i = 0; i < PartOrder.Length; i++)
            {
                if (string.Equals(PartOrder[i], name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static void AppendPart(StringBuilder text, InputBinding part)
        {
            if (text.Length > 0)
                text.Append(PART_SEPARATOR);
            text.Append(OfPath(part.effectivePath));
        }
    }
}
