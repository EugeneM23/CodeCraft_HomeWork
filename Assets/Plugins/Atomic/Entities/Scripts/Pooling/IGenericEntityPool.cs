namespace Atomic.Entities
{
    public interface IGenericEntityPool : IGenericEntityPool<string>
    {
    }

    public interface IGenericEntityPool<in TKey>
    {
        IEntity Rent(TKey key);
        void Return(IEntity entity);
    }
}