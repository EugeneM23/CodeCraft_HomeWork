using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The level's feed of enemy deaths (see IEnemyDeaths). Listeners are kept in a list and called one by one by
    /// index: no allocation per death (a mass crush reports dozens in a frame), and a throwing listener is logged
    /// while the others still hear the death. A listener removed during a report is skipped from then on; one added
    /// during a report hears the next death.
    /// </summary>
    public sealed class EnemyDeaths : IEnemyDeaths
    {
        private readonly List<Action<EnemyDeath>> _listeners = new();
        private int _reporting;
        private bool _hasHoles;

        public event Action<EnemyDeath> Died
        {
            add
            {
                if (value != null)
                    _listeners.Add(value);
            }
            remove
            {
                int index = _listeners.LastIndexOf(value);
                if (index < 0)
                    return;

                if (_reporting > 0)
                {
                    _listeners[index] = null;
                    _hasHoles = true;
                }
                else
                {
                    _listeners.RemoveAt(index);
                }
            }
        }

        public void Report(in EnemyDeath death)
        {
            _reporting++;
            try
            {
                int count = _listeners.Count;
                for (int i = 0; i < count; i++)
                {
                    Action<EnemyDeath> listener = _listeners[i];
                    if (listener == null)
                        continue;

                    try
                    {
                        listener(death);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }
            }
            finally
            {
                if (--_reporting == 0 && _hasHoles)
                {
                    _listeners.RemoveAll(static listener => listener == null);
                    _hasHoles = false;
                }
            }
        }
    }
}
