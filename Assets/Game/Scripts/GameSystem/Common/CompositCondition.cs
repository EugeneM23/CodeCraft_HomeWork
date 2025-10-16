using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay
{
    public class CompositCondition
    {
        protected List<Func<bool>> _conditions = new();

        public void AddCondition(Func<bool> condition)
        {
            _conditions.Add(condition);
        }

        public void RemoveCondition(Func<bool> condition) => _conditions.Remove(condition);

        public bool IsTrue()
        {
            for (int i = 0; i < _conditions.Count; i++)
                if (_conditions[i].Invoke())
                    return true;

            return false;
        }
    }
}