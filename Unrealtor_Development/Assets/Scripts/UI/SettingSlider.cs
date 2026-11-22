using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class SettingSlider : MonoBehaviour
{
    public bool IsActive = false;

    private InputAction IncAction1;
    private InputAction IncAction2;
    private InputAction DecAction1;
    private InputAction DecAction2;

    private PlayerInputManager PlayerManager;

    public GameObject IncButton;
    public GameObject DecButton;

    bool isFirst = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (SceneManager.GetActiveScene().name == "StartMenuScene")
        {
            StartSetting ss = GameObject.Find("SettingsPanel").GetComponent<StartSetting>();
            IncAction1 = ss.PlayerControls.Menu.IncrementSlider;
            DecAction1 = ss.PlayerControls.Menu.DecrementSlider;

            IncAction1.started += OnIncrementSlider;
            DecAction1.started += OnDecrementSlider;
        }
        else
        {
            PlayerManager = PlayerInputManager.Instance;

            IncAction1 = PlayerManager.Player1Controls.Menu.IncrementSlider;
            IncAction2 = PlayerManager.Player2Controls.Menu.IncrementSlider;

            DecAction1 = PlayerManager.Player1Controls.Menu.DecrementSlider;
            DecAction2 = PlayerManager.Player2Controls.Menu.DecrementSlider;

            IncAction1.started += OnIncrementSlider;
            IncAction2.started += OnIncrementSlider;

            DecAction1.started += OnDecrementSlider;
            DecAction2.started += OnDecrementSlider;
        }

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void SetFirst()
    {
        isFirst = true;
    }

    public void SetSliderActive(bool active)
    {
        IsActive = active;
        if (isFirst)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
            isFirst = false;
        }
    }

    public void OnIncrementSlider(InputAction.CallbackContext context)
    {
        if (!IsActive)
        {
            return;
        }
        Debug.Log("inc activeValue: " + IsActive);
        IncButton.GetComponent<Button>().onClick.Invoke();
    }

    public void OnDecrementSlider(InputAction.CallbackContext context)
    {

        if (!IsActive)
        {
            return;
        }
        Debug.Log("dec activeValue: " + IsActive);
        DecButton.GetComponent<Button>().onClick.Invoke();
    }
}
