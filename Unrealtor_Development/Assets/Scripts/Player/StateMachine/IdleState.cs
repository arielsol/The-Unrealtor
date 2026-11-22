using UnityEngine;

public class IdleState : BasePlayerState
{
    public IdleState(BasePlayerController controller) : base(controller)
    {
    }

    public override void OnEnter()
    {
        // Reset foot IK locks immediately so the idle animation drives the legs freely
        controller.NotifyPlayerStopped();
    }

    public override void OnLogic()
    {
        MusicManager.Instance.SetMoving(false, Vector3.zero);

        // Update animator - idle has no planar movement.
        // Use a longer damp time so InputX/InputY blend smoothly back to zero
        // rather than snapping, while still reliably reaching full idle.
        UpdateAnimatorLocomotionFromVelocity(Vector3.zero, controller.MoveSpeed, 0.05f);
        UpdateAnimatorGrounded(true);

        // No movement in idle state - player is stationary
    }

    public override void OnPhysicsUpdate()
    {
        if (!controller.IsPlayerGrounded) return;

        // Remove any velocity component along gravity while grounded (gravity-relative, supports gravity switching)
        Vector3 velocity = controller.PlayerVelocity;
        float alongGravity = Vector3.Dot(velocity, controller.GravityDirection);
        if (alongGravity >= 0f)
        {
            velocity -= controller.GravityDirection * alongGravity;
            controller.PlayerVelocity = velocity;
        }
    }
}