namespace Loading
{
    /// <summary>Outcome of a whole loading run: success, or the error text of the operation that failed.</summary>
    public readonly struct LoadingResult
    {
        private LoadingResult(bool succeeded, string error)
        {
            Succeeded = succeeded;
            Error = error;
        }

        public static LoadingResult Success => new(true, null);

        public bool Succeeded { get; }

        /// <summary>Null on success.</summary>
        public string Error { get; }

        public static LoadingResult Failure(string error) => new(false, error);
    }
}
