using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityCommunity.UnitySingleton;

public class GameManager : MonoSingleton<GameManager>    
{
    public enum GameState
    {
        TUTORIAL = 0,
        ACT1_RENOVATED = 1, //Act 1
        ACT2_GENTRIFIED = 2, //Act 2
        ACT3_ESCHERISTIC = 3 //Act 3
        
    }

    public enum CutsceneState
    {
        GAMEPLAY = 0,
        CUTSCENE = 1
    }
    
    public CutsceneState CurrentCutsceneState { get; private set; }

    public void SetCutsceneState(CutsceneState newState)
    {
        CurrentCutsceneState = newState;
        GameEvents.InvokeCutsceneChanged(newState);
        //OnCutsceneStateChanged?.Invoke(newState);
    }

    [System.Serializable] 
    public class DegredationStateInfo
    {
        public string id;
        public GameState stateEnum;
        public List<MatchBase.MatchType> matchOrder;
    }

    [SerializeField] public List<DegredationStateInfo> DegredationStateList = new List<DegredationStateInfo>();
    private GameState CurrentStateEnum;
    private BehaviorGraphData BehaviorGraphSO;

    private void OnEnable()
    {
        BehaviorGraphSO = MatchManager.Instance.BehaviorGraphSO;
        if (BehaviorGraphSO)
        {
            BehaviorGraphSO.ReceivePuzzleCompletedGraphEvent.Event += OnUpdateGameState;
        }
        else
        {
            Debug.Log("Missing Graph Event: " + gameObject.name);
        }
    }

    private void OnDisable()
    {
        if (BehaviorGraphSO)
        {
            BehaviorGraphSO.ReceivePuzzleCompletedGraphEvent.Event -= OnUpdateGameState;
        }
        
    }

    private void Start()
    {
        GameplayMetricsCollector.RecordActStart(GameState.TUTORIAL);
        MusicManager.Instance.PlaySound(SoundID.Ambience);
        UIManager.Instance.ShowUI("Tutorial");
        
    }

    private void OnUpdateGameState(PuzzleBase matchBase)
    {
        int stateNum = (int)CurrentStateEnum;
        bool isAllMatched = true;

        //Commented out because it was preventing match from happening 

        /*
        if(DegredationStateList[stateNum].matchOrder.Count >0)
        {
            foreach(MatchBase.MatchType type in DegredationStateList[stateNum].matchOrder)
            {
                MatchBase thisBase = PuzzlesPrefabContext.Instance.MatchBaseList.Find(m => m.MatchTypeEnum == type);
                if(thisBase)
                {
                    if (!thisBase.isSolved)
                    {
                        isAllMatched = false;
                        break;
                    }
                }
            }
        }
        
        if (isAllMatched)
        {
            GameplayMetricsCollector.RecordActComplete(CurrentStateEnum);
            CurrentStateEnum = (GameState)stateNum++;
            GameplayMetricsCollector.RecordActStart(CurrentStateEnum);
            Debug.Log((GameState)stateNum + " complete! Current state: " +
                      (GameState)(stateNum-1));
        }
        */
        
    }
}
