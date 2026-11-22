using UnityEngine;using UnityEngine.EventSystems;
using UnityEngine.InputSystem;using System.Collections;
public class TutorialCloseButton : MonoBehaviour
{
    private PlayerInputManager PlayerManager;

    private InputAction interactAction1;
    private InputAction interactAction2;    public GameObject TutorialScreen;    private bool hasPlayed = false;    void OnEnable()
    {
        Debug.Log("Tut - enable");

        if (hasPlayed)
        {
            TutorialScreen.SetActive(false);
        }

        PlayerManager = PlayerInputManager.Instance;

        interactAction1 = PlayerManager.Player1Controls.Gameplay.Interact;
        interactAction2 = PlayerManager.Player2Controls.Gameplay.Interact;

        interactAction1.started += OnInteractActionStarted;
        interactAction2.started += OnInteractActionStarted;        TutorialScreen = gameObject.transform.parent.gameObject;    }    void OnDisable()
    {
        Debug.Log("Tut - disabled");

        interactAction1.started -= OnInteractActionStarted;
        interactAction2.started -= OnInteractActionStarted;        hasPlayed = true;    }    private void OnInteractActionStarted(InputAction.CallbackContext context)
    {
        Debug.Log("Tut - int pressed");
        CloseTutorial();

    }
    public void CloseTutorial()
    {
        TutorialScreen.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        PlayerEvents.OnKeyTutorial?.Invoke();
    }
}