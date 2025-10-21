using Unity.VisualScripting;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private Collider2D _collider;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private int moveSpeed;
    [SerializeField] private float gravity;
    [SerializeField] private float maxSlopeAngle;
    [SerializeField] private float slideMultiplier;
    [SerializeField] private float jumpForce;

    private GroundOffsetComponent _groundOffsetComponent;
    private GravityComponent gravityComponent;
    private MoveComponent _moveComponent;
    private SlopeSliding slopeSliding;
    private RotationComponent rotationComponent;
    private JumpComponent jumpComponent;
    private SpriteFlip scaleRotation;

    private Vector2 inputDir;

    private void Awake()
    {
        gravityComponent = new GravityComponent(groundLayer, gravity, maxSlopeAngle);
        _groundOffsetComponent = new GroundOffsetComponent(gravityComponent);
        _moveComponent = new MoveComponent(moveSpeed);
        slopeSliding = new SlopeSliding(slideMultiplier);
        rotationComponent = new RotationComponent(transform.rotation);
        jumpComponent = new JumpComponent(jumpForce, gravityComponent);
        scaleRotation = new SpriteFlip(transform);
    }

    void Update()
    {
        inputDir = new Vector2(Input.GetAxisRaw("Horizontal"), 0f);
        scaleRotation.Flip(inputDir);

        if (Input.GetKeyDown(KeyCode.Space))
            jumpComponent.RequestJump();

        Vector3 move = Vector3.zero;

        if (_collider.GetPenetrationLayer(groundLayer, out Vector2 correction))
        {
            var delta = Vector3.Lerp(Vector3.zero, correction, 0.1f);
            move += delta;
        }

        move += _groundOffsetComponent.GetOffset(gravityComponent.HasHitGround);
        move += gravityComponent.ApplyGravity(transform.position, gravityComponent.GetGroundOffset());
        move += _moveComponent.Move(inputDir, gravityComponent.HasHitGround, gravityComponent.SurfaceNormal, _collider, groundLayer);
        move += slopeSliding.Slide(gravityComponent.HasHitGround, inputDir, gravityComponent.SurfaceNormal);

        rotationComponent.ApplyRotation(gravityComponent.HasHitGround, gravityComponent.SurfaceNormal, transform);

        transform.position += move;
    }
}