using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Inside a building: the Go Inside block takes the character in (home to sleep, into the tavern) — nothing of it is
    /// drawn and its body is in nobody's way — and lets it out again. Only that block sets it, so what the character
    /// does is on its graph, not in a component that hides it by the name of an animation.
    /// </summary>
    public sealed partial class AICharacter
    {
        private readonly List<Renderer> _hiddenInside = new();
        private bool _isInside;

        /// <summary>The character is inside a building now (Go Inside): not drawn, in nobody's way.</summary>
        public bool IsInside
        {
            get => _isInside;
            set
            {
                if (_isInside == value)
                    return;

                _isInside = value;
                if (value)
                {
                    // What is drawn now goes out of sight; a part already hidden (a weapon on the back) is left alone
                    _hiddenInside.Clear();
                    foreach (Renderer part in EntityRoot.GetComponentsInChildren<Renderer>())
                    {
                        if (!part.enabled)
                            continue;

                        part.enabled = false;
                        _hiddenInside.Add(part);
                    }
                }
                else
                {
                    foreach (Renderer part in _hiddenInside)
                    {
                        if (part != null)
                            part.enabled = true;
                    }

                    _hiddenInside.Clear();
                }

                _body?.SetInside(value);
            }
        }
    }
}
