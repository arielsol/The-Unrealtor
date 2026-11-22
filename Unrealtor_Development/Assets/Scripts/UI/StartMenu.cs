using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class StartMenu : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public string LevelOne = "";
    public GameObject EnterButton;
    public Image bar;
    public TwoPlayerInput PlayerControls;
    private InputActionMap anyAction;

    [SerializeField] private GameObject LoadingScreen;
    [SerializeField] private StartSetting SettingMenu;
    void Awake()
    {
        PlayerControls = new TwoPlayerInput();

        PlayerControls.Menu.Enable();
        PlayerControls.Gameplay.Enable();

        anyAction = PlayerControls.Menu;
        anyAction.actionTriggered += OnAnyButton;
    }

    void Start()
    {
        SetPlayerCount(false);
        SetPlayer1Control(false);
        MusicManager.Instance.ResetMusic();
    }

    public void Quit()
    {
        //MusicManager.Instance?.PlaySFX(SoundID.BackClicked, Vector3.zero);
        Debug.Log("Quit");
        Application.Quit();
    }

    public void SetPlayerCount(bool isOne)
    {
        if (isOne)
        {
            PlayerPrefs.SetInt("PlayerCount", 1);
            PlaySoundEfffect("UIBoxCheck");
        }
        else
        {
            PlayerPrefs.SetInt("PlayerCount", 2);
            PlaySoundEfffect("UIBoxUncheck");
        }
    }

    public void TwoPlayer()
    {
        MusicManager.Instance?.PlaySFX(SoundID.StartClicked, Vector3.zero);
        LoadingScreen.SetActive(true);
        DialogueManager.Instance.StartDialogue("StartScreen");
        StartCoroutine(LoadLevel());
        // SceneManager.LoadSceneAsync(LevelOne, LoadSceneMode.Single);
    }

    public void SetPlayer1Control(bool isController)
    {
        if (isController)
        {
            PlayerPrefs.SetString("Player1Control", "Controller");
            PlaySoundEfffect("UIBoxCheck");
        }
        else
        {
            PlayerPrefs.SetString("Player1Control", "Keyboard");
            PlaySoundEfffect("UIBoxUncheck");
        }

    }

    IEnumerator LoadLevel()
    {
        yield return new WaitForSeconds(5.0f);
        AsyncOperation op = SceneManager.LoadSceneAsync(LevelOne, LoadSceneMode.Single);

        while (!op.isDone)
        {
            float progress = Mathf.Clamp01(op.progress / .9f);
            Debug.Log(op.progress);
            bar.fillAmount = progress;
            yield return null;
        }
    }

    public void PlaySoundEfffect(string name)
    {
        if (Time.timeSinceLevelLoad < 0.1f) return;

        if (System.Enum.TryParse<SoundID>(name, out SoundID nameId))
        {
            MusicManager.Instance.PlaySFX(nameId, Vector3.zero);
        }
    }

    public void OnAnyButton(InputAction.CallbackContext context)
    {
        if (EventSystem.current.currentSelectedGameObject == null)
        {
            if (context.action == PlayerControls.Menu.Click || context.action == PlayerControls.Menu.Point)
            {
                Debug.Log("Testing any button press action: " + context.action);
                return;
            }
            if (SettingMenu.gameObject.activeSelf)
            {
                SettingMenu.SetAudioSelected();
            }
            else
            {
                MusicManager.Instance?.PlaySFX(SoundID.BackClicked, Vector3.zero);
                EventSystem.current.SetSelectedGameObject(EnterButton);
            }
        }

    }
}
