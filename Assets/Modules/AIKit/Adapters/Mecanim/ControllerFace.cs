using UnityEngine;

namespace AIKit.Mecanim
{
    /// <summary>
    /// Where the AI Animator's controller plays and how it is driven: the Animator itself (the usual), or a graph that
    /// plays the controller inside it — something else put in front of the Animator (a bridge supplies its own). The AI
    /// Animator only talks to its face; it knows nothing of what is behind it.
    /// </summary>
    public interface IControllerFace
    {
        /// <summary>The controller that plays (its override included); setting it puts another one in its place.</summary>
        RuntimeAnimatorController Controller { get; set; }

        int LayerCount { get; }

        string GetLayerName(int layer);

        int GetLayerIndex(string layer);

        /// <summary>The controller has a parameter of this name and kind.</summary>
        bool HasParameter(string parameter, AnimatorControllerParameterType type);

        void SetFloat(int id, float value);

        void SetBool(int id, bool value);

        void SetInteger(int id, int value);

        void SetTrigger(int id);

        void ResetTrigger(int id);

        bool HasState(int layer, int stateHash);

        void CrossFadeInFixedTime(int stateHash, float duration, int layer, float offset = 0f);

        AnimatorStateInfo GetCurrentAnimatorStateInfo(int layer);

        AnimatorStateInfo GetNextAnimatorStateInfo(int layer);

        bool IsInTransition(int layer);
    }

    /// <summary>The usual face: the Animator itself.</summary>
    public sealed class AnimatorFace : IControllerFace
    {
        private readonly Animator _animator;

        public AnimatorFace(Animator animator) => _animator = animator;

        public RuntimeAnimatorController Controller
        {
            get => _animator != null ? _animator.runtimeAnimatorController : null;
            set
            {
                if (_animator != null)
                    _animator.runtimeAnimatorController = value;
            }
        }

        public int LayerCount => _animator != null && Controller != null ? _animator.layerCount : 0;

        public string GetLayerName(int layer) => _animator.GetLayerName(layer);

        public int GetLayerIndex(string layer) => _animator.GetLayerIndex(layer);

        public bool HasParameter(string parameter, AnimatorControllerParameterType type) =>
            AIAnimator.HasParameter(_animator, parameter, type);

        public void SetFloat(int id, float value) => _animator.SetFloat(id, value);

        public void SetBool(int id, bool value) => _animator.SetBool(id, value);

        public void SetInteger(int id, int value) => _animator.SetInteger(id, value);

        public void SetTrigger(int id) => _animator.SetTrigger(id);

        public void ResetTrigger(int id) => _animator.ResetTrigger(id);

        public bool HasState(int layer, int stateHash) => _animator.HasState(layer, stateHash);

        public void CrossFadeInFixedTime(int stateHash, float duration, int layer, float offset = 0f) =>
            _animator.CrossFadeInFixedTime(stateHash, duration, layer, offset);

        public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layer) => _animator.GetCurrentAnimatorStateInfo(layer);

        public AnimatorStateInfo GetNextAnimatorStateInfo(int layer) => _animator.GetNextAnimatorStateInfo(layer);

        public bool IsInTransition(int layer) => _animator.IsInTransition(layer);
    }
}
