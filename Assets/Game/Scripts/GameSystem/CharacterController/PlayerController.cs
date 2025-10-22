using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private CapsuleCollider2D _collider;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private int _moveSpeed;
    [SerializeField] private float _gravity;
    [SerializeField] private float _maxSlopeAngle;
    [SerializeField] private float _slideMultiplier;
    [SerializeField] private float _jumpForce;

    private GroundOffsetComponent _groundOffsetComponent;
    private GravityComponent _gravityComponent;
    private MoveComponent _moveComponent;
    private SlopeSliding _slopeSliding;
    private RotationComponent _rotationComponent;
    private SpriteFlip _scaleRotation;
    private Collider2DCollisionHelper _2DCollisionHelper;
    private Vector2 _inputDir;
    private JumpComponent _jumpComponent;

    public bool IsGrounded => _gravityComponent.IsGrounded;
    public Vector2 InputDir => _inputDir;
    public Vector2 SurfaceNormal => _gravityComponent.SurfaceNormal;
    public LayerMask GroundLayer => _groundLayer;
    public CapsuleCollider2D Collider => _collider;
    public float MaxSlopeAngle => _maxSlopeAngle;
    public float Gravity => _gravity;
    public float MoveSpeed => _moveSpeed;
    public float SlideMultiplier => _slideMultiplier;

    private void Awake()
    {
        _2DCollisionHelper = new Collider2DCollisionHelper(this);
        _gravityComponent = new GravityComponent(this);
        _groundOffsetComponent = new GroundOffsetComponent(_gravityComponent);
        _moveComponent = new MoveComponent(this);
        _slopeSliding = new SlopeSliding(this, _gravityComponent);
        _rotationComponent = new RotationComponent(this);
        _scaleRotation = new SpriteFlip(this);
        _jumpComponent = new JumpComponent(this);
    }

    void Update()
    {
        _inputDir = new Vector2(Input.GetAxisRaw("Horizontal"), 0f);
        _scaleRotation.Flip(_inputDir);

        if (Input.GetKeyDown(KeyCode.Space))
            _jumpComponent.Jump(Vector2.up, _jumpForce);


        Vector3 move = Vector3.zero;

        move += _groundOffsetComponent.GetOffset();
        move += _gravityComponent.ApplyGravity(_collider.transform.position, _groundOffsetComponent.GetOffset());
        move += _moveComponent.Move();
        move += _slopeSliding.Slide();
        move += _2DCollisionHelper.GetPenetrationLayer();

        _rotationComponent.ApplyRotation(_gravityComponent.IsGrounded, _gravityComponent.SurfaceNormal, transform);

        transform.position += move;
    }

    public void AddImpulse(float force, Vector3 jumpDir)
    {
        _gravityComponent.AddImpulse(force, jumpDir);
    }
}