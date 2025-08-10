using System;
using System.Collections;
using System.Collections.Generic;
using Modules;

namespace Game
{
    public class CoinManager : IEnumerable<Coin>
    {
        public event Action OnlevelComplited;

        private readonly CoinMemoryPool _coinMemoryPool;

        private List<Coin> _coins = new();

        public CoinManager(CoinMemoryPool coinMemoryPool)
        {
            _coinMemoryPool = coinMemoryPool;
        }

        public void Add(Coin coin) => _coins.Add(coin);

        public void Remove(Coin item)
        {
            _coins.Remove(item);
            _coinMemoryPool.Despawn(item);

            if (_coins.Count == 0)
                OnlevelComplited?.Invoke();
        }

        public IEnumerator<Coin> GetEnumerator()
        {
            return _coins.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}