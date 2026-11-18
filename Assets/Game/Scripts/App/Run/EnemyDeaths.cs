using System;
using UnityEngine;

namespace Game
{
    /// <summary>The level's feed of enemy deaths (see IEnemyDeaths). A throwing listener is logged; the others still hear the death.</summary>
    public sealed class EnemyDeaths : IEnemyDeaths
    {
        private Action<EnemyDeath> _died;

        public event Action<EnemyDeath> Died
        {
            add => _died += value;
            remove => _died -= value;
        }

        public void Report(in EnemyDeath death)
        {
            Action<EnemyDeath> died = _died;
            if (died == null)
                return;

            // One by one: a broken listener (a HUD gone with its scene) must not cost the others the death
            Delegate[] listeners = died.GetInvocationList();
            for (int i = 0; i < listeners.Length; i++)
            {
                try
                {
                    ((Action<EnemyDeath>)listeners[i])(death);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}
