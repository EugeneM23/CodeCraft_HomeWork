using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Modules.Gameplay
{
    [Serializable]
    public sealed class Ammo
    {
        public event Action OnStateChanged;

        [SerializeField, Min(0)]
        private int count;

        public Ammo(int count)
        {
            this.count = count;
        }

        public int GetCount()
        {
            return this.count;
        }

        public bool Exists()
        {
            return this.count > 0;
        }

        public void Add(int charges)
        {
            this.count += charges;
            this.OnStateChanged?.Invoke();
        }

        public void Spend()
        {
            if (this.count > 0)
            {
                this.count--;
                this.OnStateChanged?.Invoke();
            }
        }
    }
}