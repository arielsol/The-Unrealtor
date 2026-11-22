using UnityEngine;
using UnityHFSM;

public class GroundedState : HybridStateMachine<string, string, string>, IPhysicsUpdatable
{
    private BasePlayerController controller;

    public GroundedState(BasePlayerController controller)
    {
        this.controller = controller;

        // Add sub-states
        AddState("Idle", new IdleState(controller));
        AddState("Walking", new WalkingState(controller));
        AddState("Running", new RunningState(controller));

        // Add transitions between sub-states based on input magnitude.
        // Entry threshold (0.1) is higher than exit threshold (0.05) to create hysteresis:
        // a clear release is required before returning to idle, preventing oscillation at
        // the boundary and ensuring stick drift never locks a model in walking.
        AddTransition(new Transition<string>("Idle", "Walking", t => GetMovementInput().magnitude > 0.1f));
        AddTransition(new Transition<string>("Walking", "Idle", t => GetMovementInput().magnitude < 0.05f));
        AddTransition(new Transition<string>("Walking", "Running", t => GetMovementInput().magnitude > 0.8f));
        // Running → Idle must be registered before Running → Walking so that stopping
        // from a run (magnitude = 0) skips the intermediate WalkingState frame entirely.
        AddTransition(new Transition<string>("Running", "Idle", t => GetMovementInput().magnitude < 0.05f));
        AddTransition(new Transition<string>("Running", "Walking", t => GetMovementInput().magnitude <= 0.8f));

        SetStartState("Idle");
    }

    private Vector2 GetMovementInput()
    {
        if (controller.InputData.IsMovePaused)
            return Vector2.zero;

        // In the new centralized input system, each player only receives their own input
        // No need to check CurrPlayerEnum since input is already filtered by PlayerManager
        if (controller.PlayerManager.CurrentControllerType == PlayerInputManager.ControllerType.TwoPlayer)
        {
            // Two player mode - each player gets their own input
            return controller.moveAction.ReadValue<Vector2>();
        }
        else
        {
            // Single player mode - only current active player gets input
            if (controller.PlayerManager.CurrPlayerEnum == controller.playerID)
            {
                return controller.moveAction.ReadValue<Vector2>();
            }
            return Vector2.zero;
        }
    }

    public void OnPhysicsUpdate()
    {
        // Parent-level physics could go here (e.g., friction, step handling)
        (ActiveState as IPhysicsUpdatable)?.OnPhysicsUpdate();
    }
}