using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using Mono.CSharp;
public enum PlayerSide
{
    Left,
    Right,
}

public class FootstepPlayer : MonoBehaviour
{

    [SerializeField] private PlayerSide playerSide;
    [SerializeField] private EventReference leftFootstepEvent;
    [SerializeField] private EventReference rightFootstepEvent;
    [SerializeField] private float footstepLockout = 0.25f;
    [SerializeField] private float velocityLockout = 0.1f;
    
    private MusicManager musicManager;
    private Rigidbody rigidBody;

    private int lastLeftFootstepFrame = -1;
    private float nextAllowedTimeLeft = 0f;

    private int lastRightFootstepFrame = -1;
    private float nextAllowedTimeRight = 0f;

    void Start()
    {
        // Doing this on Start because on Awake was causing initialization order issues
        musicManager = MusicManager.Instance;
        rigidBody = GetComponentInParent<Rigidbody>();
    }

    void Update()
    {
        if (rigidBody != null && rigidBody.linearVelocity.magnitude < velocityLockout) return;

        PlayFootstepFromTimer(playerSide);
    }

    private void PlayFootstepFromTimer(PlayerSide playerSide)
    {
        if (playerSide == PlayerSide.Left)
        {
            if (!CanPlayLeftFootstep()) return;

            musicManager.PlaySFX(SoundID.FootstepLeft, Vector3.zero);
        }
        else if (playerSide == PlayerSide.Right)
        {
            if (!CanPlayRightFootstep()) return;

            musicManager.PlaySFX(SoundID.FootstepRight, Vector3.zero);
        }
    }

    private bool CanPlayLeftFootstep()
    {
        if (Time.frameCount == lastLeftFootstepFrame)
            return false;

        if (Time.time < nextAllowedTimeLeft)
            return false;

        lastLeftFootstepFrame = Time.frameCount;
        nextAllowedTimeLeft = Time.time + footstepLockout;

        return true;
    }

    private bool CanPlayRightFootstep()
    {
        if (Time.frameCount == lastRightFootstepFrame)
            return false;

        if (Time.time < nextAllowedTimeRight)
            return false;

        lastRightFootstepFrame = Time.frameCount;
        nextAllowedTimeRight = Time.time + footstepLockout;

        return true;
    }

    public void PlayLeftFootstepFromTimer()
    {
        if (rigidBody != null && rigidBody.linearVelocity.magnitude < velocityLockout) return;

        musicManager.PlaySFX(SoundID.FootstepLeft, Vector3.zero);
    }

    public void PlayRightFootstepFromTimer()
    {
        if (rigidBody != null && rigidBody.linearVelocity.magnitude < velocityLockout) return;

        musicManager.PlaySFX(SoundID.FootstepRight, Vector3.zero);
    }

    public void PlayLeftFootstep()
    {
        // Animation is too fast, removing this for now
        /*
        if (rigidBody != null && rigidBody.linearVelocity.magnitude < velocityLockout) return;

        if (!CanPlayLeftFootstep()) return;

        musicManager.PlaySFX(SoundID.FootstepLeft, Vector3.zero);

        // Debug.Log("Left footstep triggered");
        */
    }

    public void PlayRightFootstep()
    {
        // Animation is too fast, removing this for now
        /*
        if (rigidBody != null && rigidBody.linearVelocity.magnitude < velocityLockout) return;

        if (!CanPlayRightFootstep()) return;

        musicManager.PlaySFX(SoundID.FootstepRight, Vector3.zero);

        // Debug.Log("Right footstep triggered");
        */
    }
}