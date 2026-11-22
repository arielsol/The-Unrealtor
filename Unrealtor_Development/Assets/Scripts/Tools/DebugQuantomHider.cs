using UnityEngine;

public class DebugQuantomHider : MonoBehaviour
{
    public GameObject Screen;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (Application.platform == RuntimePlatform.WindowsPlayer ||
            Application.platform == RuntimePlatform.WindowsEditor)
        {
            if (Input.GetKeyDown(KeyCode.JoystickButton6))
            {
                Screen.SetActive(false);
            }
        }
        else if (Application.platform == RuntimePlatform.OSXPlayer ||
                 Application.platform == RuntimePlatform.OSXEditor)
        {
            if (Input.GetKeyDown(KeyCode.JoystickButton10))
            {
                Screen.SetActive(false);
            }
        }

    }
}
