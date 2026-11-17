namespace Loading
{
    /// <summary>
    /// Runs the children one after another. Instant children pass in a chain within the same call; the first failed
    /// child stops the sequence, the rest are not begun. Empty — succeeds at once.
    /// </summary>
    public sealed class SequenceOperation : CompositeOperation
    {
        private int _current;

        public SequenceOperation(params LoadingOperation[] children) : base(children)
        {
        }

        protected override void OnBegin()
        {
            _current = -1;
            MoveNext();
        }

        protected override void OnTick(float dt)
        {
            LoadingOperation child = Children[_current];
            child.Tick(dt);

            if (child.Status == LoadingStatus.Succeeded)
                MoveNext();
            else
                FailIfFailed(child);
        }

        // Children after the current one may still hold the state of a previous run: they count as not started
        protected override float MeasureProgress() => WeightedProgress(_current + 1);

        private void MoveNext()
        {
            while (true)
            {
                _current++;
                if (_current >= Children.Length)
                {
                    Succeed();
                    return;
                }

                LoadingOperation child = Children[_current];
                child.Begin();

                if (child.Status != LoadingStatus.Succeeded)
                {
                    FailIfFailed(child);
                    return;
                }
            }
        }
    }
}
