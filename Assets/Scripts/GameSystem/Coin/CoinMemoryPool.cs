using Modules;
using UnityEngine;
using Zenject;

namespace Game
{
    public class CoinMemoryPool : MonoMemoryPool<Vector2Int, Coin>
    {
        protected override void Reinitialize(Vector2Int position, Coin item)
        {
            item.transform.position = new Vector3(position.x, position.y, 0);
        }

        protected override void OnSpawned(Coin item)
        {
            base.OnSpawned(item);
            item.gameObject.SetActive(true);
            item.Generate();
        }

        protected override void OnDespawned(Coin item)
        {
            base.OnDespawned(item);
            item.gameObject.SetActive(false);
        }
    }
}