using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private Collider2D _collider;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private int _moveSpeed;
    [SerializeField] private float _gravity;
    [SerializeField] private float _maxSlopeAngle;
    [SerializeField] private float _slideMultiplier;
    [SerializeField] private float _jumpForce;

    private GroundOffsetComponent _groundOffsetComponent;
    private GravityComponent _gravityComponent;
    private MoveComponent _moveComponent;
    private SlopeSliding slopeSliding;
    private RotationComponent rotationComponent;
    private SpriteFlip scaleRotation;
    private Collider2DCollisionHelper _2DCollisionHelper;
    private Vector2 _inputDir;

    public bool IsGrounded => _gravityComponent.IsGrounded;
    public Vector2 InputDir => _inputDir;
    public Vector2 SurfaceNormal => _gravityComponent.SurfaceNormal;
    public LayerMask GroundLayer => _groundLayer;
    public Collider2D Collider => _collider;
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
        slopeSliding = new SlopeSliding(this, _gravityComponent);
        rotationComponent = new RotationComponent(this);
        scaleRotation = new SpriteFlip(this);
    }

    void Update()
    {
        _inputDir = new Vector2(Input.GetAxisRaw("Horizontal"), 0f);
        scaleRotation.Flip(_inputDir);

        if (Input.GetKeyDown(KeyCode.Space))
            AddImpulse(_jumpForce, Vector2.up);


        Vector3 move = Vector3.zero;

        move += _groundOffsetComponent.GetOffset();
        move += _gravityComponent.ApplyGravity(transform.position, _groundOffsetComponent.GetOffset());
        move += _moveComponent.Move();
        move += slopeSliding.Slide();
        move += _2DCollisionHelper.GetPenetrationLayer();

        rotationComponent.ApplyRotation(_gravityComponent.IsGrounded, _gravityComponent.SurfaceNormal, transform);

        transform.position += move;
    }

    public void AddImpulse(float force, Vector3 jumpDir)
    {
        _gravityComponent.AddImpulse(force, jumpDir);
    }
}