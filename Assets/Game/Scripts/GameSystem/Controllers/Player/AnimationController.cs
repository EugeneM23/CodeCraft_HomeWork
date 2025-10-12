using Gameplay;
using UnityEngine;

public class AnimationController : MonoBehaviour
{
    private static readonly int FlyingHash = Animator.StringToHash("IsFlying");
    private static readonly int RunningHash = Animator.StringToHash("IsRunning");
    private static readonly int IdlingHash = Animator.StringToHash("IsIdling");

    [SerializeField] private Animator _animator;
    private CollisionComponent _collisionComponent;
    private Rigidbody2D _rigidbody2D;
    private string _test;

    [Inject]
    public void Construct(CollisionComponent collisionComponent, Rigidbody2D rigidbody2D)
    {
        _collisionComponent = collisionComponent;
        _rigidbody2D = rigidbody2D;
    }

    private void OnEnable()
    {
        _collisionComponent.OnFlying += OnFall;
    }

    private void OnFall()
    {
        _animator.SetTrigger("Fall");
    }

    private void Update()
    {
        _test = Time.frameCount.ToString();
        bool isFlying = !_collisionComponent.IsGrounded;
        bool isRunning = !isFlying && Mathf.Abs(_rigidbody2D.linearVelocity.x) > 1f && !isFlying;
        bool isIdling = !isFlying && !isRunning;

        _animator.SetBool(FlyingHash, isFlying);
        _animator.SetBool(RunningHash, isRunning);
        _animator.SetBool(IdlingHash, isIdling);
    }
}