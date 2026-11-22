using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityCommunity.UnitySingleton;
using UnityEngine;

public class EnvironmentPrefabContext : MonoSingleton<EnvironmentPrefabContext>
{
    [SerializeField] public List<TransformRoom> TransformRoomList = new List<TransformRoom>();
    [SerializeField] public List<RoomEnvironment> RoomEnviornmentList = new List<RoomEnvironment>();

    [Header("Environmental Effects")] 
    [SerializeField] public GameObject Water;
    [SerializeField] public float WaterHeightDiff;
    [SerializeField] public float WaterLevelTransitionTime;
    
    [SerializeField] public GameObject Fire;
    private bool _studioAudioActive;
    private bool _livingRoomAudioActive;

    [Header("Testing info")]
    [SerializeField] public List<Transform> PlayerPositionList = new List<Transform>();
    //index 0 = studio, 1 = living room, 2= childrens bedroom, 3 = bathroom
    
    private void OnEnable()
    {
        GameEvents.OnFlood += HandleFlood;
        GameEvents.OnDrainFlood += HandleDrainFlood;
        GameEvents.OnCatchFire += HandleCatchFire;
        GameEvents.OnPutOutFire += HandlePutOutFire;
        
    }

    private void Start()
    {
        RefreshStudioAudio();
    }

    private void OnDisable()
    {
        GameEvents.OnFlood -= HandleFlood;
        GameEvents.OnDrainFlood -= HandleDrainFlood;
        GameEvents.OnCatchFire -= HandleCatchFire;
        GameEvents.OnPutOutFire -= HandlePutOutFire;
    }

    public void ShowNextRoomObjects(TransformRoom.TransformRoomType targetType)
    {
        foreach (TransformRoom roomInfo in TransformRoomList)
        {
            if (roomInfo != null && roomInfo.ThisTransformRoomType == targetType)
            {
                roomInfo.ShowRoomObjects();
                Debug.Log("Triggered TransformRoom on " + roomInfo.name);
            }
        }
        RefreshStudioAudio();

    }
    
    public void HidePrevRoomObjects(TransformRoom.TransformRoomType targetType)
    {
        foreach (TransformRoom roomInfo in TransformRoomList)
        {
            if (roomInfo != null && roomInfo.ThisTransformRoomType == targetType)
            {
                roomInfo.HideRoomObjects();
                Debug.Log("Triggered TransformRoom on " + roomInfo.name);
            }
        }
        RefreshStudioAudio();

    }
 
    public void TriggerRoomEvents(TransformRoom.TransformRoomType targetType)
    {
        foreach (TransformRoom roomInfo in TransformRoomList)
        {
            if (roomInfo != null && roomInfo.ThisTransformRoomType == targetType)
            {
                roomInfo.TriggerRoomEvents();
                Debug.Log("Triggered TransformRoom on " + roomInfo.name);
            }
        }

    }

    public void ChangePlayerTransf(TransformRoom.TransformRoomType targetType)
    {
        foreach (TransformRoom roomInfo in TransformRoomList)
        {
            if (roomInfo != null && roomInfo.ThisTransformRoomType == targetType)
            {
                roomInfo.ChangePlayerTransform();
                Debug.Log("Triggered TransformRoom on " + roomInfo.name);
            }
        }

    }
    
    void HandleFlood() 
    {
        if (Water)
        {
            Water.SetActive(true);
            Water.transform.DOMove(Water.transform.position + new Vector3(0, WaterHeightDiff, 0),
                WaterLevelTransitionTime);
        }
    }

    void HandleDrainFlood()
    {
        if (Water)
        {
            Water.transform.DOMove(Water.transform.position - new Vector3(0, WaterHeightDiff, 0),
                WaterLevelTransitionTime);
        }
    }
    
    void HandleCatchFire()
    {
        
    }

    void HandlePutOutFire()
    {
        
    }

    public bool RoomHasCutscene(TransformRoom.TransformRoomType targetType)
    {
        foreach (TransformRoom roomInfo in TransformRoomList)
        {
            if (roomInfo != null && roomInfo.ThisTransformRoomType == targetType)
            {
                //return roomInfo.HasCutscene;
            }
        }

        return false;
    }

    public TransformRoom GetRoomBase(TransformRoom.TransformRoomType targetType)
    {
        foreach (TransformRoom roomInfo in TransformRoomList)
        {
            if (roomInfo != null && roomInfo.ThisTransformRoomType == targetType)
            {
                return roomInfo;
            }
        }

        return null;
    }

    private void RefreshStudioAudio()
    {
        bool isStudioActive = false;
        bool isLivingRoomActive = false;
        foreach (RoomEnvironment room in RoomEnviornmentList)
        {
            if (room == null || !room.gameObject.activeInHierarchy)
                continue;

            if (room.ThisRoomEnvironmentType == RoomEnvironment.RoomEnvironmentType.STUDIOROOM)
            {
                isStudioActive = true;
            }
            else if (room.ThisRoomEnvironmentType == RoomEnvironment.RoomEnvironmentType.ACTONEROOM)
            {
                // ACTONEROOM is the living room / bedroom phase after the studio intro.
                isLivingRoomActive = true;
            }
        }

        if (MusicManager.Instance == null)
            return;

        if (isStudioActive && !_studioAudioActive)
        {
            MusicManager.Instance.PlaySound(SoundID.StudioAmbience, Vector3.zero);
            MusicManager.Instance.PlaySound(SoundID.BuzzingFluorescentLights, Vector3.zero);
            _studioAudioActive = true;
        }
        else if (!isStudioActive && _studioAudioActive)
        {
            MusicManager.Instance.StopSound(SoundID.StudioAmbience);
            MusicManager.Instance.StopSound(SoundID.BuzzingFluorescentLights);
            _studioAudioActive = false;
        }

        if (isLivingRoomActive && !_livingRoomAudioActive)
        {
            MusicManager.Instance.PlaySound(SoundID.LivingRoomBedroomAmbience, Vector3.zero);
            _livingRoomAudioActive = true;
        }
        else if (!isLivingRoomActive && _livingRoomAudioActive)
        {
            MusicManager.Instance.StopSound(SoundID.LivingRoomBedroomAmbience);
            _livingRoomAudioActive = false;
        }
    }
   
}
