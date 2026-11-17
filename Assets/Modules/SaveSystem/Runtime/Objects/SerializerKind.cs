namespace Saving.Objects
{
    /// <summary>Who saves a component type: what the Saveable inspector shows next to it.</summary>
    public enum SerializerKind
    {
        None,     // nothing to save
        Own,      // a ComponentSerializer of the game
        BuiltIn,  // one of the plugin's serializers
        Fields    // [Save] fields
    }
}
