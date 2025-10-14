using System;
using Gameplay;
using UnityEngine;
using IDisposable = Gameplay.IDisposable;

public class AnimationController : MonoBehaviour, IDisposable, IInitializeble
{
    private static readonly int FlyingHash = Animator.StringToHash("IsFlying");
    private static readonly int RunningHash = Animator.StringToHash("IsRunning");
    private static readonly int IdlingHash = Animator.StringToHash("IsIdling");

    [SerializeField] private Animator _animator;

    private CollisionComponent _collisionComponent;
    private Rigidbody2D _rigidbody2D;
    private InputReader _inputReader;

    [Inject]
    public void Construct(CollisionComponent collisionComponent, Rigidbody2D rigidbody2D, InputReader inputReader)
    {
        _collisionComponent = collisionComponent;
        _rigidbody2D = rigidbody2D;
        _inputReader = inputReader;
        _collisionComponent.OnFlying += OnFall;
        _inputReader.OnFire += Attack;
    }

    public void Initialize()
    {
        _collisionComponent.OnFlying += OnFall;
        _inputReader.OnFire += Attack;
    }

    public void Dispose()
    {
        _collisionComponent.OnFlying -= OnFall;
        _inputReader.OnFire -= Attack;
    }

    private void Attack()
    {
        _animator.Play("Attack");
    }

    private void OnFall() => _animator.SetTrigger("Fall");

    private void Update()
    {
        bool isFlying = !_collisionComponent.IsGrounded;
        bool isRunning = !isFlying && Mathf.Abs(_rigidbody2D.linearVelocity.x) > 1f && !isFlying;
        bool isIdling = !isFlying && !isRunning;

        _animator.SetBool(FlyingHash, isFlying);
        _animator.SetBool(RunningHash, isRunning);
        _animator.SetBool(IdlingHash, isIdling);
    }

    private void OnDestroy() => Dispose();
}