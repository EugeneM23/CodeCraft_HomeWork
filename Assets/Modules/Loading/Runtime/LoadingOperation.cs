using System;

namespace Loading
{
    /// <summary>
    /// One step of a loading — a leaf, or a node made of other operations (SequenceOperation, ParallelOperation).
    /// The owner calls Begin once and then Tick every frame until IsDone. A subclass does its work in OnBegin/OnTick,
    /// reports progress through MeasureProgress and ends with Succeed or Fail; an exception thrown from OnBegin or
    /// OnTick fails the operation instead of escaping. Weight is the operation's share of the parent's progress.
    /// </summary>
    public abstract class LoadingOperation
    {
        private readonly float _weight;

        protected LoadingOperation(float weight = 1f)
        {
            _weight = weight > 0f ? weight : 0f;
        }

        public virtual float Weight => _weight;
        public LoadingStatus Status { get; private set; }
        public bool IsDone => Status == LoadingStatus.Succeeded || Status == LoadingStatus.Failed;

        /// <summary>Why the operation failed; null until it does.</summary>
        public string Error { get; private set; }

        /// <summary>The exception that failed the operation, if it failed by one.</summary>
        public Exception Exception { get; private set; }

        /// <summary>0..1: 0 before Begin, 1 once succeeded, MeasureProgress in between.</summary>
        public float Progress => Status switch
        {
            LoadingStatus.Idle => 0f,
            LoadingStatus.Succeeded => 1f,
            _ => Clamp01(MeasureProgress())
        };

        /// <summary>Starts the operation from scratch; a finished one may be begun again.</summary>
        public void Begin()
        {
            if (Status == LoadingStatus.Running)
                throw new InvalidOperationException($"{GetType().Name} is already running");

            Status = LoadingStatus.Running;
            Error = null;
            Exception = null;

            try
            {
                OnBegin();
            }
            catch (Exception exception)
            {
                FailBy(exception);
            }
        }

        /// <summary>Advances a running operation by dt seconds; does nothing otherwise.</summary>
        public void Tick(float dt)
        {
            if (Status != LoadingStatus.Running)
                return;

            try
            {
                OnTick(dt);
            }
            catch (Exception exception)
            {
                FailBy(exception);
            }
        }

        /// <summary>Starts the work; may finish at once with Succeed or Fail.</summary>
        protected abstract void OnBegin();

        protected virtual void OnTick(float dt)
        {
        }

        /// <summary>Progress of the running operation; clamped to 0..1 by Progress.</summary>
        protected virtual float MeasureProgress() => 0f;

        /// <summary>Ignored unless running: a failed operation does not come back to life.</summary>
        protected void Succeed()
        {
            if (Status == LoadingStatus.Running)
                Status = LoadingStatus.Succeeded;
        }

        /// <summary>Ignored unless running.</summary>
        protected void Fail(string error, Exception exception = null)
        {
            if (Status != LoadingStatus.Running)
                return;

            Status = LoadingStatus.Failed;
            Error = string.IsNullOrEmpty(error) ? $"{GetType().Name} failed" : error;
            Exception = exception;
        }

        private void FailBy(Exception exception) =>
            Fail($"{GetType().Name}: {exception.GetType().Name}: {exception.Message}", exception);

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
