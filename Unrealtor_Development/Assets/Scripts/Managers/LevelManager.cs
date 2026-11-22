using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

public class LevelManager : MonoBehaviour
{
    [SerializeField] private BasePlayerController twoPlayerController;
    [SerializeField] private MatchManager matchManager;
    [SerializeField] private float levelChargeTime = 1.5f;
    [SerializeField] private int currentLevel;
    private TwoPlayerInput controls;
    private bool player1Interact;
    private bool isLevelComplete;
    private float chargeAmount;
    private float chargeRatio;
    private float currentTime;

    private void Awake()
    {
        controls = PlayerInputManager.Instance.Controls;

        controls.Gameplay.Interact.performed += ctx => player1Interact = true;
        controls.Gameplay.Interact.canceled += ctx => player1Interact = false;
    }

    

    private void Start()
    {
        GameplayMetricsCollector.RecordLevelStart();
        Debug.Log("Starting Level sound ambience");
        
       if (MusicManager.Instance != null)
    {
        MusicManager.Instance.PlaySound(SoundID.Ambience, Vector3.zero); // 2D sound
        Debug.Log("Ambience playing");
    }
    else
    {
        Debug.LogWarning("MusicManager instance not found!");
    }
        isLevelComplete = false;
    }

    private void Update()
    {
        if(isLevelComplete)
        {
            ChargeNextLevel();
        }
    }

    public float GetChargeAmount()
    {
        return chargeAmount;
    }

    public void SetComplete(bool isComplete)
    {
        if (isComplete)
            GameplayMetricsCollector.RecordLevelComplete();
        isLevelComplete = isComplete;
    }

    private void LoadNextLevel(int CurrentLevel)
    {
        int nextLevel = CurrentLevel + 1;
        SceneManager.LoadScene(nextLevel);
    }

    private void ChargeNextLevel()
    {
        if (player1Interact)
        {
            currentTime += Time.deltaTime;
            if (currentTime >= levelChargeTime)
            {
                currentTime = levelChargeTime;
                Debug.Log("Loading Next Scene");
                LoadNextLevel(currentLevel);
            }
            chargeRatio = currentTime / levelChargeTime;
            chargeAmount = Mathf.Lerp(0f, -10f, chargeRatio);
        }
        else
        {
            currentTime -= Time.deltaTime;
            if (currentTime < 0f) 
            {
                currentTime = 0f;
            }
            chargeRatio = currentTime / levelChargeTime;
            chargeAmount = Mathf.Lerp(0f, -10f, chargeRatio); // Fades back up to 0 smoothly
        }
    }
}
