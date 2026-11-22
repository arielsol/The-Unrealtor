using System.Collections.Generic;
using UnityEngine;

public class PickUpObject : InteractionBase
{
    private static readonly Dictionary<BasePlayerController.PlayerID, PickUpObject> ActivePickupSourceByPlayer = new();

    private bool isPickedUpPlayer1 = false;
    private bool isPickedUpPlayer2 = false;
    [SerializeField] private MatchUI.PopUpType type;

    public static bool TryConsumeInteractToClosePickupPopup(BasePlayerController.PlayerID playerId)
    {
        if (UIManager.Instance == null)
            return false;

        return UIManager.Instance.TryClosePickupPopupWithInteract(playerId);
    }

    /// <summary>Sync local pickup flags when the hint is closed from F (UIManager already hid the popup).</summary>
    public static void NotifyPickupHintClosedForPlayer(BasePlayerController.PlayerID playerId, MatchUI.PopUpType popupType)
    {

        if (ActivePickupSourceByPlayer.TryGetValue(playerId, out PickUpObject registered) && registered != null)
        {

            if (playerId == BasePlayerController.PlayerID.Player1)
                registered.isPickedUpPlayer1 = false;
            else
                registered.isPickedUpPlayer2 = false;
            ActivePickupSourceByPlayer.Remove(playerId);
            if (registered.dialogueName != "" && !registered.dialogueHasPlayed)
            {
                DialogueManager.Instance.StartDialogue(registered.dialogueName);
                registered.dialogueHasPlayed = true;
            }
            MusicManager.Instance.PlaySound(SoundID.InteractibleDismissed, Vector3.zero);
            return;
        }

        foreach (PickUpObject p in Object.FindObjectsByType<PickUpObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (p == null || p.type != popupType) continue;
            if (playerId == BasePlayerController.PlayerID.Player1 && p.isPickedUpPlayer1)
            {

                p.isPickedUpPlayer1 = false;
                if (p.dialogueName != "")
                {
                    DialogueManager.Instance.StartDialogue(p.dialogueName);
                }
            }
            if (playerId == BasePlayerController.PlayerID.Player2 && p.isPickedUpPlayer2)
            {

                p.isPickedUpPlayer2 = false;
                if (p.dialogueName != "")
                {
                    DialogueManager.Instance.StartDialogue(p.dialogueName);
                }
            }
        }
        return;
    }

    public static void ClearGlobalInteractRegistrationForPlayer(BasePlayerController.PlayerID playerId)
    {
        ActivePickupSourceByPlayer.Remove(playerId);
    }

    public override void OnInteract(BasePlayerController player)
    {
        if (player == null)
            return;

        if (!CanInteract())
            return;

        Vector3 probe = player.Rigidbody != null ? player.Rigidbody.position : player.transform.position;
        if (!IsPlayerFacingInteractable(player, probe, GetComponent<Collider>()))
            return;

        bool isLeftPlayer = player.playerID == BasePlayerController.PlayerID.Player1;
        bool isPickedUp = isLeftPlayer ? !isPickedUpPlayer1 : !isPickedUpPlayer2;
        if (isLeftPlayer)
            isPickedUpPlayer1 = isPickedUp;
        else
            isPickedUpPlayer2 = isPickedUp;

        if (playSoundOnInteract)
            MusicManager.Instance.PlaySFX(_interactSound, Vector3.zero);

        if (player.playerID == BasePlayerController.PlayerID.Player1)
            onInteractPlayer1?.Invoke();
        else
            onInteractPlayer2?.Invoke();

        // MatchUI applies that player to WorldSpaceControlSchemeUI so KB vs gamepad hints match who opened this pickup.
        UIManager.Instance?.UpdatePickupPopup(isPickedUp, type, player.playerID);

        if (isPickedUp)
            ActivePickupSourceByPlayer[player.playerID] = this;
        else if (ActivePickupSourceByPlayer.TryGetValue(player.playerID, out PickUpObject reg) && reg == this)
            ActivePickupSourceByPlayer.Remove(player.playerID);
    }
}
