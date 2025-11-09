namespace Atomic.Entities
{
    public interface IGenericEntityFactory : IGenericEntityFactory<string>
    {
    }

    public interface IGenericEntityFactory<TKey>
    {
        GenericEntityFactory<TKey> AddInstaller(TKey key, IEntityInstaller installer);
        GenericEntityFactory<TKey> RemoveInstaller(TKey key);
        IEntity Create(TKey key);
    }
}