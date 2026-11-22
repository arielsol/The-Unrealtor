using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class CharacterMovementController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Animation Settings")]
    [SerializeField] private float animationDampTime = 0.1f;

    [Header("Mouse Rotation")]
    [Tooltip("Mouse sensitivity for horizontal rotation")]
    [SerializeField] private float mouseSensitivity = 2f;
    [Tooltip("Enable smooth rotation instead of instant snap")]
    [SerializeField] private bool smoothRotation = true;
    [Tooltip("How fast the character rotates to face the target direction")]
    [SerializeField] private float rotationSmoothSpeed = 10f;

    // TODO: Re-enable Foot IK when animation blend tree is ready
    // [Header("Turn-In-Place Integration")]
    // [Tooltip("Reference to FootIKSystem for TIP coordination")]
    // [SerializeField] private FootIKSystem footIKSystem;

    private CharacterController controller;
    private Animator animator;
    private Vector3 velocity;

    // Animator parameter hashes for performance
    private static readonly int InputXHash = Animator.StringToHash("InputX");
    private static readonly int InputYHash = Animator.StringToHash("InputY");

    // Rotation state
    private float targetYRotation;
    private float currentYRotation;
    private float rotationVelocity;

    // Public properties for TIP system integration
    public float TargetYawRotation => targetYRotation;
    public float CurrentYawRotation => currentYRotation;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        // Initialize rotation to current facing direction
        currentYRotation = transform.eulerAngles.y;
        targetYRotation = currentYRotation;

        // Lock cursor for mouse look
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleRotation();
        HandleMovement();
        HandleAnimator();
    }

    private void HandleRotation()
    {
        // Get mouse input
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;

        // Accumulate target rotation (always track where player WANTS to face)
        targetYRotation += mouseX;

        // TODO: Re-enable when Foot IK animation is ready
        // // Check if TIP system wants to control rotation
        // // When TIP is active, don't rotate root - let TIP handle stepping first
        // if (footIKSystem != null && footIKSystem.IsTIPActive)
        // {
        //     // Don't rotate root - TIP will apply rotation after feet have stepped
        //     // Just keep tracking target for when TIP releases
        //     return;
        // }

        // Normal rotation (when moving or TIP not active)
        if (smoothRotation)
        {
            // Smooth rotation using SmoothDamp
            currentYRotation = Mathf.SmoothDampAngle(
                currentYRotation,
                targetYRotation,
                ref rotationVelocity,
                1f / rotationSmoothSpeed
            );
        }
        else
        {
            // Instant rotation
            currentYRotation = targetYRotation;
        }

        // Apply rotation to character
        transform.rotation = Quaternion.Euler(0f, currentYRotation, 0f);
    }

    /// <summary>
    /// Called by FootIKSystem when TIP stepping completes to sync rotation.
    /// </summary>
    public void SyncRotationFromTIP(float newYawRotation)
    {
        currentYRotation = newYawRotation;
        targetYRotation = newYawRotation;
        transform.rotation = Quaternion.Euler(0f, currentYRotation, 0f);
    }

    private void HandleMovement()
    {
        // Get input
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        // Calculate movement direction relative to character's forward
        Vector3 moveDirection = transform.right * horizontal + transform.forward * vertical;
        moveDirection = Vector3.ClampMagnitude(moveDirection, 1f);

        // Apply movement
        controller.Move(moveDirection * moveSpeed * Time.deltaTime);

        // Apply gravity
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Small downward force to keep grounded
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void HandleAnimator()
    {
        // Get input values
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        // Update animator parameters with smooth damping
        animator.SetFloat(InputXHash, horizontal, animationDampTime, Time.deltaTime);
        animator.SetFloat(InputYHash, vertical, animationDampTime, Time.deltaTime);
    }
}
