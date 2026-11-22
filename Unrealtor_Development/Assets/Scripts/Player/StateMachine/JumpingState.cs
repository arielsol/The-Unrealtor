using UnityEngine;

public class JumpingState : BasePlayerState
{
    public JumpingState(BasePlayerController controller) : base(controller)
    {
    }

    public override void OnEnter()
    {
        // Apply jump velocity when entering this state
        Vector3 velocity = controller.PlayerVelocity;
        float jumpSpeed = Mathf.Sqrt(controller.JumpHeight * 2.0f * controller.GravityValue);
        Vector3 jumpVelocity = -controller.GravityDirection * jumpSpeed;

        // Remove any existing velocity in the gravity direction and apply jump
        velocity = velocity - Vector3.Project(velocity, controller.GravityDirection);
        velocity += jumpVelocity;
        controller.PlayerVelocity = velocity;
        
        // Update animator for jump state
        UpdateAnimatorGrounded(false);

        MusicManager.Instance.SetMoving(false, Vector3.zero);
        
        //Debug.Log($"Jump OnEnter - Velocity: {controller.PlayerVelocity}, Grounded: {controller.IsPlayerGrounded}");
        PlayerEvents.Trigger(PlayerAction.Jump);
    }

    public override void OnLogic()
    {
        // Keep directional locomotion responsive while airborne
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
        //MusicManager.Instance.PlaySound(SoundID.JumpConcrete, Vector3.zero);
    }
}