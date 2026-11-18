namespace Game
{
    /// <summary>The special module on key E (smoke launchers in milestone 1). Tank key: SpecialModule.</summary>
    public interface ISpecialModule
    {
        ModuleId Id { get; }
        bool IsReady { get; }

        /// <summary>0 right after use, 1 ready.</summary>
        float Charge01 { get; }

        /// <summary>Seconds until ready.</summary>
        float Remaining { get; }

        bool TryActivate();
    }
}
