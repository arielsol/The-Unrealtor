using System;
using System.Collections.Generic;
using DG.Tweening;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;


public class DoorBase : PuzzleBase
{
    public enum DoorType
    {
        SAMPLEDOOR,
        INTRO,
        C_BEDROOM,
        BATHROOM,
        P_BEDROOM,
        VOID,
        VOID2,
        NONE
    }
    
    
    [SerializeField] private int TotalNumOfPuzzles;
    //[SerializeField] private Vector3 OpenRotation = new Vector3(0,-45,0);
    [SerializeField] public DoorType ThisDoorType;

    [SerializeField] private GameObject LHalf;
    [SerializeField] private GameObject RHalf;
    [SerializeField] private GameObject Silhouette;
    
    [SerializeField] private Collider col;

    private Image[] Icons;

    private bool debug = false;

    private int currPuzzleNum = 0;
    
    private bool IsOpen = false;
    public bool IsReadyToOpen = false;

    private BehaviorGraphData BehaviorGraphSO;

    [Tooltip("Mapping of what match -> door index")]
    [SerializeField]public List<MatchBase.MatchType> doorKeyList;

    [SerializeField] private int RotationAngle = 90;
    public AudioSource asource;
    public AudioClip openDoor,closeDoor;
    

    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        Icons = GetComponentsInChildren<Image>();

    }

    private void Start()
    {
        BehaviorGraphSO = MatchManager.Instance.BehaviorGraphSO;
    
    }


    public void OpenDoor()
    {
        //if (LHalf != null) LHalf.transform.Rotate( new Vector3(0,RotationAngle,0)); 
        //if (RHalf != null) RHalf.transform.Rotate(new Vector3(0,-RotationAngle,0));
        if (!IsOpen)
        {
            if (LHalf != null) LHalf.transform.DOLocalRotate(new Vector3(0, RotationAngle, 0), 3.0f);
            if (RHalf != null) RHalf.transform.DOLocalRotate(new Vector3(0, -RotationAngle, 0), 3.0f);

            IsOpen = true;
            MusicManager.Instance?.PlaySFX(SoundID.DoorOpen, Vector3.zero);
            if (asource)
            {
                asource.clip = IsOpen ? openDoor : closeDoor;
                asource.Play();
            }

            DeactivateCollider(col);
        }
    }

    public void SilhouetteOff()
    {
        if (Silhouette)
        {
            Silhouette.gameObject.SetActive(false);
        }
    }
    
    public void DeactivateCollider ( Collider other )
    {
        if (IsOpen) col.enabled = false;
        else col.enabled = true;
    }

    private void OnEnable()
    {
        BehaviorGraphSO  = MatchManager.Instance.BehaviorGraphSO;
        if (BehaviorGraphSO)
        {
            BehaviorGraphSO.ReceiveDoorOpenEC.Event += OnOpenDoorEvent;
        }
        else
        {
            Debug.Log("Missing OpenDoorGraphEvent on" + gameObject.name);
        }
        
    }

    private void OnDisable()
    {
        if (BehaviorGraphSO)
        {
            BehaviorGraphSO.ReceiveDoorOpenEC.Event -= OnOpenDoorEvent;
        }
        else
        {
            Debug.Log("Missing OpenDoorGraphEvent on" + gameObject.name);
        }
    }

    private void OnOpenDoorEvent(PuzzleBase match)
    {
        DoorBase inputDoor = match as DoorBase;
        if (inputDoor)
        {
            if (inputDoor.ThisDoorType == ThisDoorType)
            {
                    Debug.Log("Door Open event recieved" + match.gameObject.name + " on object " + gameObject.name);
                    IsReadyToOpen = true;
                    //OpenDoor();
            }
        }
        
    }

    public bool CheckDoorPrereqs()
    {
        bool isUnlocked = true;
        foreach (MatchBase.MatchType matchType in doorKeyList)
        {
            if (matchType != null)
            {
                MatchBase match =
                    PuzzlesPrefabContext.Instance.MatchBaseList.Find(match => match.MatchTypeEnum == matchType);
                if (match != null)
                {
                    if (!match.isSolved) isUnlocked = false;
                    
                }
            }
        }
        
        return isUnlocked;
    }
    
    

    
        
    

}
