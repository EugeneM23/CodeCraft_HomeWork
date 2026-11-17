namespace AIKit
{
    /// <summary>What Validate All found wrong, by kind — a test can ask for one kind.</summary>
    public enum ValidationCode
    {
        /// <summary>A character whose setup is wrong: no body, a missing component, no weapon or ability the graph uses.</summary>
        CharacterSetup
    }

    public readonly struct ValidationIssue
    {
        public readonly ValidationCode Code;
        public readonly bool IsError;
        public readonly string Message;

        /// <summary>The asset or object to select when the message is clicked.</summary>
        public readonly UnityEngine.Object Context;

        public ValidationIssue(ValidationCode code, bool isError, string message, UnityEngine.Object context)
        {
            Code = code;
            IsError = isError;
            Message = message;
            Context = context;
        }

        public override string ToString() => $"{(IsError ? "Error" : "Warning")} {Code}: {Message}";
    }
}
