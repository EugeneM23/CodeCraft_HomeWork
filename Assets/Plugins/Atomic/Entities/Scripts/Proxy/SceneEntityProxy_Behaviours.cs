using System;
using System.Collections.Generic;

namespace Atomic.Entities
{
    public partial class SceneEntityProxy<E>
    {
        public event Action<IEntity, IEntityBehaviour> OnBehaviourAdded
        {
            add => _source.OnBehaviourAdded += value;
            remove => _source.OnBehaviourAdded -= value;
        }

        public event Action<IEntity, IEntityBehaviour> OnBehaviourDeleted
        {
            add => _source.OnBehaviourDeleted += value;
            remove => _source.OnBehaviourDeleted -= value;
        }

        public event Action<IEntity> OnBehavioursCleared
        {
            add => _source.OnBehavioursCleared += value;
            remove => _source.OnBehavioursCleared -= value;
        }

        public IReadOnlyCollection<IEntityBehaviour> Behaviours => _source.Behaviours;

        public bool AddBehaviour(in IEntityBehaviour behaviour) => _source.AddBehaviour(in behaviour);
        public bool DelBehaviour(in IEntityBehaviour behaviour) => _source.DelBehaviour(in behaviour);
        public bool HasBehaviour(in IEntityBehaviour behaviour) => _source.HasBehaviour(in behaviour);
        public bool ClearBehaviours() => _source.ClearBehaviours();
    }
}