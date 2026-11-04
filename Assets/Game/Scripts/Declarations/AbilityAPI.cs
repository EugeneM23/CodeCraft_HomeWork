using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Ability API definition. Ability derives from Atomic.Entities.Entity, so it is a valid
    /// first generic argument for ValueKey/TagKey.
    /// </summary>
    [EntityExtensionsAPI]
    public static partial class AbilityAPI
    {
        #region Tags

        public static readonly TagKey<Ability> BaseAbility = new(nameof(BaseAbility));
        public static readonly TagKey<Ability> PointAbility = new(nameof(PointAbility));
        public static readonly TagKey<Ability> TargetAbility = new(nameof(TargetAbility));

        #endregion

        #region Base

        public static readonly ValueKey<Ability, IAction> BaseAction = new(nameof(BaseAction));
        public static readonly ValueKey<Ability, IFunction<bool>> BaseCondition = new(nameof(BaseCondition));
        public static readonly ValueKey<Ability, IEvent> BaseEvent = new(nameof(BaseEvent));
        public static readonly ValueKey<Ability, IReactiveVariable<int>> Charges = new(nameof(Charges));
        public static readonly ValueKey<Ability, IValue<int>> ManaCost = new(nameof(ManaCost));
        public static readonly ValueKey<Ability, IValue<float>> Radius = new(nameof(Radius));

        #endregion

        #region Point

        public static readonly ValueKey<Ability, IAction<Vector3>> PointAction = new(nameof(PointAction));
        public static readonly ValueKey<Ability, IFunction<Vector3, bool>> PointCondition = new(nameof(PointCondition));
        public static readonly ValueKey<Ability, IEvent<Vector3>> PointEvent = new(nameof(PointEvent));

        #endregion

        #region Target

        public static readonly ValueKey<Ability, IAction<IEntity>> TargetAction = new(nameof(TargetAction));
        public static readonly ValueKey<Ability, IFunction<IEntity, bool>> TargetCondition = new(nameof(TargetCondition));
        public static readonly ValueKey<Ability, IEvent<IEntity>> TargetEvent = new(nameof(TargetEvent));

        #endregion
    }
}
