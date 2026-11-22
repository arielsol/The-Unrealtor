using System;
public static class PlayerEvents
{
    public static Action OnMove;
    public static Action OnJump;
    public static Action OnInteract;
    public static Action OnSubmit;
    public static Action OnSlowMovement;
    public static Action OnLook;
    public static Action OnResetCam;
    public static Action OnSwitchPlayer;
    public static Action OnRotate;
    
    public static Action OnKeyTutorial;

    // New: per-player contextual events
    public static Action<BasePlayerController.PlayerID, PlayerAction> OnActionByPlayer;
    public static Action<BasePlayerController.PlayerID> OnMoveByPlayer;
    public static Action<BasePlayerController.PlayerID> OnJumpByPlayer;
    public static Action<BasePlayerController.PlayerID> OnInteractByPlayer;
    public static Action<BasePlayerController.PlayerID> OnSubmitByPlayer;
    public static Action<BasePlayerController.PlayerID> OnSlowByPlayer;
    public static Action<BasePlayerController.PlayerID> OnLookByPlayer;
    public static Action<BasePlayerController.PlayerID> OnResetCamByPlayer;
    public static Action<BasePlayerController.PlayerID> OnRotateByPlayer;

    public static void Trigger(PlayerAction action)
    {
        switch (action)
        {
            case PlayerAction.Move:         OnMove?.Invoke(); break;
            case PlayerAction.Jump:         OnJump?.Invoke(); break;
            case PlayerAction.Interact:     OnInteract?.Invoke(); break;
            case PlayerAction.Look:         OnLook?.Invoke(); break;
            case PlayerAction.SlowMovement: OnSlowMovement?.Invoke(); break;
            case PlayerAction.Submit:       OnSubmit?.Invoke(); break;
            case PlayerAction.ResetCam:     OnResetCam?.Invoke(); break;
            case PlayerAction.SwitchPlayer: OnSwitchPlayer?.Invoke(); break;
            case PlayerAction.Rotate:       OnRotate?.Invoke(); break;
            case PlayerAction.KeyTutorial:  OnKeyTutorial?.Invoke(); break;
        }
    }

    // New: Trigger with player context
    public static void TriggerByPlayer(PlayerAction action, BasePlayerController.PlayerID playerId)
    {
        OnActionByPlayer?.Invoke(playerId, action);
        switch (action)
        {
            case PlayerAction.Move:         OnMoveByPlayer?.Invoke(playerId); break;
            case PlayerAction.Jump:         OnJumpByPlayer?.Invoke(playerId); break;
            case PlayerAction.Interact:     OnInteractByPlayer?.Invoke(playerId); break;
            case PlayerAction.Submit:       OnSubmitByPlayer?.Invoke(playerId); break;
            case PlayerAction.SlowMovement: OnSlowByPlayer?.Invoke(playerId); break;
            case PlayerAction.Look:         OnLookByPlayer?.Invoke(playerId); break;
            case PlayerAction.ResetCam:     OnResetCamByPlayer?.Invoke(playerId); break;
            case PlayerAction.Rotate:       OnRotateByPlayer?.Invoke(playerId); break;
            // KeyTutorial is global/dialogue-driven; not per-player
        }
    }
}