namespace Loading
{
    /// <summary>
    /// Runs the children together: succeeds when all have, fails with the first failed child (the others are not
    /// ticked any more). Empty — succeeds at once.
    /// </summary>
    public sealed class ParallelOperation : CompositeOperation
    {
        public ParallelOperation(params LoadingOperation[] children) : base(children)
        {
        }

        protected override void OnBegin()
        {
            foreach (LoadingOperation child in Children)
            {
                child.Begin();
                if (FailIfFailed(child))
                    return;
            }

            SucceedIfAllDone();
        }

        protected override void OnTick(float dt)
        {
            foreach (LoadingOperation child in Children)
            {
                child.Tick(dt);
                if (FailIfFailed(child))
                    return;
            }

            SucceedIfAllDone();
        }

        protected override float MeasureProgress() => WeightedProgress(Children.Length);

        private void SucceedIfAllDone()
        {
            foreach (LoadingOperation child in Children)
            {
                if (child.Status != LoadingStatus.Succeeded)
                    return;
            }

            Succeed();
        }
    }
}
