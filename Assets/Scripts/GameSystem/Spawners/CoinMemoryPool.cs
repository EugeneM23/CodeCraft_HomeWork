using Modules;
using Zenject;

namespace Game
{
    public class CoinMemoryPool : MonoMemoryPool<Coin>
    {
        protected override void OnSpawned(Coin item)
        {
            base.OnSpawned(item);
            item.gameObject.SetActive(true);
        }

        protected override void OnDespawned(Coin item)
        {
            base.OnDespawned(item);
            item.gameObject.SetActive(false);
        }
    }
}