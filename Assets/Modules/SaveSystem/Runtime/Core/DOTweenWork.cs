using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Saving
{
    /// <summary>
    /// DOTween tweens the game waits for: playing, with an OnComplete callback, not looping for ever. Found by
    /// reflection, so the module does not depend on DOTween; without it (or with its members stripped from a build)
    /// nothing is ever busy. A looping tween, a paused one and one without a callback (a counter on a label) do not
    /// hold a save back. A tween with the id <see cref="SaveLoadSystem.WORK_SAVED_BY_GAME"/> is saved by the game itself
    /// (it writes where the flight is and starts it again after a load): it does not hold a save back and is not
    /// finished early, but a load still drops it — the game starts it again from the save.
    /// </summary>
    internal sealed class DOTweenWork : IWorkInFlight
    {
        private const BindingFlags STATIC = BindingFlags.Public | BindingFlags.Static;
        private const BindingFlags INSTANCE = BindingFlags.Public | BindingFlags.Instance;

        private static bool _looked;
        private static MethodInfo _playingTweens;
        private static Type _listType;
        private static FieldInfo _onComplete;
        private static FieldInfo _id;
        private static FieldInfo _stringId;
        private static MethodInfo _loops;
        private static MethodInfo _complete;
        private static MethodInfo _kill;

        public bool IsBusy => Awaited(includeSavedByGame: false).Count > 0;

        public void Finish()
        {
            foreach (object tween in Awaited(includeSavedByGame: false))
                Call(_complete, tween, true);
        }

        public void Drop()
        {
            foreach (object tween in Awaited(includeSavedByGame: true))
                Call(_kill, tween, false);
        }

        private static List<object> Awaited(bool includeSavedByGame)
        {
            var awaited = new List<object>();
            if (!Find())
                return awaited;

            IList playing;
            try
            {
                playing = _playingTweens.Invoke(null, new[] { Activator.CreateInstance(_listType) }) as IList;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return awaited;
            }

            if (playing == null)
                return awaited;

            foreach (object tween in playing)
            {
                if (tween == null || _onComplete.GetValue(tween) == null)
                    continue;

                if (!includeSavedByGame && IsSavedByGame(tween))
                    continue;

                if (_loops.Invoke(null, new[] { tween }) is int loops && loops < 0)
                    continue; // never ends

                awaited.Add(tween);
            }

            return awaited;
        }

        // SetId(string) keeps the id in stringId; older DOTween versions keep any id in id
        private static bool IsSavedByGame(object tween) =>
            SaveLoadSystem.WORK_SAVED_BY_GAME.Equals(_id.GetValue(tween))
            || (_stringId != null && SaveLoadSystem.WORK_SAVED_BY_GAME.Equals(_stringId.GetValue(tween)));

        private static void Call(MethodInfo method, object tween, bool flag)
        {
            try
            {
                method.Invoke(null, new[] { tween, flag });
            }
            catch (TargetInvocationException exception)
            {
                // The game's own callback threw: the rest still ends
                Debug.LogException(exception.InnerException ?? exception);
            }
        }

        private static bool Find()
        {
            if (_looked)
                return _playingTweens != null;

            _looked = true;
            Type dotween = Type.GetType("DG.Tweening.DOTween, DOTween");
            Type tween = Type.GetType("DG.Tweening.Tween, DOTween");
            Type extensions = Type.GetType("DG.Tweening.TweenExtensions, DOTween");
            if (dotween == null || tween == null || extensions == null)
                return false;

            Type list = typeof(List<>).MakeGenericType(tween);
            MethodInfo playingTweens = dotween.GetMethod("PlayingTweens", STATIC, null, new[] { list }, null);
            FieldInfo onComplete = tween.GetField("onComplete", INSTANCE);
            FieldInfo id = tween.GetField("id", INSTANCE);
            MethodInfo loops = extensions.GetMethod("Loops", STATIC, null, new[] { tween }, null);
            MethodInfo complete = extensions.GetMethod("Complete", STATIC, null, new[] { tween, typeof(bool) }, null);
            MethodInfo kill = extensions.GetMethod("Kill", STATIC, null, new[] { tween, typeof(bool) }, null);
            if (playingTweens == null || onComplete == null || id == null || loops == null || complete == null || kill == null)
                return false; // another DOTween version, or members stripped from a build: nothing waits

            _listType = list;
            _onComplete = onComplete;
            _id = id;
            _stringId = tween.GetField("stringId", INSTANCE); // absent in older versions
            _loops = loops;
            _complete = complete;
            _kill = kill;
            _playingTweens = playingTweens;
            return true;
        }
    }
}
