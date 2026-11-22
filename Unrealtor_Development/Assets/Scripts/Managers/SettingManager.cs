using UnityEngine;
using Cinemachine;
using QFSW.QC;

public class SettingManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public BasePlayerController Player1;
    public BasePlayerController Player2;

    public float CurrPlayer1Sensitivity = 0.5f;
    public float CurrPlayer2Sensitivity = 0.5f;

    public static SettingManager Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)

        {

            Destroy(gameObject);

            return;

        }

        Instance = this;

        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        Player1 = GameObject.Find("Player1_Left").GetComponent<BasePlayerController>();
        Player2 = GameObject.Find("Player2_Right").GetComponent<BasePlayerController>();
        CurrPlayer1Sensitivity = Player1.sensitivityMultiplier / 2.0f;
        CurrPlayer2Sensitivity = Player2.sensitivityMultiplier / 2.0f;
    }

    // Update is called once per frame
    void Update()
    {
    }

    [Command("SetPlayer1Sensitivity", MonoTargetType.Single)]
    public void SetPlayer1Sensitivity(float sense)
    {
        sense = Mathf.Clamp01(sense);
        CurrPlayer1Sensitivity = sense;
        Player1.sensitivityMultiplier = sense * 2.0f;
        Player1.UpdateCameraSensitivity();
    }

    [Command("SetPlayer2Sensitivity", MonoTargetType.Single)]
    public void SetPlayer2Sensitivity(float sense)
    {
        sense = Mathf.Clamp01(sense);
        CurrPlayer2Sensitivity = sense;
        Player2.sensitivityMultiplier = sense * 2.0f;
        Player2.UpdateCameraSensitivity();

    }
}
