using UnityEngine;

using FMODUnity;

using FMOD.Studio;

using Yarn.Unity;

using System.Collections;

using System.Collections.Generic;



public class MusicManager : MonoBehaviour

{

    public static MusicManager Instance;



    [SerializeField] private MusicData musicData;

    [Header("FMOD Banks")]
    [SerializeField] private string masterBankName = "Master";
    [SerializeField] private string masterStringsBankName = "Master.strings";
    [SerializeField] private string musicBankName = "Desktop/MusicSystem";
    [SerializeField] private string sfxBankName = "SFX";
    [SerializeField] private string ambienceBankName = "Ambience";
    [SerializeField] private string voBankName = "VoiceOver";
    [SerializeField] private string uiBankName = "UI";
    [SerializeField] private bool preloadBankSampleData = true;



    [Header("Volume Settings (0–1)")]

    [Range(0f, 1f)] public float masterVolume = 1f;

    [Range(0f, 1f)] public float musicVolume = 1f;

    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Range(0f, 1f)] public float voVolume = 1f;



    private Vector3 _playerPosition;

    private Dictionary<SoundID, SoundEntry> _soundEntries = new();

    public Dictionary<SoundID, EventInstance> _activeInstances = new();

    [HideInInspector] public EventInstance _currentMusicInstance;

    private SoundEntry _currentMusicEntry;

    private readonly HashSet<string> _loadedBanks = new();



    private bool _isMoving;

    private float _footstepTimer;

    //bgm event ints
    private int _puzzlesSolved = 0;
    private int _beatDrop = 0;



    private void Awake()

    {

        if (Instance != null && Instance != this)

        {

            Destroy(gameObject);

            return;

        }

        Instance = this;

        DontDestroyOnLoad(gameObject);



        foreach (var entry in musicData.soundEntries)

            _soundEntries[entry.soundID] = entry;


        LoadBankIfNeeded(masterStringsBankName);
        LoadBankIfNeeded(masterBankName);

        // Load all the other banks
        /*
        LoadBankIfNeeded(musicBankName);
        LoadBankIfNeeded(sfxBankName);
        LoadBankIfNeeded(ambienceBankName);
        LoadBankIfNeeded(voBankName);
        LoadBankIfNeeded(uiBankName);
        */

        LoadVolumeSettings();

        ApplyVolumes();

        StopSound(SoundID.FinalBGM);
        StopSound(SoundID.RightAmbience);
        StopSound(SoundID.LeftAmbience);

        PlaySound(SoundID.FinalBGM);
        PlaySound(SoundID.RightAmbience);
        PlaySound(SoundID.LeftAmbience);
    }

    private void OnDestroy()

    {

    }

    private void EnsureBankLoaded(SoundEntry entry)

    {

        LoadBankIfNeeded(GetBankName(entry));

    }

    private string GetBankName(SoundEntry entry)

    {

        if (!string.IsNullOrWhiteSpace(entry.bankName))

            return entry.bankName;



        return entry.category switch

        {

            SoundCategory.Music => musicBankName,
            SoundCategory.SFX => sfxBankName,
            SoundCategory.Ambience => ambienceBankName,
            SoundCategory.VO => voBankName,
            SoundCategory.UI => uiBankName,
            _ => string.Empty

        };

    }

    private void LoadBankIfNeeded(string bankName)

    {

        if (string.IsNullOrWhiteSpace(bankName) || _loadedBanks.Contains(bankName))

            return;



        try

        {

            // FMOD path-based lookups require the strings bank to be loaded.
            if (bankName != masterStringsBankName)

                LoadBankIfNeeded(masterStringsBankName);

            RuntimeManager.LoadBank(bankName, preloadBankSampleData);
            _loadedBanks.Add(bankName);

        }

        catch (BankLoadException exception)

        {

            Debug.LogWarning($"Failed to load FMOD bank '{bankName}': {exception.Message}");

        }

    }

    private void Update()

    {

        if (_isMoving)

        {

            _footstepTimer += Time.deltaTime;

            if (_footstepTimer >= musicData.footstepInterval)

            {

                // PlaySFX(SoundID.FootstepConcrete, Vector3.zero); //Replaced by animation tags

                _footstepTimer = 0f;

            }

        }

        else

        {

            _footstepTimer = 0f;

        }



        ApplyVolumes();

    }



    public void SetMoving(bool isMoving, Vector3 position)

    {

        _isMoving = isMoving;

        _playerPosition = Vector3.zero;

    }



    public void PlaySFX(SoundID soundID, Vector3 position)

    {
        if (!_soundEntries.TryGetValue(soundID, out var entry))

        {

            Debug.LogWarning($"Sound not found: {soundID}");

            return;

        }


        if (entry.category != SoundCategory.SFX && entry.category != SoundCategory.UI) return;



        if (entry.eventRef.IsNull)

        {

            Debug.LogWarning($"Sound has no FMOD event reference: {soundID}");

            return;

        }

        RuntimeManager.PlayOneShot(entry.eventRef, position);

    }

    [YarnCommand("PlaySFX")]
    public void VOPlaySFX(string sfxName)
    {
        if (System.Enum.TryParse<SoundID>(sfxName, out SoundID sName))
        {
            PlaySFX(sName, Vector3.zero);
        }
    }

    /// <summary>
    /// Creates and configures an FMOD event instance but does NOT start it.
    /// Useful when callers need to set parameters before first audible frame.
    /// </summary>
    public EventInstance CreateSoundInstance(SoundID soundID, Vector3? position = null)
    {
        if (!_soundEntries.TryGetValue(soundID, out var entry))
        {
            Debug.LogWarning($"Sound not found: {soundID}");
            return default;
        }

        if (entry.eventRef.IsNull)
        {
            Debug.LogWarning($"Sound has no FMOD event reference: {soundID}");
            return default;
        }

        EventInstance instance = RuntimeManager.CreateInstance(entry.eventRef);
        // Only apply 3D positioning when an explicit position is provided.
        // Parameter-driven emitters (e.g. ListenerEmitter) pass null so the FMOD
        // spatializer doesn't fight their custom pan/distance parameters.
        if (position.HasValue)
        {
            instance.set3DAttributes(RuntimeUtils.To3DAttributes(position.Value));
        }
        instance.setPitch(entry.pitch);
        return instance;
    }

    public EventInstance PlaySound(SoundID soundID, Vector3? position = null, bool forceOverride = false)

    {

        if (!_soundEntries.TryGetValue(soundID, out var entry))

        {

            Debug.LogWarning($"Sound not found: {soundID}");

            return default;

        }

        float finalVolume = entry.defaultVolume * masterVolume;

        // Looping Music = exclusive BGM (priority replaces FinalBGM etc.). Non-looping Music = one-shots that layer on top.
        bool exclusiveMusic = entry.category == SoundCategory.Music && entry.isLooping;

        if (entry.category == SoundCategory.Music)

        {
            // finalVolume *= musicVolume;

            if (exclusiveMusic)

            {
                if (!forceOverride && _currentMusicEntry != null && entry.priority <= _currentMusicEntry.priority)

                    return default;

                StopCurrentMusic();

                _currentMusicEntry = entry;

            }

        }

        else if (entry.category == SoundCategory.SFX)
        {
            finalVolume *= sfxVolume;
        }



        if (entry.eventRef.IsNull)

        {

            Debug.LogWarning($"Sound has no FMOD event reference: {soundID}");

            return default;

        }



        EventInstance instance = CreateSoundInstance(soundID, position);
        if (!instance.isValid())
            return default;

        instance.start();

        // One-shot music: do not replace BGM handle; release so the event can finish without leaking instances.
        if (entry.category == SoundCategory.Music && !entry.isLooping)

        {
            instance.release();

            return default;

        }

        if (entry.isLooping && !_activeInstances.ContainsKey(soundID))

            _activeInstances[soundID] = instance;

        if (exclusiveMusic)

            _currentMusicInstance = instance;

        return instance;

    }



    public void StopSound(SoundID soundID, bool allowFadeOut = true)

    {

        if (_activeInstances.TryGetValue(soundID, out var instance))

        {

            instance.stop(allowFadeOut ? FMOD.Studio.STOP_MODE.ALLOWFADEOUT : FMOD.Studio.STOP_MODE.IMMEDIATE);

            instance.release();

            _activeInstances.Remove(soundID);

        }
        else
        {
            Debug.Log("MM - failed to stop " + soundID);
        }

    }



    private void StopCurrentMusic(bool allowFadeOut = true)

    {

        if (_currentMusicInstance.isValid())

        {

            _currentMusicInstance.stop(allowFadeOut ? FMOD.Studio.STOP_MODE.ALLOWFADEOUT : FMOD.Studio.STOP_MODE.IMMEDIATE);

            _currentMusicInstance.release();

            _currentMusicInstance.clearHandle();

        }



        _currentMusicEntry = null;

        _currentMusicInstance = default;

    }



    public EventInstance GetCurrentMusic()

    {

        return _currentMusicInstance;

    }



    public void SetMasterVolume(float value)

    {

        masterVolume = Mathf.Clamp01(value);

        RuntimeManager.StudioSystem.setParameterByName("Vol_Master", masterVolume);

        if (_currentMusicInstance.isValid())
        {
            //        _currentMusicInstance.setVolume(_currentMusicEntry.defaultVolume * masterVolume * musicVolume);
        }

        SaveVolumeSettings();

    }



    public void SetMusicVolume(float value)

    {

        musicVolume = Mathf.Clamp01(value);

        RuntimeManager.StudioSystem.setParameterByName("Vol_MX", musicVolume);

        if (_currentMusicInstance.isValid())
        {
            //         _currentMusicInstance.setVolume(_currentMusicEntry.defaultVolume * masterVolume * musicVolume);
        }

        SaveVolumeSettings();

    }



    public void SetSFXVolume(float value)

    {

        sfxVolume = Mathf.Clamp01(value);

        RuntimeManager.StudioSystem.setParameterByName("Vol_SFX", sfxVolume);

        SaveVolumeSettings();

    }



    public void SetVOVolume(float value)

    {

        voVolume = Mathf.Clamp01(value);

        RuntimeManager.StudioSystem.setParameterByName("Vol_VO", voVolume);

        SaveVolumeSettings();

    }



    private void ApplyVolumes()

    {

        RuntimeManager.StudioSystem.setParameterByName("Vol_Master", masterVolume);

        RuntimeManager.StudioSystem.setParameterByName("Vol_MX", musicVolume);

        RuntimeManager.StudioSystem.setParameterByName("Vol_SFX", sfxVolume);

        RuntimeManager.StudioSystem.setParameterByName("Vol_VO", voVolume);



    }



    private void SaveVolumeSettings()

    {

        PlayerPrefs.SetFloat("MasterVolume", masterVolume);

        PlayerPrefs.SetFloat("MusicVolume", musicVolume);

        PlayerPrefs.SetFloat("SFXVolume", sfxVolume);

        PlayerPrefs.SetFloat("VOVolume", voVolume);

        PlayerPrefs.Save();

    }



    private void LoadVolumeSettings()

    {

        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);

        musicVolume = PlayerPrefs.GetFloat("MusicVolume", 1f);

        sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);

        voVolume = PlayerPrefs.GetFloat("VOVolume", 1f);

    }



    public float CallVO(int idx, DialogueScriptableObject currDialogue, ref EventInstance currVOSound, float finalVolume)

    {

        //Debug.Log("CallVO");
        if (currVOSound.isValid())
        {
            currVOSound.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            currVOSound.release();
        }

        if (currDialogue.VOSoundEvents.Count <= idx)
        {
            return -1.0f;
        }

        currVOSound.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        currVOSound.release();

        currVOSound = RuntimeManager.CreateInstance(currDialogue.VOSoundEvents[idx]);
        currVOSound.set3DAttributes(RuntimeUtils.To3DAttributes(Vector3.zero));

        float vol = 1.0f;
        currVOSound.getVolume(out vol);
        currVOSound.setVolume(vol * finalVolume);
        currVOSound.start();

        EventDescription desc;
        RuntimeManager.StudioSystem.getEventByID(currDialogue.VOSoundEvents[idx].Guid, out desc);
        desc.getLength(out int lengthMs);

        float lineTime = lengthMs / 1000f;
        return lineTime;

    }

    public void IncrementMusic()
    {
        _beatDrop = 1;
        _puzzlesSolved++;
        RuntimeManager.StudioSystem.setParameterByName("BeatDrop", _beatDrop);
        RuntimeManager.StudioSystem.setParameterByName("PuzzlesSolved", _puzzlesSolved);
        StartCoroutine(DropTimer());
        //  Debug.Log("MM - inc");
    }

    public void ResetMusic()
    {
        _puzzlesSolved = 0;
        _beatDrop = 0;
        RuntimeManager.StudioSystem.setParameterByName("BeatDrop", _beatDrop);
        RuntimeManager.StudioSystem.setParameterByName("PuzzlesSolved", _puzzlesSolved);
    }

    private IEnumerator DropTimer()
    {
        yield return new WaitForSeconds(36.0f);
        _beatDrop = 0;
        RuntimeManager.StudioSystem.setParameterByName("BeatDrop", _beatDrop);
        //   Debug.Log("MM - done drop");
    }

    public void InvokeAfterRealtimeDelay(float seconds, System.Action callback)
    {
        if (callback == null) return;
        StartCoroutine(InvokeAfterRealtimeDelayRoutine(seconds, callback));
    }

    private IEnumerator InvokeAfterRealtimeDelayRoutine(float seconds, System.Action callback)
    {
        if (seconds > 0f)
            yield return new WaitForSecondsRealtime(seconds);
        callback.Invoke();
    }

}

