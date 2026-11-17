using System;
using UnityEngine;

namespace ScreenNavigation
{
    /// <summary>
    /// Animation of a UIScreen appearing and disappearing. Plays from the current state, runs on unscaled time (the game
    /// may be paused) and calls done at the end; a new Play interrupts the running one, whose done is never called.
    /// Derive for tweens or an Animator; FadeTransition is the built-in one.
    /// </summary>
    public abstract class ScreenTransition : MonoBehaviour
    {
        public abstract void Play(bool show, Action done);
    }
}
