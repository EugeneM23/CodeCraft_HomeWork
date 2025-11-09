namespace Atomic.Contexts
{
    public partial class Context : IContext
    {
        public string Name
        {
            get { return this.name; }
            set { this.name = value; }
        }

        public IContext Parent
        {
            get { return this.parent; }
            set { this.parent = value; }
        }

        private readonly IContext owner;
        private string name;
        private IContext parent;

        public Context(string name = null, IContext parent = null, IContext owner = null)
        {
            this.name = name;
            this.parent = parent;
            this.owner = owner ?? this;
        }

        public void Clear()
        {
            this.ClearValues();
            this.ClearControllers();
        }

        public void UnsubscribeAll()
        {
            DelegateUtils.Unsubscribe(ref this.OnInitialized);
            DelegateUtils.Unsubscribe(ref this.OnEnabled);
            DelegateUtils.Unsubscribe(ref this.OnDisabled);
            DelegateUtils.Unsubscribe(ref this.OnUpdated);
            DelegateUtils.Unsubscribe(ref this.OnFixedUpdated);
            DelegateUtils.Unsubscribe(ref this.OnLateUpdated);
            DelegateUtils.Unsubscribe(ref this.OnDisposed);
        }
    }
}