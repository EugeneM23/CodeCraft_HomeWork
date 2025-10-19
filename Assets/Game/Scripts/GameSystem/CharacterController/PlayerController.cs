using Unity.VisualScripting;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private int moveSpeed;
    [SerializeField] private float gravity;
    [SerializeField] private float maxSlopeAngle;
    [SerializeField] private float slideMultiplier;
    [SerializeField] private float jumpForce;

    private GroundMovement groundMovement;
    private GravityComponent gravityComponent;
    private SlopeMovement slopeMovement;
    private SlopeSliding slopeSliding;
    private RotationComponent rotationComponent;
    private JumpComponent jumpComponent;
    private SpriteFlip scaleRotation;

    private Vector2 inputDir;

    private void Awake()
    {
        gravityComponent = new GravityComponent(groundLayer, gravity, maxSlopeAngle);
        groundMovement = new GroundMovement(gravityComponent);
        slopeMovement = new SlopeMovement(moveSpeed);
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

        move += groundMovement.GetOffset(gravityComponent.HasHitGround);
        move += gravityComponent.ApplyGravity(transform.position, gravityComponent.GetGroundOffset());
        move += slopeMovement.Move(inputDir, gravityComponent.HasHitGround, gravityComponent.SurfaceNormal);
        move += slopeSliding.Slide(gravityComponent.HasHitGround, inputDir, gravityComponent.SurfaceNormal);

        rotationComponent.ApplyRotation(gravityComponent.HasHitGround, gravityComponent.SurfaceNormal, transform);

        transform.position += move;
    }

    public class SpriteFlip
    {
        private readonly Transform transform;

        public SpriteFlip(Transform transform)
        {
            this.transform = transform;
        }

        public void Flip(Vector2 direction)
        {
            if (direction == Vector2.zero) return;

            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * Mathf.Sign(direction.x);
            transform.localScale = scale;
        }
    }
}