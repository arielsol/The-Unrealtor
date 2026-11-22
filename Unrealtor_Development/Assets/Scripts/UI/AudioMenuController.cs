using UnityEngine;
using UnityEngine.UI;

public class AudioMenuController : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] private Image MasterBar;
    [SerializeField] private Image VOBar;
    [SerializeField] private Image SFXBar;
    [SerializeField] private Image MusicBar;

    void Start()
    {
        MasterBar.fillAmount = MusicManager.Instance.masterVolume;
        MusicBar.fillAmount = MusicManager.Instance.musicVolume;
        SFXBar.fillAmount = MusicManager.Instance.sfxVolume;
        VOBar.fillAmount = MusicManager.Instance.voVolume;
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void AdjustMaster(bool increase = true)
    {
        float vol = MusicManager.Instance.masterVolume;
        if (increase)
        {
            MusicManager.Instance.PlaySFX(SoundID.AudioUpClicked, Vector3.zero);
            MusicManager.Instance.SetMasterVolume(vol + 0.1f);
            MasterBar.fillAmount = Mathf.Clamp01(vol + 0.1f);
        }
        if (!increase)
        {
            MusicManager.Instance.PlaySFX(SoundID.AudioDownClicked, Vector3.zero);
            MusicManager.Instance.SetMasterVolume(vol - 0.1f);
            MasterBar.fillAmount = Mathf.Clamp01(vol - 0.1f);
        }


    }

    public void AdjustMusic(bool increase = true)
    {
        float vol = MusicManager.Instance.musicVolume;
        if (increase)
        {
            MusicManager.Instance.PlaySFX(SoundID.AudioUpClicked, Vector3.zero);
            MusicManager.Instance.SetMusicVolume(vol + 0.1f);
            MusicBar.fillAmount = Mathf.Clamp01(vol + 0.1f);
        }
        if (!increase)
        {
            MusicManager.Instance.PlaySFX(SoundID.AudioDownClicked, Vector3.zero);
            MusicManager.Instance.SetMusicVolume(vol - 0.1f);
            MusicBar.fillAmount = Mathf.Clamp01(vol - 0.1f);
        }


    }

    public void AdjustSFX(bool increase = true)
    {
        float vol = MusicManager.Instance.sfxVolume;
        if (increase)
        {
            MusicManager.Instance.PlaySFX(SoundID.AudioUpClicked, Vector3.zero);
            MusicManager.Instance.SetSFXVolume(vol + 0.1f);
            SFXBar.fillAmount = Mathf.Clamp01(vol + 0.1f);
        }
        if (!increase)
        {
            MusicManager.Instance.PlaySFX(SoundID.AudioDownClicked, Vector3.zero);
            MusicManager.Instance.SetSFXVolume(vol - 0.1f);
            SFXBar.fillAmount = Mathf.Clamp01(vol - 0.1f);
        }


    }

    public void AdjustVO(bool increase = true)
    {
        float vol = MusicManager.Instance.voVolume;
        if (increase)
        {
            MusicManager.Instance.PlaySFX(SoundID.AudioUpClicked, Vector3.zero);
            MusicManager.Instance.SetVOVolume(vol + 0.1f);
            VOBar.fillAmount = Mathf.Clamp01(vol + 0.1f);
        }
        if (!increase)
        {
            MusicManager.Instance.PlaySFX(SoundID.AudioDownClicked, Vector3.zero);
            MusicManager.Instance.SetVOVolume(vol - 0.1f);
            VOBar.fillAmount = Mathf.Clamp01(vol - 0.1f);
        }


    }
}
