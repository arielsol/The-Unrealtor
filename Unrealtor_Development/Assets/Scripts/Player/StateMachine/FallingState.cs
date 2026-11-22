using UnityEngine;

public class FallingState : BasePlayerState
{
    public FallingState(BasePlayerController controller) : base(controller)
    {
    }

    public override void OnEnter()
    {
        // Update animator for falling state
        UpdateAnimatorGrounded(false);
    }

    public override void OnLogic()
    {
        MusicManager.Instance.SetMoving(false, Vector3.zero);

        // Update directional locomotion during air movement (non-physics)
        Vector2 input = GetMovementInput();
        Vector3 desiredDirection = CalculateMovementDirection(input);
        float effectiveSpeed = GetEffectiveSpeed(controller.MoveSpeed);
        Vector3 desiredVelocity = desiredDirection * effectiveSpeed;
        UpdateAnimatorLocomotionFromVelocity(desiredVelocity, effectiveSpeed);
    }

    public override void OnPhysicsUpdate()
    {
        ApplyGravity();
        HandleAirMovement();
    }

    private void ApplyGravity()
    {
        Vector3 velocity = controller.PlayerVelocity;
        Vector3 gravityForce = controller.GravityDirection * controller.GravityValue * Time.fixedDeltaTime;
        velocity += gravityForce;
        controller.PlayerVelocity = velocity;
    }

    private void HandleAirMovement()
    {
        Vector2 input = GetMovementInput();
        Vector3 move = CalculateMovementDirection(input);

        // Apply effective speed (including micro scaling)
        float effectiveSpeed = GetEffectiveSpeed(controller.MoveSpeed);

        // Combine movement with gravity-based velocity
        Vector3 gravityVelocity = Vector3.Project(controller.PlayerVelocity, controller.GravityDirection);
        Vector3 finalMove = (move * effectiveSpeed) + gravityVelocity;
        ApplyMovementPhysics(finalMove);
    }
}