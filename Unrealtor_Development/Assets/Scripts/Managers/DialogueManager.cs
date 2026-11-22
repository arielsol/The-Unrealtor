using UnityEngine;
using Yarn.Unity;
using FMODUnity;
using FMOD.Studio;
using System.Collections;
using System.Collections.Generic;
using System;
using Cinemachine;
using UnityCommunity.UnitySingleton;
using UnityEngine.SceneManagement;

public class DialogueManager : MonoSingleton<DialogueManager>
{
    //public static DialogueManager Instance;

    public string startingDialogue;
    public bool StartAutomatically = true;

    private PlayerInputManager PIM;
    private CinemachineBrain Player1Brain;
    private CinemachineBrain Player2Brain;

    public DialogueScriptableObject currDialogue;
    private DialogueRunner Runner;
    public EventInstance currVOSound;

    [SerializeField] private TMPro.TMP_Text CurrentLine;

    [SerializeField] private List<string> StartList = new List<string>();
    [SerializeField] private List<string> IntroList = new List<string>();
    [SerializeField] private List<string> PlungerList = new List<string>();
    [SerializeField] private List<string> ExtinguisherList = new List<string>();
    [SerializeField] private List<string> DrainList = new List<string>();

    [SerializeField] private LinePresenter Player1Presenter;
    [SerializeField] private LineAdvancer Advancer;


    private bool InCutscene = false;

    private bool inStart = true;
    private bool inIntro = false;
    private bool inPlunger = false;
    private bool inExtinguisher = false;
    private bool inDrain = false;
    private bool keyDone = false;
    private bool puzzleDone = false;
    private bool drainDone = false;

    public bool DebugDialogue = false;

    public static event Action<string> OnDialogueCompleted;
    private string currentDialogueName;
    private bool isCompletingDialogue;
    private Coroutine currCoroutine;

    //hint bark variables
    private List<KeyValuePair<string, Coroutine>> solveDialogueList = new List<KeyValuePair<string, Coroutine>>();


    public bool IsDialoguePlaying
    {
        get { return currDialogue != null; }
    }

    public bool IsCompletingDialogue
    {
        get { return isCompletingDialogue; }
    }

    void Start()
    {
        /*f (Instance != null && Instance != this)

        {

            Destroy(gameObject);

            return;

        }

        Instance = this;*/

        Runner = GetComponentInChildren<DialogueRunner>();
        Runner.onDialogueComplete.AddListener(OnDialogueEnd);

        GameEvents.OnCutsceneStateChanged += CutsceneStateChanged;

        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "StartMenuScene")
        {
            PIM = PlayerInputManager.Instance;
            Player1Brain = GameObject.Find("LPlayer1CamBrain").GetComponent<CinemachineBrain>();
            Player2Brain = GameObject.Find("RPlayer2CamBrain").GetComponent<CinemachineBrain>();
        }

        Player1Presenter = GameObject.Find("Line Presenter").GetComponent<LinePresenter>();

        Advancer = GameObject.Find("Line Advancer").GetComponent<LineAdvancer>();

        if (StartAutomatically) { StartDialogue(startingDialogue); }

        GameEvents.OnFlood += SetPuzzle;
        GameEvents.OnDrainSolve += SetDrain;

    }


    void CutsceneStateChanged(GameManager.CutsceneState cs)
    {
        if (cs == GameManager.CutsceneState.CUTSCENE)
        {
            InCutscene = true;
            EndDialogue();
        }
        else
        {
            InCutscene = false;
        }
    }


    void Update()
    {

    }

    void OnDialogueEnd()
    {
        isCompletingDialogue = true;
        string completedDialogueName = currentDialogueName;
        currDialogue = null;
        currentDialogueName = null;
        currVOSound.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        currVOSound.release();

        if (!string.IsNullOrEmpty(completedDialogueName))
        {
            OnDialogueCompleted?.Invoke(completedDialogueName);
        }
        isCompletingDialogue = false;
    }

    public void EndDialogue()
    {
        Runner.Stop();
        OnDialogueEnd();
    }

    public bool StartDialogue(string name)
    {
        //player completed before bark so hint doesn't play
        foreach (KeyValuePair<string, Coroutine> kvp in solveDialogueList)
        {
            if (kvp.Key == name)
            {
                if (DebugDialogue) Debug.Log("[DM] Stopping Hint due to: " + name);
                StopCoroutine(kvp.Value);
                solveDialogueList.Remove(kvp);
                break;
            }
        }

        if (DebugDialogue) Debug.Log("[DM] Top of Starting: " + name);

        //if (InCutscene)
        //{
        //    if (DebugDialogue) Debug.Log("[DM] Dialogue In Cutscene");
        //    return false;
        //}

        //start nodes 
        if (inStart)
        {
            if (!StartList.Contains(name))
            {
                if (DebugDialogue) Debug.Log("[DM] dialogue not in start nodes");
                return false;
            }
        }

        //intro nodes
        if (inIntro)
        {
            if (!IntroList.Contains(name))
            {
                if (DebugDialogue) Debug.Log("[DM] dialogue not in intro nodes");
                return false;
            }
        }

        //extinguisher/plunger nodes
        if (inPlunger || inExtinguisher)
        {
            int count = 0;
            if (!PlungerList.Contains(name) && inPlunger)
            {
                if (DebugDialogue) Debug.Log("[DM] dialogue not in plunger nodes");
                count++;
            }
            if (!ExtinguisherList.Contains(name) && inExtinguisher)
            {
                if (DebugDialogue) Debug.Log("[DM] dialogue not in extinguisher nodes");
                count++;
            }
            if (count == 2)
            {
                return false;
            }

        }

        //cloglook and firelook hard coding
        if (inPlunger && (name == "ClogLook" || name == "ClogLook2"))
        {
            if (DebugDialogue) Debug.Log("[DM] dialogue cloglook not allowed after plunger");
            return false;
        }

        if (inExtinguisher && name == "FireLook")
        {
            if (DebugDialogue) Debug.Log("[DM] dialogue firelook not allowed after extinguisher");
            return false;
        }

        if (keyDone && name == "DoorLook")
        {
            if (DebugDialogue) Debug.Log("[DM] dialogue doorlook not allowed after key");
            return false;
        }

        if (puzzleDone && (name == "ClosetLook" || name == "ClosetLook2" || name == "DoorknobTimer"))
        {
            if (DebugDialogue) Debug.Log("[DM] dialogue closetlook/draintimer not allowed after puzzle");
            return false;
        }

        if (drainDone && (name == "WaterfallLook" || name == "HalfHintDrain"))
        {
            if (DebugDialogue) Debug.Log("[DM] dialogue waterfalllook not allowed after drain");
            return false;
        }

        if (InCutscene && (name == "ClosetLook" || name == "ClosetLook2"))
        {
            if (DebugDialogue) Debug.Log("[DM] dialogue closetlook not allowed in cutscene EVER!!!!!!");
            return false;
        }

        if (InCutscene && (name.Contains("Half")))
        {
            if (DebugDialogue) Debug.Log("[DM] dialogue with Half not allowed in cutscene EVER!!!!!!");
            return false;
        }

        //drain nodes
        if (inDrain)
        {
            if (!DrainList.Contains(name))
            {
                if (DebugDialogue) Debug.Log("[DM] dialogue not in drain nodes");
                return false;
            }
        }

        // Debug.Log("3[DM] Top of Starting: " + name);
        DialogueScriptableObject newDialogue = Resources.Load<DialogueScriptableObject>(name);

        if (newDialogue == null)
        {
            if (DebugDialogue) Debug.Log("[DM] Dialogue not found name: " + name);
            return false;
        }
        else if (currDialogue == newDialogue)
        {
            if (DebugDialogue) Debug.Log("[DM] Same Dialogue Called");
            return false;
        }
        else if (currDialogue != null && currDialogue.priority >= newDialogue.priority)
        {
            if (DebugDialogue) Debug.Log("[DM] New Dialogue too low Priority. Curr: " + currDialogue.priority + " New: " + newDialogue.priority);
            return false;
        }
        else
        {
            //Runner.Stop();
            if (DebugDialogue) Debug.Log("[DM] Trying to play: " + name + " new priority: " + newDialogue.priority);
            currDialogue = newDialogue;
            currentDialogueName = name;
            Runner.StartDialogue(name);
            return true;
        }
        if (DebugDialogue) Debug.Log("[DM] Should not be reached");
    }

    [YarnCommand("StartDialogue")]
    public void YarnStartDialogue(string name)
    {
        StartDialogue(name);
    }

    [YarnCommand("CallVO")]
    public void CallVO(int idx)
    {
        if (!currDialogue) return;

        float finalVolume = currDialogue.dialogueVOVolume;
        float time = MusicManager.Instance.CallVO(idx, currDialogue, ref currVOSound, finalVolume);

        if (currCoroutine != null)
        {
            StopCoroutine(currCoroutine);
        }

        currCoroutine = StartCoroutine(CallNextLine(time));
    }

    [YarnCommand("SetIntro")]
    public void SetIntro()
    {
        if (DebugDialogue) Debug.Log("[DM] Currently in intro nodes");
        inIntro = true;
        inStart = false;
    }

    [YarnCommand("SetKey")]
    public void SetKey()
    {
        if (DebugDialogue) Debug.Log("[DM] Currently in key nodes");
        keyDone = true;
    }

    [YarnCommand("SetPlunger")]
    public void SetPlunger()
    {
        if (DebugDialogue) Debug.Log("[DM] Currently in plunger nodes");
        inIntro = false;
        inPlunger = true;
    }

    [YarnCommand("SetExtinguisher")]
    public void SetExtinguisher()
    {
        if (DebugDialogue) Debug.Log("[DM] Currently in extinguisher nodes");
        inIntro = false;
        inExtinguisher = true;
    }

    [YarnCommand("SetDrain")]
    public void SetDrain()
    {
        if (DebugDialogue) Debug.Log("[DM] Currently in drain nodes");
        inExtinguisher = false;
        inPlunger = false;
        inDrain = true;
    }

    [YarnCommand("SetPuzzle")]
    public void SetPuzzle()
    {
        if (DebugDialogue) Debug.Log("[DM] Currently in post-puzzle");
        puzzleDone = true;
        if (drainDone)
        {
            SetDrain();
        }
    }

    [YarnCommand("SetShower")]
    public void SetShower()
    {
        if (DebugDialogue) Debug.Log("[DM] Currently in post-shower");
        drainDone = true;
        if (puzzleDone)
        {
            SetDrain();
        }
    }


    [YarnCommand("SetInactive")]
    public static void SetInactive(GameObject obj)
    {
        Debug.Log("[DM] deactivate");
        obj.SetActive(false);
    }

    [YarnCommand("StopPlayerMovement")]
    public void StopPlayerMovement()
    {
        if (DebugDialogue) Debug.Log("[DM] stop movement");
        PIM.SetAllPlayersInputPaused(true);

    }

    [YarnCommand("ResumePlayerMovement")]
    public void ResumePlayerMovement()
    {
        if (DebugDialogue) Debug.Log("[DM] resume movement");
        PIM.SetAllPlayersInputPaused(false);

    }

    public void PauseDialogue()
    {
        if (DebugDialogue) Debug.Log("[DM] Pause");
        Player1Brain.enabled = false;
        Player2Brain.enabled = false;
        currVOSound.setPaused(true);
        Time.timeScale = 0.0f;
    }

    public void PlayDialogue()
    {
        if (DebugDialogue) Debug.Log("[DM] Play");
        Player1Brain.enabled = true;
        Player2Brain.enabled = true;
        currVOSound.setPaused(false);
        Time.timeScale = 1.0f;
        //GameObject.Find("Line Advancer").GetComponent<LineAdvancer>().RequestLineHurryUp();

    }

    IEnumerator CallNextLine(float time)
    {
        // Debug.Log("CallVO2");
        yield return new WaitForSeconds(time + 0.5f);
        Advancer.RequestNextLine();
        //Debug.Log("CallVO3");
    }

    [YarnCommand("DialogueDelay")]
    public void PuzzleBarkCall(float time, string hintDialogue, string solve)
    {
        Coroutine co = StartCoroutine(StartBarkTimer(time, hintDialogue, solve));
        KeyValuePair<string, Coroutine> tempPair = new KeyValuePair<string, Coroutine>(solve, co);
        solveDialogueList.Add(tempPair);
    }

    IEnumerator StartBarkTimer(float time, string hint, string solve)
    {
        if (DebugDialogue) Debug.Log("[DM] Delaying: " + hint);
        yield return new WaitForSeconds(time);
        StartDialogue(hint); //timer finished and hint plays
        foreach (KeyValuePair<string, Coroutine> kvp in solveDialogueList)
        {
            if (kvp.Key == solve)
            {
                solveDialogueList.Remove(kvp);
                break;
            }
        }
    }
}
