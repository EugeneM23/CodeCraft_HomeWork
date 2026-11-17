namespace Loading
{
    /// <summary>Life of a LoadingOperation: Idle until Begin, Running until it succeeds or fails.</summary>
    public enum LoadingStatus
    {
        Idle,
        Running,
        Succeeded,
        Failed
    }
}
