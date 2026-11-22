using UnityEngine;
using UnityEngine.UI;

public class GameplayMenuController : MonoBehaviour
{
    [SerializeField] private Image Player1Bar;
    [SerializeField] private Image Player2Bar;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Player1Bar.fillAmount = SettingManager.Instance.CurrPlayer1Sensitivity;
        Player2Bar.fillAmount = SettingManager.Instance.CurrPlayer2Sensitivity;
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void AdjustPlayer1Sense(bool increase = true)
    {
        Debug.Log("why");
        float vol = SettingManager.Instance.CurrPlayer1Sensitivity;
        if (increase)
        {
            SettingManager.Instance.SetPlayer1Sensitivity(vol + 0.1f);
            Player1Bar.fillAmount = Mathf.Clamp01(vol + 0.1f);
        }
        if (!increase)
        {
            SettingManager.Instance.SetPlayer1Sensitivity(vol - 0.1f);
            Player1Bar.fillAmount = Mathf.Clamp01(vol - 0.1f);
        }
    }

    public void AdjustPlayer2Sense(bool increase = true)
    {
        Debug.Log("why2");
        float vol = SettingManager.Instance.CurrPlayer2Sensitivity;
        if (increase)
        {
            SettingManager.Instance.SetPlayer2Sensitivity(vol + 0.1f);
            Player2Bar.fillAmount = Mathf.Clamp01(vol + 0.1f);
        }
        if (!increase)
        {
            SettingManager.Instance.SetPlayer2Sensitivity(vol - 0.1f);
            Player2Bar.fillAmount = Mathf.Clamp01(vol - 0.1f);
        }


    }
}
