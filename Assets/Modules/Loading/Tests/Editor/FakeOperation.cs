using System;
using System.Collections.Generic;

namespace Loading.Tests
{
    /// <summary>A leaf driven by hand: the test finishes it, sets its progress or makes it throw.</summary>
    internal sealed class FakeOperation : LoadingOperation
    {
        private readonly List<string> _log;
        private readonly string _name;

        public FakeOperation(float weight = 1f, List<string> log = null, string name = null) : base(weight)
        {
            _log = log;
            _name = name;
        }

        public float Measured { get; set; }
        public bool SucceedOnBegin { get; set; }
        public string FailOnBegin { get; set; }
        public Exception ThrowOnBegin { get; set; }
        public Exception ThrowOnTick { get; set; }
        public int Ticks { get; private set; }
        public float TickedTime { get; private set; }

        public void Finish() => Succeed();

        public void Break(string error) => Fail(error);

        protected override void OnBegin()
        {
            _log?.Add($"begin {_name}");
            Ticks = 0;
            TickedTime = 0f;

            if (ThrowOnBegin != null)
                throw ThrowOnBegin;
            if (FailOnBegin != null)
                Fail(FailOnBegin);
            else if (SucceedOnBegin)
                Succeed();
        }

        protected override void OnTick(float dt)
        {
            Ticks++;
            TickedTime += dt;
            _log?.Add($"tick {_name}");

            if (ThrowOnTick != null)
                throw ThrowOnTick;
        }

        protected override float MeasureProgress() => Measured;
    }
}
