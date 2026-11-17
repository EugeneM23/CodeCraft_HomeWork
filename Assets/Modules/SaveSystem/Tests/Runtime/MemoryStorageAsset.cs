namespace Saving.Tests
{
    /// <summary>A custom storage asset that hands out one in-memory storage the test can look into.</summary>
    public sealed class MemoryStorageAsset : SaveStorageAsset
    {
        public InMemorySaveStorage Storage { get; } = new();

        public override ISaveStorage CreateStorage() => Storage;
    }
}
