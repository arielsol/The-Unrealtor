using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class StartSetting : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private bool isInSlider = false;
    public TwoPlayerInput PlayerControls;

    private InputAction backAction;

    public GameObject SettingMenu;
    public GameObject AudioButton;
    public GameObject SettingButton;

    void Awake()
    {
        PlayerControls = GameObject.Find("StartScreen").GetComponent<StartMenu>().PlayerControls;

        backAction = PlayerControls.Menu.Back;
        backAction.started += OnBackActionStart;
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void SetSlider(bool inSlider)
    {
        isInSlider = inSlider;
    }

    public void OnBackActionStart(InputAction.CallbackContext context)
    {

        if (isInSlider)
        {
            MusicManager.Instance?.PlaySFX(SoundID.BackClicked, Vector3.zero);
            EventSystem.current.SetSelectedGameObject(AudioButton);
            isInSlider = false;
        }
        else if (SettingMenu.activeSelf)
        {
            MusicManager.Instance?.PlaySFX(SoundID.BackClicked, Vector3.zero);
            EventSystem.current.SetSelectedGameObject(SettingButton);
            SettingMenu.SetActive(false);
        }
    }

    public void SetAudioSelected()
    {
        MusicManager.Instance?.PlaySFX(SoundID.BackClicked, Vector3.zero);
        EventSystem.current.SetSelectedGameObject(AudioButton);
        isInSlider = false;
    }


}
