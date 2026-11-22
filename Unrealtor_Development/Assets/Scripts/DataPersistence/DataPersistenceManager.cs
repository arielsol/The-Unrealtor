using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DataPersistenceManager : MonoBehaviour
{
    [Header("Debugging")]
    [SerializeField] private bool isDataPersistenceEnabled = true;
    [SerializeField] private bool initializeDataIfNull = false;
    [SerializeField] private bool overrideSelectedProfileId = false;
    [SerializeField] private string testSelectedProfileId = "test";

    [Header("File Storage Config")]
    [SerializeField] private string fileName;
    [SerializeField] private bool useEncryption;

    [Header("Auto Saving Config")]
    [SerializeField] private float autoSaveTimeSeconds = 60f;

    private GameData gameData;
    private List<IDataPersistence> dataPersistenceObjects;
    private FileDataHandler dataHandler;
    private string selectedProfileId = "";

    private Coroutine autoSaveCoroutine;
    public static DataPersistenceManager instance { get; private set; }

    private void Awake()
    {
        if (instance != null)
        {
            Debug.Log("Found more than one Data Persistence Manager in the scene. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        if (!isDataPersistenceEnabled)
            Debug.LogWarning("Data Persistence is currently disabled via inspector flag.");

        dataHandler = new FileDataHandler(Application.persistentDataPath, fileName, useEncryption);
        InitializeSelectedProfileId();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        dataPersistenceObjects = FindAllDataPersistenceObjects();
        LoadGame();

        if (autoSaveCoroutine != null)
            StopCoroutine(autoSaveCoroutine);
        autoSaveCoroutine = StartCoroutine(AutoSave());
    }

    private void LoadGame()
    {
        if (!isDataPersistenceEnabled)
        {
            Debug.LogWarning("[DataPersistenceManager] LoadGame skipped - persistence disabled");
            return;
        }

        // 1) Try to load from file
        gameData = dataHandler.Load(selectedProfileId);
        Debug.Log($"[DataPersistenceManager] Loading from path: {Application.persistentDataPath}");

        // 2) If none and we want to init, do NewGame first
        if (gameData == null && initializeDataIfNull)
        {
            Debug.Log("[DataPersistenceManager] No saved data found. Initializing new data.");
            NewGame();
        }

        // 3) If still null, bail
        if (gameData == null)
        {
            Debug.Log("[DataPersistenceManager] No saved data found. A new game needs to be started before data can be loaded.");
            return;
        }

        bool isFirstTime = gameData.isFirstTimeGame;
    

        // 4) Hand off the data to all persistence objects
        foreach (IDataPersistence dp in dataPersistenceObjects)
        {
            try
            {
                dp.LoadData(gameData);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DataPersistenceManager] Error loading data for {dp.GetType().Name}: {e.Message}");
            }
        }

        if (isFirstTime)
        {
            gameData.isFirstTimeGame = false;
        }
        
        Debug.Log($"[DataPersistenceManager] LoadGame completed with {dataPersistenceObjects.Count} objects");
    }

    public void NewGame()
    {
        gameData = new GameData();
    }

    public void ChangeSelectedProfileId(string newProfileId)
    {
        selectedProfileId = newProfileId;
        LoadGame();
    }

    public void DeleteProfileData(string profileId)
    {
        dataHandler.Delete(profileId);
        InitializeSelectedProfileId();
        LoadGame();
    }

    private void InitializeSelectedProfileId()
    {
        selectedProfileId = dataHandler.GetMostRecentlyUpdatedProfileId();

        if (overrideSelectedProfileId)
        {
            selectedProfileId = testSelectedProfileId;
            Debug.LogWarning("Overrode Selected Profile Id with test id: " + testSelectedProfileId);
        }
    }

    public void SaveGame()
    {
        if (!isDataPersistenceEnabled || gameData == null)
        {
            Debug.LogWarning("[DataPersistenceManager] SaveGame skipped - persistence disabled or no game data");
            return;
        }


        foreach (IDataPersistence dp in dataPersistenceObjects)
        {
            try
            {
                dp.SaveData(gameData);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DataPersistenceManager] Error saving data for {dp.GetType().Name}: {e.Message}");
            }
        }

        gameData.lastUpdated = DateTime.Now.ToBinary();
        dataHandler.Save(gameData, selectedProfileId);
    }

    private void OnApplicationQuit()
    {
        SaveGame();
    }

    public void GameOver()
    {
        DeleteProfileData(selectedProfileId);
    }

    private List<IDataPersistence> FindAllDataPersistenceObjects()
    {
        var found = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.InstanceID)
            .OfType<IDataPersistence>();
        return new List<IDataPersistence>(found);
    }

    public bool HasGameData()
    {
        return gameData != null;
    }

    public Dictionary<string, GameData> GetAllProfilesGameData()
    {
        return dataHandler.LoadAllProfiles();
    }

    private IEnumerator AutoSave()
    {
        while (true)
        {
            yield return new WaitForSeconds(autoSaveTimeSeconds);
            SaveGame();
            Debug.Log("Auto Saved Game.");
        }
    }
}