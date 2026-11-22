using UnityEngine;
using UnityEngine.InputSystem;
using Yarn.Unity;
using UnityEngine.SceneManagement;
using System.Collections;
using DG.Tweening;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MenuController : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public GameObject PauseMenu;
    public GameObject SettingsMenu;
    public GameObject StartingButton;
    public GameObject AudioButton;
    public GameObject CreditMenu;
    public ScrollCredits Scroll;

    private GameObject ActiveButton = null;
    private bool isInSlider = false;

    private PlayerInputManager PlayerManager;

    // This is maybe not the right way to handle two player InputActions - made by Ari
    private InputAction escAction1;
    private InputAction escAction2;
    private InputAction backAction1;
    private InputAction backAction2;
    private InputActionMap anyAction1;
    private InputActionMap anyAction2;

    //preventing double esc inputs so menu can actually open
    private static bool canPause = true;
    private Coroutine EscPressed;
    private bool wasPausedDuringCutscene;

    [SerializeField] private float CreditsFadeTime;
    [SerializeField] private Ease CreditsFadeCurve;


    void Start()
    {
        PlayerManager = PlayerInputManager.Instance;


        escAction1 = PlayerManager.Player1Controls.Gameplay.Escape;
        escAction2 = PlayerManager.Player2Controls.Gameplay.Escape;

        backAction1 = PlayerManager.Player1Controls.Menu.Back;
        backAction2 = PlayerManager.Player2Controls.Menu.Back;

        anyAction1 = PlayerManager.Player1Controls.Menu;
        anyAction2 = PlayerManager.Player2Controls.Menu;

        anyAction1.actionTriggered += OnAnyButton;
        anyAction2.actionTriggered += OnAnyButton;

        escAction1.started += OnEscActionStart;
        escAction2.started += OnEscActionStart;

        backAction1.started += OnBackActionStart;
        backAction2.started += OnBackActionStart;

        GameEvents.OnEndGame += OnEndGame;

    }

    private void OnBackActionStart(InputAction.CallbackContext context)
    {
        //  Debug.Log("MC - back pressed");
        MusicManager.Instance?.PlaySFX(SoundID.BackClicked, Vector3.zero);
        if (CreditMenu.activeSelf)
        {
            //   Debug.Log("MC - close credits");
            // LoadMainMenu();
            return;
        }
        if (!canPause)
        {
            return;
        }
        if (SettingsMenu.activeSelf && !isInSlider)
        {
            // Debug.Log("MC - close settings");
            ExitSetting();
        }
        else if (PauseMenu.activeSelf)
        {
            // Debug.Log("MC - Unpause menu");
            UnPause();
        }
        else if (isInSlider)
        {
            // Debug.Log("MC - exit slider");
            EventSystem.current.SetSelectedGameObject(ActiveButton);
            isInSlider = false;
        }
    }

    private void OnEscActionStart(InputAction.CallbackContext context)
    {
        if (CreditMenu.activeSelf)
        {
            return;
        }
        // Debug.Log("MC - esc pressed");
        if (!canPause)
        {
            return;
        }
        StartCoroutine(PauseTimer());
        if (SettingsMenu.activeSelf)
        {
            //       Debug.Log("MC - close settings");
            ExitSetting();
        }
        else if (PauseMenu.activeSelf)
        {
            //     Debug.Log("MC - Unpause menu");
            UnPause();
        }
        else
        {
            //   Debug.Log("MC - pause menu");
            Pause();
        }
    }

    public void Pause()
    {
        //all pause functionality here
        MusicManager.Instance?.PlaySFX(SoundID.PauseMenuOpened, Vector3.zero);
        PauseMenu.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale = 0f;
        wasPausedDuringCutscene = GameManager.Instance != null &&
                                  GameManager.Instance.CurrentCutsceneState == GameManager.CutsceneState.CUTSCENE;
        if (!wasPausedDuringCutscene)
        {
            PlayerInputManager.Instance.SaveInputData();
        }
        PlayerInputManager.Instance.SetAllPlayersInputPaused(true);
        DialogueManager.Instance.PauseDialogue();
        EventSystem.current.SetSelectedGameObject(StartingButton);
        MusicManager.Instance?.PlaySFX(SoundID.PauseMenuOpened, Vector3.zero);
    }

    public void UnPause()
    {
        //all unpause functionality
        MusicManager.Instance?.PlaySFX(SoundID.PauseMenuClosed, Vector3.zero);
        PauseMenu.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Time.timeScale = 1.0f;
        DialogueManager.Instance.PlayDialogue();
        if (wasPausedDuringCutscene)
        {
            // Keep player controls paused; cutscene code restores them when sequence is done.
            PlayerInputManager.Instance.SetAllPlayersInputPaused(true);
            wasPausedDuringCutscene = false;
        }
        else
        {
            PlayerInputManager.Instance.RestorePlayers();
        }
        MusicManager.Instance?.PlaySFX(SoundID.PauseMenuClosed, Vector3.zero);
    }

    public void ExitSetting()
    {
        MusicManager.Instance?.PlaySFX(SoundID.BackClicked, Vector3.zero);
        SettingsMenu.SetActive(false);
        PauseMenu.SetActive(true);
        EventSystem.current.SetSelectedGameObject(StartingButton);
        MusicManager.Instance?.PlaySFX(SoundID.PauseMenuClosed, Vector3.zero);
    }

    public void OpenSetting()
    {
        MusicManager.Instance?.PlaySFX(SoundID.PauseMenuClick, Vector3.zero);
        SettingsMenu.SetActive(true);
        PauseMenu.SetActive(false);
        EventSystem.current.SetSelectedGameObject(AudioButton);
        ActiveButton = AudioButton;
        MusicManager.Instance?.PlaySFX(SoundID.PauseMenuOpened, Vector3.zero);
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1.0f;
        //  Debug.Log("Main Menu");
        SceneManager.LoadSceneAsync("StartMenuScene", LoadSceneMode.Single);
    }

    public void Quit()
    {
        //  Debug.Log("Quit");
        Application.Quit();
    }

    private IEnumerator PauseTimer()
    {
        canPause = false;
        yield return new WaitForSecondsRealtime(0.25f);
        canPause = true;

    }

    public void SetActiveButton(GameObject go)
    {
        if (go != null && go != ActiveButton)
        {
            MusicManager.Instance?.PlaySFX(SoundID.PauseMenuHovered, Vector3.zero);
        }
        ActiveButton = go;
    }

    public void SetSlider(bool inSlider)
    {
        isInSlider = inSlider;
    }

    private void OnEndGame()
    {
        if (PauseMenu.activeSelf)
        {
            // Debug.Log("MC - Unpause menu");
            UnPause();
        }

        //FadeInCredits();
        CreditMenu.SetActive(true);
        Scroll.SetMoving();

        // DialogueManager.Instance.StartDialogue("CreditsScreen");
    }

    private void FadeInCredits()
    {
        MusicManager.Instance?.PlaySFX(SoundID.InteractibleHovered, Vector3.zero);
        CreditMenu.SetActive(true);
        //CreditMenu.transform.DOLocalMoveX();
        Scroll.SetMoving();

        /*Image menuImage = CreditMenu.GetComponent<Image>();
        Color menuColor = menuImage.color;
        CreditMenu.SetActive(true);
        menuColor.a = 0.0f;
        menuImage.color = menuColor;
        
        float currTime = 0.0f;
        float currAlpha = 0.0f;
        while(currTime < CreditsFadeTime)
        {
            currTime += Time.deltaTime; 
            currAlpha = currTime/CreditsFadeTime;
            DOVirtual.EasedValue(0, 1, currAlpha, CreditsFadeCurve);
            
            Debug.Log(currTime +  " " + currAlpha);
            yield return new WaitForEndOfFrame();
        }*/

        // DialogueManager.Instance.StartDialogue("CreditsScreen");
    }

    public void PlaySoundEfffect(string name)
    {
        if (System.Enum.TryParse<SoundID>(name, out SoundID nameId))
        {
            MusicManager.Instance.PlaySFX(nameId, Vector3.zero);
        }
    }

    public void OnAnyButton(InputAction.CallbackContext context)
    {
        if (EventSystem.current.currentSelectedGameObject == null)
        {
            if (context.action == PlayerManager.Player1Controls.Menu.Click || context.action == PlayerManager.Player1Controls.Menu.Point ||
                context.action == PlayerManager.Player2Controls.Menu.Click || context.action == PlayerManager.Player2Controls.Menu.Click)
            {
                Debug.Log("Testing any button press action: " + context.action);
                return;
            }
            isInSlider = false;
            if (SettingsMenu.gameObject.activeSelf)
            {
                MusicManager.Instance?.PlaySFX(SoundID.PauseMenuHovered, Vector3.zero);
                EventSystem.current.SetSelectedGameObject(AudioButton);
                isInSlider = false;
            }
            else
            {
                MusicManager.Instance?.PlaySFX(SoundID.PauseMenuHovered, Vector3.zero);
                EventSystem.current.SetSelectedGameObject(StartingButton);
            }
        }

    }

}
