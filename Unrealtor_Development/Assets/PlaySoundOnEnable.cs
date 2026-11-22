using UnityEngine;

public class PlaySoundOnEnable : MonoBehaviour
{
    [SerializeField] private SoundID soundID;
    private void OnEnable()
    {
        MusicManager.Instance?.PlaySFX(soundID, Vector3.zero);
    }
}
