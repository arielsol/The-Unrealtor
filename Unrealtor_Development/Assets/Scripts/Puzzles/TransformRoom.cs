using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

public class TransformRoom : PuzzleBase
{
    public enum TransformRoomType
    {
        START,
        TUTORIAL_TRANSFORM,
        STUDIO_TO_HOUSE_TRANSFORM,
        ACTONE_TO_ACTTWO_TRANSFORM,
        ACTTWO_TO_ACTTHREE_TRANSFORM,
        NONE,
        TEST
    }

    public enum CutsceneType
    {
        FIGHT_CUTSCENE,
        NONE
    }
    
    
    [SerializeField] public TransformRoomType ThisTransformRoomType;
    
    /*[Header("Cutscene (optional)")]
    [SerializeField] public bool HasCutscene = false;
    [SerializeField] public PlayableAsset PlayableTimeline;
    [SerializeField] public CutsceneType ThisCutsceneType;*/
    
    [Header("Old Objects - Disable")]
    [SerializeField] public List<MatchBase.MatchType> OldMatches;
    [SerializeField] public List<DoorBase.DoorType> OldDoors;
    [SerializeField] public List<GravitySwitchZone.StairsType> OldStairs ;
    [SerializeField] private RoomEnvironment OldRoom;
    [SerializeField] public List<GameObject> OldObjects;
    
    [Header("New Objects - Enable")]
    [SerializeField] public List<MatchBase.MatchType> NewMatches;
    [SerializeField] public List<DoorBase.DoorType> NewDoors;
    [SerializeField] public List<GravitySwitchZone.StairsType> NewStairs ;
    [SerializeField] private RoomEnvironment NewRoom;
    [SerializeField] public List<GameObject> NewObjects;
    
    [SerializeField] public List<GameEvents.GameActionEnum> TriggeredHouseEvents;
    //[SerializeField] public Animator RoomAnim;
    
    [Header("Player Transform Effects")]
    [SerializeField] public Transform NewLPlayerTransform;
    [SerializeField] public Transform NewRPlayerTransform;
    
    private BehaviorGraphData BehaviorGraphSO;
    

    private void Start()
    {
        if (ThisTransformRoomType == TransformRoomType.START)
        {
            ShowRoomObjects();
            HideRoomObjects();
            ChangePlayerTransform();
            TriggerRoomEvents();
        }
        
    }

    public void ShowRoomObjects()
    {
        //set rooms/puzzles active
        if (NewRoom)
        {
            NewRoom.gameObject.SetActive(true);
        }
        if (NewObjects.Count > 0)
        {
            foreach (GameObject nextObject in NewObjects)
            {
                if(nextObject) nextObject.SetActive(true);
            }
        }

        if (NewMatches.Count > 0)
        {
            foreach (MatchBase.MatchType thisEnum in NewMatches)
            {
                MatchBase thisMatch =  PuzzlesPrefabContext.Instance.MatchBaseList.Find(match => match.MatchTypeEnum == thisEnum);
                if(thisMatch) thisMatch.gameObject.SetActive(true);
            }
        }
        if (NewDoors.Count > 0)
        {
            foreach (DoorBase.DoorType thisEnum in NewDoors)
            {
                DoorBase thisDoor =  PuzzlesPrefabContext.Instance.DoorBaseList.Find(door => door.ThisDoorType == thisEnum);
                if(thisDoor) thisDoor.gameObject.SetActive(true);
            }
        }
        if (NewStairs.Count > 0)
        {
            foreach (GravitySwitchZone.StairsType stairsEnum in NewStairs)
            {
                GravitySwitchZone thisStairs =  PuzzlesPrefabContext.Instance.GravityStairsList.Find(stairs => stairs.ThisStairsType == stairsEnum);
                if(thisStairs) thisStairs.gameObject.SetActive(true);
            }
        }
    }

    public void HideRoomObjects()
    {
        //set rooms/puzzles inactive
        if (OldRoom)
        {
            OldRoom.gameObject.SetActive(false);
        }
        if (OldObjects.Count > 0)
        {
            foreach (GameObject prevObject in OldObjects)
            {
                if(prevObject) prevObject.SetActive(false);
            }
        }
        
        if (OldMatches.Count > 0)
        {
            foreach (MatchBase.MatchType thisEnum in OldMatches)
            {
                MatchBase thisMatch =  PuzzlesPrefabContext.Instance.MatchBaseList.Find(match => match.MatchTypeEnum == thisEnum);
                if(thisMatch) thisMatch.gameObject.SetActive(false);
            }
        }
        if (OldDoors.Count > 0)
        {
            foreach (DoorBase.DoorType thisEnum in OldDoors)
            {
                DoorBase thisDoor =  PuzzlesPrefabContext.Instance.DoorBaseList.Find(door => door.ThisDoorType == thisEnum);
                if(thisDoor) thisDoor.gameObject.SetActive(false);
            }
        }
        if (OldStairs.Count > 0)
        {
            foreach (GravitySwitchZone.StairsType stairsEnum in OldStairs)
            {
                GravitySwitchZone thisStairs =  PuzzlesPrefabContext.Instance.GravityStairsList.Find(stairs => stairs.ThisStairsType == stairsEnum);
                if(thisStairs) thisStairs.gameObject.SetActive(false);
            }
        }
    }

    public void ChangePlayerTransform()
    {
        //move player
        if (NewLPlayerTransform)
        {
            PlayerInputManager.Instance.Player1.transform.position = NewLPlayerTransform.position;
            PlayerInputManager.Instance.Player1.transform.rotation = NewLPlayerTransform.rotation;
            Vector3 dir = -NewLPlayerTransform.up;
            
            StartCoroutine(ChangePlayerGrav(dir.normalized, true));
            
        }
        if (NewRPlayerTransform)
        {
            PlayerInputManager.Instance.Player2.transform.position = NewRPlayerTransform.position;
            PlayerInputManager.Instance.Player2.transform.rotation = NewRPlayerTransform.rotation;
            Vector3 dir = -NewRPlayerTransform.up;
            
            StartCoroutine(ChangePlayerGrav(dir.normalized, false));
        }
    }
    
    public static IEnumerator ChangePlayerGrav(Vector3 newGrav, bool isPlayer1)
    {
        yield return new WaitForSeconds(0.1f);
        
        if(isPlayer1) PlayerInputManager.Instance.Player1.SwitchGravity(newGrav);
        else PlayerInputManager.Instance.Player2.SwitchGravity(newGrav);
    }

    public void TriggerRoomEvents()
    {
        foreach (GameEvents.GameActionEnum actionEnum in TriggeredHouseEvents)
        {
            GameEvents.GameTrigger(actionEnum);
        }
    }
    
    

}
