using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "AnimSet", menuName = "Game/AnimSet")]
    public class AnimSet : ScriptableObject
    {
        [SerializeField] private AnimationClip _idle;
        [SerializeField] private AnimationClip _run;
    }
}