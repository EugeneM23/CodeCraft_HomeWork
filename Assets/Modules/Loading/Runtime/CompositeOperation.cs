using System;

namespace Loading
{
    /// <summary>
    /// A node of the loading tree: weighs as much as its children together. A failed child fails the node with the
    /// child's error, so the text of the leaf reaches the root.
    /// </summary>
    public abstract class CompositeOperation : LoadingOperation
    {
        private readonly float _weight;

        protected CompositeOperation(LoadingOperation[] children)
        {
            Children = children ?? throw new ArgumentNullException(nameof(children));
            foreach (LoadingOperation child in Children)
            {
                if (child == null)
                    throw new ArgumentNullException(nameof(children), $"{GetType().Name}: a child is null");

                _weight += child.Weight;
            }
        }

        public override float Weight => _weight;

        protected LoadingOperation[] Children { get; }

        /// <summary>Weighted mean of the children's progress; a weightless node has none until it is done.</summary>
        protected float WeightedProgress(int startedCount)
        {
            if (_weight <= 0f)
                return 0f;

            float done = 0f;
            for (int i = 0; i < startedCount && i < Children.Length; i++)
                done += Children[i].Weight * Children[i].Progress;

            return done / _weight;
        }

        /// <summary>True when the child failed and took the node down with it.</summary>
        protected bool FailIfFailed(LoadingOperation child)
        {
            if (child.Status != LoadingStatus.Failed)
                return false;

            Fail(child.Error, child.Exception);
            return true;
        }
    }
}
