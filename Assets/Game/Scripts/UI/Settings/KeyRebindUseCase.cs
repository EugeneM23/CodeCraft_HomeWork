using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// The rules of the key table, without UI: which binding a row edits, which other row holds the same key,
    /// and the swap that frees it. A key slot is one binding of one action (a part of a composite too).
    /// </summary>
    public static class KeyRebindUseCase
    {
        /// <summary>One rebindable binding: an action and the index of the binding in action.bindings.</summary>
        public readonly struct KeySlot
        {
            public readonly InputAction Action;
            public readonly int BindingIndex;

            public KeySlot(InputAction action, int bindingIndex)
            {
                Action = action;
                BindingIndex = bindingIndex;
            }

            public bool IsValid => Action != null && BindingIndex >= 0 && BindingIndex < Action.bindings.Count;

            public string EffectivePath => IsValid ? Action.bindings[BindingIndex].effectivePath : null;
        }

        /// <summary>Index of the part (up, down, left, right…) of the composite with this name; -1 when absent.</summary>
        public static int FindCompositePart(InputAction action, string compositeName, string partName)
        {
            IReadOnlyList<InputBinding> bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                if (!bindings[i].isComposite || !string.Equals(bindings[i].name, compositeName, StringComparison.OrdinalIgnoreCase))
                    continue;

                for (int part = i + 1; part < bindings.Count && bindings[part].isPartOfComposite; part++)
                {
                    if (string.Equals(bindings[part].name, partName, StringComparison.OrdinalIgnoreCase))
                        return part;
                }
                return -1;
            }
            return -1;
        }

        /// <summary>Index of the first plain binding (neither a composite nor its part); -1 when there is none.</summary>
        public static int FindFirstPlainBinding(InputAction action)
        {
            IReadOnlyList<InputBinding> bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                if (!bindings[i].isComposite && !bindings[i].isPartOfComposite)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// The other slot of the same action map whose key is the same as slots[self]'s; -1 when the key is free.
        /// </summary>
        public static int FindConflict(IReadOnlyList<KeySlot> slots, int self)
        {
            KeySlot changed = slots[self];
            string path = changed.EffectivePath;
            if (string.IsNullOrEmpty(path))
                return -1;

            InputActionMap map = changed.Action.actionMap;
            for (int i = 0; i < slots.Count; i++)
            {
                if (i == self || !slots[i].IsValid || slots[i].Action.actionMap != map)
                    continue;

                if (string.Equals(slots[i].EffectivePath, path, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// After slots[self] got a new key: if another slot of its map has the same key, gives that slot the
        /// key slots[self] had before (oldPath). Returns the index of the swapped slot, or -1.
        /// </summary>
        public static int ResolveConflict(IReadOnlyList<KeySlot> slots, int self, string oldPath)
        {
            int other = FindConflict(slots, self);
            if (other < 0 || string.IsNullOrEmpty(oldPath))
                return -1;

            SetPath(slots[other], oldPath);
            return other;
        }

        /// <summary>Gives the slot this key; back to its default it drops the override instead, keeping the saved JSON short.</summary>
        public static void SetPath(KeySlot slot, string path)
        {
            InputBinding binding = slot.Action.bindings[slot.BindingIndex];
            if (string.Equals(binding.path, path, StringComparison.OrdinalIgnoreCase))
                slot.Action.RemoveBindingOverride(slot.BindingIndex);
            else
                slot.Action.ApplyBindingOverride(slot.BindingIndex, path);
        }
    }
}
