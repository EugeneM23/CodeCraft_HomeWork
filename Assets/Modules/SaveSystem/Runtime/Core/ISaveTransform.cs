namespace Saving
{
    /// <summary>One step a slot's text goes through on its way to the storage: compression, encryption.</summary>
    public interface ISaveTransform
    {
        string Id { get; }            // written into the text, so a reader knows the steps: letters and digits only
        byte[] Encode(byte[] data);
        byte[] Decode(byte[] data);   // throws for data it cannot read
    }
}
