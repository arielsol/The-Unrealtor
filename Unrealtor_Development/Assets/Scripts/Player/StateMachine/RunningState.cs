using UnityEngine;

public class RunningState : BasePlayerState
{
    private const float RUNNING_SPEED_MULTIPLIER = 1.5f;

    public RunningState(BasePlayerController controller) : base(controller)
    {
    }

    public override void OnLogic()
    {
        if (!controller.IsPlayerGrounded) return;

        Vector2 input = GetMovementInput();
        bool hasInput = input.sqrMagnitude > 0.01f;
        bool isMoving = hasInput && controller.IsPlayerGrounded;
        //MusicManager.Instance.SetMoving(isMoving, Vector3.zero);

        // Update animator locomotion for running (non-physics)
        Vector3 desiredDirection = CalculateMovementDirection(input);
        float effectiveSpeed = GetEffectiveSpeed(controller.MoveSpeed * RUNNING_SPEED_MULTIPLIER);
        Vector3 desiredVelocity = desiredDirection * effectiveSpeed;
        UpdateAnimatorLocomotionFromVelocity(desiredVelocity, effectiveSpeed);
        UpdateAnimatorGrounded(true);
    }

    public override void OnPhysicsUpdate()
    {
        if (!controller.IsPlayerGrounded) return;

        HandleGroundedLogic();
        HandleMovement();
    }

    private void HandleGroundedLogic()
    {
        // Remove any velocity component along gravity while grounded (gravity-relative, supports gravity switching)
        Vector3 velocity = controller.PlayerVelocity;
        float alongGravity = Vector3.Dot(velocity, controller.GravityDirection);
        if (alongGravity > 0f)
        {
            velocity -= controller.GravityDirection * alongGravity;
            controller.PlayerVelocity = velocity;
        }
    }

    private void HandleMovement()
    {
        Vector2 input = GetMovementInput();
        Vector3 move = CalculateMovementDirection(input);

        // Apply effective speed with running multiplier (including micro scaling)
        float effectiveSpeed = GetEffectiveSpeed(controller.MoveSpeed * RUNNING_SPEED_MULTIPLIER);
        Vector3 finalMove = move * effectiveSpeed;

        // Apply movement in physics step
        ApplyMovementPhysics(finalMove);
    }
}