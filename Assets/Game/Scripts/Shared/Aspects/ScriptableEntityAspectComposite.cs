using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// An aspect that applies a list of other aspects.
    /// </summary>
    /// <remarks>
    /// This used to be Atomic.Extensions.ScriptableEntityAspectComposite. That module was
    /// removed upstream with no replacement, but Assets/Game/Configs/Aspects/AspectComposite.asset
    /// is an instance of it, so the class is kept here. Its .cs.meta deliberately carries the
    /// old plugin file's GUID (2d50e608ad0d431b87c56ba80dad2315) and the field is still named
    /// _aspects, so the existing asset keeps deserializing.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "Scriptable Aspect Composite",
        menuName = "BuffSystem/Aspect/ScriptableAspectComposite"
    )]
    public sealed class ScriptableEntityAspectComposite : ScriptableEntityAspect
    {
        [SerializeField]
        private ScriptableEntityAspect[] _aspects;

        public override void Apply(IEntity entity)
        {
            for (int i = 0, count = _aspects.Length; i < count; i++)
                _aspects[i].Apply(entity);
        }

        public override void Discard(IEntity entity)
        {
            for (int i = 0, count = _aspects.Length; i < count; i++)
                _aspects[i].Discard(entity);
        }
    }
}
