namespace Saving.Objects
{
    /// <summary>How a Saveable saves one of its components.</summary>
    public enum SaveMode
    {
        Auto,       // its own serializer, a built-in one or its [Save] members
        AllFields,  // everything it can: SaveMembers.AllWithReferences
        Selected    // only the members picked in the Inspector: SaveMembers.Candidates by name
    }
}
