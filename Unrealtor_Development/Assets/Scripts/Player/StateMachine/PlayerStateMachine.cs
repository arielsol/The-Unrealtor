using UnityEngine;
using UnityHFSM;

public class PlayerStateMachine
{
    private BasePlayerController controller;
    private StateMachine<string, string> fsm;
    private string lastStateName = "";

    public PlayerStateMachine(BasePlayerController controller)
    {
        this.controller = controller;
    }

    public void Initialize()
    {
        fsm = new StateMachine<string, string>();

        var groundedState = new GroundedState(controller);
        var jumpingState = new JumpingState(controller);
        var fallingState = new FallingState(controller);

        fsm.AddState("Grounded", groundedState);
        fsm.AddState("Jumping", jumpingState);
        fsm.AddState("Falling", fallingState);

        // Trigger transition for jump from any grounded sub-state
        fsm.AddTriggerTransition("Jump", "Grounded", "Jumping");

        // Fall off cliff: if we're not grounded, we should be falling regardless of velocity direction.
        // (Velocity direction can be "up" relative to new gravity right after a gravity switch.)
        fsm.AddTransition("Grounded", "Falling", t => !controller.IsPlayerGrounded);

        // Jump to fall - check if moving in gravity direction
        fsm.AddTransition("Jumping", "Falling", t => Vector3.Dot(controller.PlayerVelocity, controller.GravityDirection) >= 0);

        // Land (only when actually falling in gravity direction and touching ground)
        fsm.AddTransition("Jumping", "Grounded", t => controller.IsPlayerGrounded && Vector3.Dot(controller.PlayerVelocity, controller.GravityDirection) >= 0);
        fsm.AddTransition("Falling", "Grounded", t => controller.IsPlayerGrounded);

        fsm.SetStartState("Grounded");
        fsm.Init();
    }

    public void Update()
    {
        fsm?.OnLogic();

        // Debug state transitions only when they change
        if (controller.EnableStateMachineDebug)
        {
            string currentStateName = fsm.GetActiveHierarchyPath();
            if (currentStateName != lastStateName)
            {
                Debug.Log($"[StateMachine] {controller.playerID}: {lastStateName} -> {currentStateName}");
                lastStateName = currentStateName;
            }

            Debug.Log($"[StateMachine] IsPlayerGrounded: {controller.IsPlayerGrounded}");
            Debug.Log($"[StateMachine] PlayerVelocity: {controller.PlayerVelocity}");
            Debug.Log($"[StateMachine] GravityDirection: {controller.GravityDirection}");
            Debug.Log($"[StateMachine] PlayerVelocity in gravity direction: {Vector3.Dot(controller.PlayerVelocity, controller.GravityDirection)}");
        }
    }

    public void PhysicsUpdate()
    {
        if (fsm == null) return;
        (fsm.ActiveState as IPhysicsUpdatable)?.OnPhysicsUpdate();
    }

    public string GetCurrentStateName()
    {
        return fsm?.GetActiveHierarchyPath() ?? "None";
    }

    public void TriggerJump()
    {
        if (controller.EnableStateMachineDebug)
        {
            Debug.Log($"[StateMachine] {controller.playerID}: Jump triggered from {GetCurrentStateName()}");
        }
        fsm?.Trigger("Jump");
    }
}