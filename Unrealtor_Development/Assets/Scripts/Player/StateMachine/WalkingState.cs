using UnityEngine;

public class WalkingState : BasePlayerState
{
    public WalkingState(BasePlayerController controller) : base(controller)
    {
    }

    public override void OnLogic()
    {
        // Update animator speed for walking (non-physics)
        // Drive movement audio based on input intent
        Vector2 input = GetMovementInput();
        bool hasInput = input.sqrMagnitude > 0.01f;
        bool isMoving = hasInput && controller.IsPlayerGrounded;
        //MusicManager.Instance.SetMoving(isMoving, Vector3.zero);
        
        if (!controller.IsPlayerGrounded)
        return; 


        Vector3 desiredDirection = CalculateMovementDirection(input);
        float effectiveSpeed = GetEffectiveSpeed(controller.MoveSpeed);
        Vector3 desiredVelocity = desiredDirection * effectiveSpeed;
        UpdateAnimatorLocomotionFromVelocity(desiredVelocity, effectiveSpeed);
        UpdateAnimatorGrounded(true);

        
        
    }

    public override void OnPhysicsUpdate()
    {
        if (!controller.IsPlayerGrounded)
        return;

        HandleGroundedLogic();
        HandleMovement();
    }

    private void HandleGroundedLogic()
    {
        // Remove any velocity component along gravity while grounded (gravity-relative, supports gravity switching)
        Vector3 velocity = controller.PlayerVelocity;
        float alongGravity = Vector3.Dot(velocity, controller.GravityDirection);
        if (alongGravity >= 0f)
        {
            velocity -= controller.GravityDirection * alongGravity;
            controller.PlayerVelocity = velocity;
        }
    }
    private void HandleMovement()
    {
        Vector2 input = GetMovementInput();
        Vector3 move = CalculateMovementDirection(input);
        float effectiveSpeed = GetEffectiveSpeed(controller.MoveSpeed);
        Vector3 finalMove = move * effectiveSpeed;
        ApplyMovementPhysics(finalMove);
    }
}