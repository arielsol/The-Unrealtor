using System;
using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityCommunity.UnitySingleton;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using Vector3 = UnityEngine.Vector3;

public class MatchManager : MonoSingleton<MatchManager>
{
    public enum MatchState
    {
        SEARCHING,
        MATCHHOLD,
        MATCHCOMPLETE,
        MATCHEFFECT,
        ALLMATCHESCOMPLETE
    }

    private struct MatchProximity
    {
        public bool isPassing;
        public float closeness;
        public bool yAligned;
        public float LXcloseness;
        public float RXcloseness;
        public float LYcloseness;
        public float RYcloseness;
    }


    [Header("InfoPerQuad")]
    private List<MatchBase> MatchBaseList = new List<MatchBase>();
    private List<MatchBase> NearbyMatches = new List<MatchBase>();

    public MatchUI GeneralUI { get; private set; }

    private MatchBase CurrMatch;
    private MatchState CurrMatchState;
    private TwoPlayerInput PlayerControls;
    private DialogueManager DialogueManagerObj;

    [SerializeField] public BehaviorGraphData BehaviorGraphSO;

    [NonSerialized] public bool IsViewportOneCamera = false;
    [NonSerialized] public float ViewportShiftSpeed = 0.15f;
    
    [Header("Post Match Sequence")]
    [SerializeField] public float PuzzleCamDuration = 2.0f;
    [SerializeField] public float CompleteCamZoomSpeed = 0.15f;
    [SerializeField] public float CompleteCamDuration = 3.0f;
    [SerializeField] public float DelayedEffectTimeOffset = 3.0f;
    [Tooltip("Wall time after MatchSuccess in MatchComplete before puzzle-type stinger (e.g. plunger pop). Uses unscaled time.")]
    [SerializeField, Min(0f)] private float matchStingerDelayAfterSuccess = 3f;
    
    [SerializeField] private AnimationCurve camBumpCurve;
    [SerializeField] private float camBumpDuration;
    [SerializeField] private float camBumpStrength;

    [Header("Submit")]
    [SerializeField] public float AutoSubmitDuration = 0.5f;
    [SerializeField] private bool IsAutoSubmit = false;

    [Header("Cutscene")]                     // <-- NEW
    [SerializeField] private PlayableDirector matchCutsceneDirector;  // <-- NEW

    [SerializeField] private MiddleIndicatorController MiddleIndicator; // new script
    [SerializeField] private float MiddleHintMinCloseness = 0.3f; // threshold to start hinting

    private MatchBase _closestMatchThisFrame;
    private float _closestMatchCloseness;
    private MatchBase _lastApproachMatch;
    [SerializeField] private float approachSoundThreshold = 0.55f;

    [Header("Silhouette Settings")]
    [SerializeField]
    private float SilhouetteShowThreshold = 0.35f;

    [SerializeField] private float SilhouetteHideThreshold = 0.25f;
    [SerializeField] private float SilhouetteGraceTime = 0.2f;
    [SerializeField] private float SilhouetteSwitchBias = 0.05f;
    private MatchBase _activeSilhouetteMatchRef;
    private float _activeSilhouetteGraceTimer;
    private float _activeSilhouetteCloseness;

    [Header("Silhouette UI (Shift + Near)")]
    [SerializeField] private SilhouetteHint leftSilhouetteHint;
    [SerializeField] private SilhouetteHint rightSilhouetteHint;


    private float AutoSubmitTimer = 0.0f;
    private int objectiveIndex = 0;

    [Header("Testing")]
    [SerializeField] public bool DebugMode;
    [SerializeField] public MatchBase.MatchType CameraTestMatch;
    [SerializeField] public Transform TestLPlayerTransform;
    [SerializeField] public Transform TestRPlayerTransform;

    private InputAction CameraShortcut;
    private InputAction PlayerTransformShortcut;
    private InputAction SkipCamShortcut;
    
    private Coroutine matchCompleteCoroutine;
    private bool hasMatchCutsceneRunInBackgroundOverride;
    private bool previousRunInBackground;

    private void Awake()
    {
        Debug.Log("MatchManager Awake: " + gameObject.name);
    }
    void Start()
    {
        GeneralUI = GameObject.Find("GeneralUI").GetComponent<MatchUI>();
        DialogueManagerObj = GameObject.Find("DialogueManager").GetComponent<DialogueManager>();
        MatchBaseList = PuzzlesPrefabContext.Instance.MatchBaseList;

        GeneralUI.ActivateLeftBorder(false);
        GeneralUI.ActivateRightBorder(false);
        GeneralUI.ActivateCompleteBorder(false);

        NoAvailableMatches();

        PlayerControls = PlayerInputManager.Instance.Controls;
        CameraShortcut = PlayerControls.Gameplay.CameraShortcut;
        CameraShortcut.started += OnCameraShortcut;
        
        SkipCamShortcut = PlayerControls.Gameplay.SkipCamShortcut;
        SkipCamShortcut.started += OnSkipCamShortcut;

        PlayerTransformShortcut = PlayerControls.Gameplay.PlayerTransformShortcut;
        PlayerTransformShortcut.started += OnPlayerTransformShortcut;

        IsViewportOneCamera = false;
        CurrMatchState = MatchState.SEARCHING;
        CurrMatch = null;

        /*GeneralUI.Flicker(true);
        GeneralUI.ActivateLeftBorder(true);
        GeneralUI.ActivateRightBorder(true);*/
    }


    void Update()
    {

        //return;
        _closestMatchThisFrame = null;
        _closestMatchCloseness = 0f;
        MatchBase leftClosest;
        MatchBase rightClosest;
        MatchProximity leftProx = default;
        MatchProximity rightProx = default;
        rightProx.isPassing = false;

        if (DebugMode)
        {
            Debug.Log("Match State: " + CurrMatchState);
        }

        if ((CurrMatchState == MatchState.SEARCHING || CurrMatchState == MatchState.MATCHHOLD)
            && GameManager.Instance.CurrentCutsceneState ==
            GameManager.CutsceneState.GAMEPLAY) //if the match manager isn't completing another match, or all completed
        {
            if (CurrMatchState != MatchState.MATCHHOLD)
            {
                AutoSubmitTimer = 0.0f;
            }

            leftClosest = FindClosestMatchForPlayer(0);
            rightClosest = FindClosestMatchForPlayer(1);
            bool isValid = true;
            if (leftClosest != null && leftClosest.CheckMatchPrereqs())
            {
                leftProx = EvaluateProximity(leftClosest);
                TryPlayApproachSound(leftClosest, leftProx);

                // if closest left is aligned, activate left border
                GeneralUI.ActivateLeftBorder(leftClosest.isLeftOnScreen);
                //if (leftClosest.isLeftMatched)
                {
                    //calculate alpha of border
                    GeneralUI.LerpBorder(leftProx.LXcloseness, leftProx.LYcloseness, QuadBase.QuadType.LeftQuad);
                }
            }
            else
            {
                GeneralUI.ActivateLeftBorder(false);
                CurrMatchState = MatchState.SEARCHING;
                isValid = false;
            }

            if (rightClosest != null && rightClosest.CheckMatchPrereqs())
            {
                rightProx = EvaluateProximity(rightClosest);
                TryPlayApproachSound(rightClosest, rightProx);

                // if closest right is aligned, activate right border
                GeneralUI.ActivateRightBorder(rightClosest.isRightOnScreen);
                
                GeneralUI.LerpBorder(rightProx.RXcloseness, rightProx.RYcloseness, QuadBase.QuadType.RightQuad);
                
            }
            else
            {
                GeneralUI.ActivateRightBorder(false);
                CurrMatchState = MatchState.SEARCHING;
                isValid = false;
            }

            if (!isValid)
            {
                GeneralUI.Flicker(false);
                CurrMatchState = MatchState.SEARCHING;
                return;
            }
            //Debug.Log("LeftClosest: " + leftClosest.isLeftMatched + " RightClosest: " + rightClosest.isRightMatched);

            // if left and right are different matchbases and both aligned, flicker border
            // if left and right are the same and both aligned, don't flicker border
            if (leftClosest.isLeftOnScreen && rightClosest.isRightOnScreen)
            {
                if (leftClosest != rightClosest) //left and right are not the same, FLICKER ON
                {
                    GeneralUI.Flicker(true);
                    CurrMatchState = MatchState.SEARCHING;
                    GeneralUI.LerpBorderY(0, QuadBase.QuadType.LeftQuad);
                    GeneralUI.LerpBorderY(0, QuadBase.QuadType.RightQuad);
                }
                else //left and right are the same
                {
                    if (leftProx.isPassing || rightProx.isPassing) //match is valid, call matchComplete
                    {
                        GeneralUI.Flicker(false);

                        CurrMatch = leftClosest;
                        CurrMatchState = MatchState.MATCHHOLD;
                        GameplayMetricsCollector.RecordMatchSolvableStart(CurrMatch);
                        if (IsAutoSubmit)
                        {
                            AutoSubmitTimer += Time.deltaTime;
                            if (AutoSubmitTimer >= AutoSubmitDuration)
                            {
                                CurrMatchState = MatchState.MATCHCOMPLETE;
                                //MatchBase match = PuzzlesPrefabContext.Instance.FindMatch(this);
                                MatchManager.Instance.MatchComplete(CurrMatch);
                                //SendMatchCompleteEvent();
                                AutoSubmitTimer = 0.0f;

                                Debug.Log("match complete " + CurrMatch.gameObject.name);
                            }
                        }
                    }
                    else //match is not valid
                    {
                        //leftcloeset == rightcloeset but not passing
                        GeneralUI.Flicker(false);
                        CurrMatchState = MatchState.SEARCHING;
                    }
                }
            }
            else
            {
                //left or right (or both) are not on the screen
                CurrMatchState = MatchState.SEARCHING;
                GeneralUI.Flicker(false);
            }
        }
    }

    private void TryPlayApproachSound(MatchBase match, MatchProximity proximity)
    {
        if (match == null || match.isSolved || !proximity.yAligned)
            return;

        if (proximity.closeness < approachSoundThreshold)
        {
            if (_lastApproachMatch == match)
                _lastApproachMatch = null;
            return;
        }

        if (_lastApproachMatch == match)
            return;

        _lastApproachMatch = match;
        MusicManager.Instance?.PlaySFX(SoundID.PuzzlePieceApproached, Vector3.zero);
    }

    private void SetSilhouette()
    {
        bool hasCandidate = (_closestMatchThisFrame != null && !_closestMatchThisFrame.isSolved);
        var leftClosest = FindClosestMatchForPlayer(0);
        var rightClosest = FindClosestMatchForPlayer(1);
        if (leftClosest != null)
        {
            var lt = leftClosest.leftQuad != null ? leftClosest.leftQuad.transform : leftClosest.transform;
            leftSilhouetteHint.SetTarget(lt);
            leftSilhouetteHint.SetSilhouette(leftClosest.MatchTypeEnum);
        }
        if (rightClosest != null)
        {
            var rt = rightClosest.rightQuad != null ? rightClosest.rightQuad.transform : rightClosest.transform;
            rightSilhouetteHint.SetTarget(rt);
            rightSilhouetteHint.SetSilhouette(rightClosest.MatchTypeEnum);
        }

        if (hasCandidate && _closestMatchCloseness >= SilhouetteShowThreshold)
        {
            if (_activeSilhouetteMatchRef == null)
            {
                _activeSilhouetteMatchRef = _closestMatchThisFrame;
                _activeSilhouetteCloseness = _closestMatchCloseness;
                _activeSilhouetteGraceTimer = SilhouetteGraceTime;
            }
            else if (_closestMatchThisFrame != _activeSilhouetteMatchRef)
            {
                if (_closestMatchCloseness >= (_activeSilhouetteCloseness + SilhouetteSwitchBias))
                {
                    _activeSilhouetteMatchRef.HideSilhouettes();
                    _activeSilhouetteMatchRef = _closestMatchThisFrame;
                    _activeSilhouetteCloseness = _closestMatchCloseness;
                    _activeSilhouetteGraceTimer = SilhouetteGraceTime;
                }
            }
            else
            {
                _activeSilhouetteCloseness = _closestMatchCloseness;
                _activeSilhouetteGraceTimer = SilhouetteGraceTime;
            }
        }

        if (_activeSilhouetteMatchRef != null && _activeSilhouetteMatchRef.isSolved)
        {
            _activeSilhouetteMatchRef.HideSilhouettes();
            _activeSilhouetteMatchRef = null;
            _activeSilhouetteGraceTimer = 0f;
            _activeSilhouetteCloseness = 0f;
        }

        if (_activeSilhouetteMatchRef != null)
        {
            var proxActive = EvaluateProximity(_activeSilhouetteMatchRef);
            if (proxActive.closeness >= SilhouetteHideThreshold)
            {
                _activeSilhouetteGraceTimer = SilhouetteGraceTime;
            }
            else
            {
                _activeSilhouetteGraceTimer -= Time.deltaTime;
            }

            if (proxActive.isPassing || _activeSilhouetteGraceTimer <= 0f)
            {
                _activeSilhouetteMatchRef.HideSilhouettes();
                _activeSilhouetteMatchRef = null;
                _activeSilhouetteCloseness = 0f;
            }
            else
            {
                if (_activeSilhouetteMatchRef.isLeftMatched && _activeSilhouetteMatchRef.isRightMatched &&
                    proxActive.yAligned)
                {
                    _activeSilhouetteMatchRef.ShowBothSilhouettes();
                }
                else if (_activeSilhouetteMatchRef.isLeftMatched)
                {
                    _activeSilhouetteMatchRef.ShowRightSilhouette();
                }
                else if (_activeSilhouetteMatchRef.isRightMatched)
                {
                    _activeSilhouetteMatchRef.ShowLeftSilhouette();
                }
                else
                {
                    _activeSilhouetteMatchRef.ShowBothSilhouettes();
                }
            }
        }
        else
        {
            for (int i = 0; i < MatchBaseList.Count; i++)
            {
                if (MatchBaseList[i] != null) MatchBaseList[i].HideSilhouettes();
            }
        }
    }

    private MatchProximity EvaluateProximity(MatchBase matchInfo)
    {
        //Evaluate Proximity: For a match to be considered valid:
        // 1) x coordinates must align with middle border in screen space
        // 2) y coordinates of quads must be close to each other in screen space
        // 3) player must be in front of both quads
        // 4) there should be nothing in between the quad and the player
        // 5) players should be in the colliders of both quads
        // 6) any external conditions should be satisfied

        MatchProximity result = new MatchProximity
        {
            isPassing = false,
            closeness = 0f,
            yAligned = false
        };

        if (matchInfo.isSolved)
        {
            result.isPassing = true;
            result.closeness = 0f;
            return result;
        }

        float yThreshold = Screen.height * matchInfo.yThresholdPercent / 100;
        float leftXThreshold = Screen.width * matchInfo.leftXThresholdPercent / 100;
        float rightXThreshold = Screen.width * matchInfo.rightXThresholdPercent / 100;
        
        UnityEngine.Vector2 topLeftVertexScreenPoint = new UnityEngine.Vector2(0.0f, 0.0f);
        UnityEngine.Vector2 bottomLeftVertexScreenPoint = new UnityEngine.Vector2(0.0f, 0.0f);
        UnityEngine.Vector2 topRightVertexScreenPoint = new UnityEngine.Vector2(100000.0f, 0.0f);
        UnityEngine.Vector2 bottomRightVertexScreenPoint = new UnityEngine.Vector2(100000.0f, 0.0f);

        topLeftVertexScreenPoint = CameraPrefabContext.Instance.LeftCinemachineBrain.GetComponent<Camera>().WorldToScreenPoint(matchInfo.leftQuad.vertices[2]);
        bottomLeftVertexScreenPoint = CameraPrefabContext.Instance.LeftCinemachineBrain.GetComponent<Camera>().WorldToScreenPoint(matchInfo.leftQuad.vertices[0]);
        topRightVertexScreenPoint = CameraPrefabContext.Instance.RightCinemachineBrain.GetComponent<Camera>().WorldToScreenPoint(matchInfo.rightQuad.vertices[3]);
        bottomRightVertexScreenPoint = CameraPrefabContext.Instance.RightCinemachineBrain.GetComponent<Camera>().WorldToScreenPoint(matchInfo.rightQuad.vertices[1]);
        
        // 1) x coordinates must align with middle border in screen space
        float screenMiddleX = Screen.width / 2.0f;
        float topLeftDistFromMiddleX = screenMiddleX - topLeftVertexScreenPoint.x;
        float bottomLeftDistFromMiddleX = screenMiddleX - bottomLeftVertexScreenPoint.x;
        float topRightDistFromMiddleX = screenMiddleX - topRightVertexScreenPoint.x;
        float bottomRightDistFromMiddleX = screenMiddleX - bottomRightVertexScreenPoint.x;
    
        // 2) y coordinates of quads must be close to each other in screen space
        float screenMiddleY = Screen.height / 2.0f;
        float leftYMidpoint = (topLeftVertexScreenPoint.y + bottomLeftVertexScreenPoint.y)/2.0f;
        float rightYMidpoint =  (topRightVertexScreenPoint.y + bottomRightVertexScreenPoint.y)/2.0f;
        float leftDistFromMiddleY = Mathf.Abs(screenMiddleY - leftYMidpoint);
        float rightDistFromMiddleY = Mathf.Abs(screenMiddleY - rightYMidpoint);

        // 3) player must be in front of both quads
        UnityEngine.Vector3 forwardToLeftQuad =
            PlayerInputManager.Instance.Player1.transform.position - matchInfo.leftQuad.transform.position;
        forwardToLeftQuad.Normalize();
        float dotLeft = UnityEngine.Vector3.Dot(forwardToLeftQuad, matchInfo.leftQuad.transform.forward.normalized);
        float angleLeft = Mathf.Acos(Mathf.Clamp(dotLeft, -1f, 1f)) * Mathf.Rad2Deg;

        UnityEngine.Vector3 forwardToRightQuad =
            PlayerInputManager.Instance.Player2.transform.position - matchInfo.rightQuad.transform.position;
        forwardToRightQuad.Normalize();
        float dotRight = UnityEngine.Vector3.Dot(forwardToRightQuad, matchInfo.rightQuad.transform.forward.normalized);
        float angleRight = Mathf.Acos(Mathf.Clamp(dotRight, -1f, 1f)) * Mathf.Rad2Deg;

        //3a) small radius
        bool isLWithinSmallRadius = true;
        bool isRWithinSmallRadius = true;
        if (angleLeft > matchInfo.leftFacingAngleThreshold) //not passing angle threshold
        {
            isLWithinSmallRadius = (PlayerInputManager.Instance.Player1.transform.position - matchInfo.leftQuad.transform.position).magnitude
                                  < (matchInfo.smallRadiusThreshold * 10);
            isLWithinSmallRadius = isLWithinSmallRadius && (angleLeft < 180 );
        }
        if (angleRight > matchInfo.rightFacingAngleThreshold)
        {
            isRWithinSmallRadius = (PlayerInputManager.Instance.Player2.transform.position - matchInfo.rightQuad.transform.position).magnitude
                                  < (matchInfo.smallRadiusThreshold * 10);
            isRWithinSmallRadius = isRWithinSmallRadius && (angleLeft < 180 );
        }

        if (DebugMode)
        {
            Debug.Log(matchInfo.gameObject.name + " LTop: " + topLeftVertexScreenPoint + " LBot: " +
                      bottomLeftVertexScreenPoint + " RTop: " + topRightVertexScreenPoint + " RBot: " +
                      bottomRightVertexScreenPoint + " Langle: " + angleLeft + " Rangle: " + angleRight);
        }

        // 4) there should be nothing in between the quad and the player
        bool isObstructed = false;
        bool isLObstructed = false;
        bool isRobstructed = false;
        Vector3 LDir = PlayerInputManager.Instance.Player1.PlayerMoveCam.transform.position
                       - matchInfo.leftQuad.transform.position;
        Vector3 RDir = PlayerInputManager.Instance.Player2.PlayerMoveCam.transform.position
                       - matchInfo.rightQuad.transform.position;
        RaycastHit[] Lhits = Physics.RaycastAll(matchInfo.leftQuad.transform.position, LDir, 5.0f);
        RaycastHit[] Rhits = Physics.RaycastAll(matchInfo.rightQuad.transform.position, RDir, 5.0f);
        System.Array.Sort(Lhits, (a, b) => (a.distance.CompareTo(b.distance)));
        System.Array.Sort(Rhits, (a, b) => (a.distance.CompareTo(b.distance)));
        Debug.DrawRay(matchInfo.leftQuad.transform.position, LDir * 3.0f, Color.red);
        Debug.DrawRay(matchInfo.rightQuad.transform.position, (RDir) * 3.0f, Color.red);

        for (int i = 0; i < Lhits.Length; i++)
        {
            RaycastHit hit = Lhits[i];
            if (hit.collider.gameObject.tag != "PuzzleObject" && hit.collider.gameObject.tag != "Player"&& !hit.collider.isTrigger)
            {
                isLObstructed = true;
                //Debug.Log(matchInfo.MatchTypeEnum +  " Left raycast hit: " + hit.collider + " at index: " + i);
            }
            else if (hit.collider.gameObject.tag == "Player")
            {
                //Debug.Log(matchInfo.MatchTypeEnum +  " Left raycast hit player at index: " + i);
                break;
            } 
        }
        for (int i = 0; i < Rhits.Length; i++)
        {
            RaycastHit hit = Rhits[i];
            if (hit.collider.gameObject.tag != "PuzzleObject" && hit.collider.gameObject.tag != "Player" && !hit.collider.isTrigger)
            {
                isRobstructed = true;
                //Debug.Log(matchInfo.MatchTypeEnum +  " Right raycast hit: " + hit.collider + " at index: " + i);
            }
            else if (hit.collider.gameObject.tag == "Player")
            {
                //Debug.Log(matchInfo.MatchTypeEnum +  " Right raycast hit player at index: " + i);
                break;
            }
        }
        isObstructed = isLObstructed && isRobstructed;
        
        //check if players are facing puzzle
        float dotLCamToQuad = Vector3.Dot(-LDir, CameraPrefabContext.Instance.LeftCinemachineBrain.transform.forward);
        float angleLCamToQuad = Mathf.Acos(Mathf.Clamp(dotLCamToQuad, -1f, 1f)) * Mathf.Rad2Deg;
        float dotRCamToQuad = Vector3.Dot(-RDir, CameraPrefabContext.Instance.RightCinemachineBrain.transform.forward);
        float angleRCamToQuad = Mathf.Acos(Mathf.Clamp(dotRCamToQuad, -1f, 1f)) * Mathf.Rad2Deg;
        bool isLookingAtLQuad = angleLCamToQuad <= matchInfo.leftFacingAngleThreshold;
        bool isLookingAtRQuad = angleRCamToQuad <= matchInfo.rightFacingAngleThreshold;
        
        // 5) players should be in the colliders of both quads
        bool areQuadsNearby = matchInfo.leftQuad.IsNearby && matchInfo.rightQuad.IsNearby
            && NearbyMatches.Contains(matchInfo);

        // 6) any external conditions should be satisfied
        if (matchInfo.HasExternalCondition)
        {
            result.isPassing = matchInfo.ExternalCondition.isSolved;
        }
        
        // 7) player upvector should be same as quad
        float dotUpLeft = UnityEngine.Vector3.Dot(PlayerInputManager.Instance.Player1.transform.up, matchInfo.leftQuad.transform.up.normalized);
        //float angleUpLeft = Mathf.Acos(Mathf.Clamp(dotUpLeft, -1f, 1f)) * Mathf.Rad2Deg;
        
        float dotUpRight = UnityEngine.Vector3.Dot(PlayerInputManager.Instance.Player2.transform.up, matchInfo.rightQuad.transform.up.normalized);
        //float angleUpRight = Mathf.Acos(Mathf.Clamp(dotUpRight, -1f, 1f)) * Mathf.Rad2Deg;

        //put all conditions together
        matchInfo.isLeftMatched =
            (Mathf.Abs(topLeftDistFromMiddleX) <= leftXThreshold) &&
             (Mathf.Abs(bottomLeftDistFromMiddleX) <= leftXThreshold) &&
             ((angleLeft <= matchInfo.leftFacingAngleThreshold) || isLWithinSmallRadius) &&
             (matchInfo.leftQuad.IsNearby)&& isLookingAtLQuad &&
            (Mathf.Abs(leftDistFromMiddleY) <= yThreshold) &&
            (dotUpLeft >0.5f );

        matchInfo.isRightMatched =
            (Mathf.Abs(topRightDistFromMiddleX) <= rightXThreshold) &&
             (Mathf.Abs(bottomRightDistFromMiddleX) <= rightXThreshold) &&
             ((angleRight <= matchInfo.rightFacingAngleThreshold) || isRWithinSmallRadius) &&
             (matchInfo.rightQuad.IsNearby) && isLookingAtRQuad &&
            (Mathf.Abs(rightDistFromMiddleY) <= yThreshold)
            && (dotUpRight >0.5f );

        bool yAligned =
            (Mathf.Abs(leftDistFromMiddleY) <= yThreshold) &&
            (Mathf.Abs(rightDistFromMiddleY) <= yThreshold) ;
        result.yAligned = yAligned;

        bool isPassingThresholds =
            matchInfo.isLeftMatched && matchInfo.isRightMatched && //left and right angle, screen pos
            yAligned && areQuadsNearby && //y screen pos dist, is the player close by
            !isObstructed; //raycast

        if (DebugMode)
        {
            Debug.Log(matchInfo.MatchTypeEnum + " is passing?: " + isPassingThresholds + " IsLeftMatched: "
                      + matchInfo.isLeftMatched + " IsRightMatched: " + matchInfo.isRightMatched + " IsYAligned: "
                      + yAligned + " AreQuadsNearby: " + areQuadsNearby + " IsNotObstructed: " + !isObstructed + 
                      " dotUpLeft: "+ dotUpLeft + " dotUpRight: " + dotUpRight);
        }

        result.isPassing = isPassingThresholds;

        //calcuate closeness for ui
        float xError =
            (Mathf.Abs(topLeftDistFromMiddleX) +
             Mathf.Abs(bottomLeftDistFromMiddleX) +
             Mathf.Abs(topRightDistFromMiddleX) +
             Mathf.Abs(bottomRightDistFromMiddleX)) * 0.25f;

        float yError = (Mathf.Abs(leftDistFromMiddleY) +
                        Mathf.Abs(rightDistFromMiddleY)) * 0.5f;

        float angleError = (angleLeft + angleRight) * 0.5f;

        float maxXError = ((leftXThreshold + rightXThreshold) / 2) * 3f;
        float maxYError = yThreshold * 3f;
        float maxAngleError = ((matchInfo.leftFacingAngleThreshold + matchInfo.rightFacingAngleThreshold) / 2) * 3f;

        float xFactor = 1f - Mathf.Clamp01(xError / maxXError);
        float yFactor = 1f - Mathf.Clamp01(yError / maxYError);
        float angleFactor = 1f - Mathf.Clamp01(angleError / maxAngleError);

        float closeness = (xFactor + yFactor + angleFactor) / 3f;
        result.closeness = Mathf.Clamp01(closeness);

        //calculate if bars should show up
        //not obstructed, within angle, is within range, on screen
        // 1 = closer, 0 = farther
        //if statement is a check for if the bars show up
        if (!isLObstructed && (angleLeft < 180) && angleLCamToQuad < 180 && matchInfo.leftQuad.IsNearby &&
            (Mathf.Abs(topLeftDistFromMiddleX) <= (Screen.width/4.0f)) &&
            (Mathf.Abs(bottomLeftDistFromMiddleX) <= (Screen.width/4.0f))&& (dotUpLeft >0.5f ))
        {
            //if the avg of left points are within the threshold, then closeness should be 1
            // if the avg is out of threshold && within half of the screen on the left side, calculate percentage difference
            //deal with x values on the left side
            float LXPreThereshold = (Screen.width/4.0f) - leftXThreshold;
            float LAnglePreThereshold = 180 - matchInfo.leftFacingAngleThreshold;
            float LXAvg = Mathf.Abs((Mathf.Abs(topLeftDistFromMiddleX) +
                                      Mathf.Abs(bottomLeftDistFromMiddleX)) / 2.0f);
            float LXcloseness = -1;
            if (LXAvg <= leftXThreshold)
            {
                //is within threshold, should be 1
                LXcloseness = 1.0f;
            }
            else if (LXAvg > leftXThreshold)
            {
                //more than threshold, should lerp here
                float LXDiv = Mathf.Clamp( (LXAvg - leftXThreshold) / LXPreThereshold, 0.0f ,1.0f);
                LXcloseness = 1.0f - LXDiv;
            }
            float LAngleCloseness =  1.0f - (Mathf.Abs(angleLeft - matchInfo.leftFacingAngleThreshold)/LAnglePreThereshold);
            result.LXcloseness = (LXcloseness * LAngleCloseness);
            
            //deal with y values on the left side
            float LYPreThereshold = (Screen.height/4.0f) - yThreshold;
            float LYcloseness = -1;
            if (leftDistFromMiddleY <= yThreshold)
            {
                //is within threshold, should be 1
                LYcloseness = 1.0f;
            }
            else if (leftDistFromMiddleY > yThreshold)
            {
                //more than threshold, should lerp here
                float LYDiv = Mathf.Clamp( (leftDistFromMiddleY - yThreshold) / LYPreThereshold, 0.0f ,1.0f);
                LYcloseness = 1.0f - LYDiv;
            }
            result.LYcloseness = LYcloseness;
            
            //borders should show
            matchInfo.isLeftOnScreen = true;
        }
        else
        {
            result.LXcloseness = -1;
            matchInfo.isLeftOnScreen = false;
        }
        
        if (!isRobstructed && (angleRight < 180) &&  angleRCamToQuad < 180 && matchInfo.rightQuad.IsNearby &&
            (Mathf.Abs(topRightDistFromMiddleX) <= (Screen.width/4.0f)) &&
            (Mathf.Abs(bottomRightDistFromMiddleX) <= (Screen.width/4.0f))&& (dotUpRight >0.5f ))
        {
            //deal with x values on the right side
            float RXPreThereshold = (Screen.width/4.0f) - rightXThreshold;
            float RAnglePreThereshold = 180 - matchInfo.rightFacingAngleThreshold;
            float RXAvg = Mathf.Abs((Mathf.Abs(topRightDistFromMiddleX) +
                                     Mathf.Abs(bottomRightDistFromMiddleX)) / 2.0f);
            float RXcloseness = -1;
            if (RXAvg <= rightXThreshold)
            {
                //is within threshold, should be 1
                RXcloseness = 1.0f;
            }
            else if (RXAvg > rightXThreshold)
            {
                //more than threshold, should lerp here
                float RXDiv = Mathf.Clamp( (RXAvg - rightXThreshold) / RXPreThereshold, 0.0f ,1.0f);
                RXcloseness = 1.0f - RXDiv;
            }
            float RAngleCloseness = 1.0f - (Mathf.Abs(angleRight - matchInfo.rightFacingAngleThreshold)/ RAnglePreThereshold);
            result.RXcloseness = (RXcloseness * RAngleCloseness);
            
            //deal with y values on the right side
            float RYPreThereshold = (Screen.height/4.0f) - yThreshold;
            float RYcloseness = -1;
            if (rightDistFromMiddleY <= yThreshold)
            {
                //is within threshold, should be 1
                RYcloseness = 1.0f;
            }
            else if (rightDistFromMiddleY > yThreshold)
            {
                //more than threshold, should lerp here
                float RYDiv = Mathf.Clamp( (rightDistFromMiddleY - yThreshold) / RYPreThereshold, 0.0f ,1.0f);
                RYcloseness = 1.0f - RYDiv;
            }
            result.RYcloseness = RYcloseness;
            
            //borders should show
            matchInfo.isRightOnScreen = true;
        }
        else
        {
            result.RXcloseness = -1;
            matchInfo.isRightOnScreen = false;
        }
        
        return result;
    }

    private void OnCameraShortcut(InputAction.CallbackContext context)
    {
#if UNITY_EDITOR
        if (CameraTestMatch != null)
        {
            MatchBase camMatch =
                PuzzlesPrefabContext.Instance.MatchBaseList.Find(match => match.MatchTypeEnum == CameraTestMatch);
            camMatch.gameObject.SetActive(true);
            StartCoroutine(AutoCompleteCamEffect(camMatch));
        }
#endif
    }
    
    private void OnSkipCamShortcut(InputAction.CallbackContext context)
    {
#if UNITY_EDITOR
        if (CurrMatch != null && CurrMatchState== MatchState.MATCHEFFECT)
        {
           StartCoroutine( StopCompleteEffect(CurrMatch));
        }
#endif
    }

    private void OnPlayerTransformShortcut(InputAction.CallbackContext context)
    {
#if UNITY_EDITOR
        if (TestLPlayerTransform)
        {
            PlayerInputManager.Instance.Player1.transform.position = TestLPlayerTransform.position;
            Vector3 dir = -TestLPlayerTransform.up.normalized;
            StartCoroutine(TransformRoom.ChangePlayerGrav(dir, true));
        }
        if (TestRPlayerTransform)
        {
            PlayerInputManager.Instance.Player2.transform.position = TestRPlayerTransform.position;
            Vector3 dir = -TestRPlayerTransform.up.normalized;
            StartCoroutine(TransformRoom.ChangePlayerGrav(dir, false));
        }
#endif
    }

    public void ValidMatchFound(MatchBase matchInfo)
    {
        CurrMatch = matchInfo;
    }

    public void InvalidMatch()
    {

    }

    public void SendMatchCompleteEvent()
    {
        BehaviorGraphSO.TriggerPuzzleCompletedGraphEvent.SendEventMessage(CurrMatch);
        Debug.Log("SendMatchCompleteEvent on " + CurrMatch.name);
        return;

    }

    private void ScheduleMatchTypeStingerAfterSuccess(MatchBase matchInfo)
    {
        // SFX stingers were replaced by tags in cutscenes -Ari

        /* 
        if (MusicManager.Instance == null)
            return;

        MatchBase captured = matchInfo;
        MusicManager.Instance.InvokeAfterRealtimeDelay(matchStingerDelayAfterSuccess, () =>
        {
            MatchManager mm = MatchManager.Instance;
            if (captured == null || !captured || mm == null)
                return;

            mm.InvokeMatchTypeStinger(captured);
        });
        */
    }

    private void TryPlayMatchTypeStinger(MatchBase matchInfo)
    {
        // SFX stingers were replaced by tags in cutscenes -Ari

        /*
        if (matchInfo == null || MusicManager.Instance == null)
            return;

        SoundID stinger;
        switch (matchInfo.MatchTypeEnum)
        {
            case MatchBase.MatchType.LAMP:
                stinger = SoundID.LightTurningOn;
                break;
            case MatchBase.MatchType.KEY:
                stinger = SoundID.KeyOpeningDoor;
                break;
            case MatchBase.MatchType.JIGSAW:
                stinger = SoundID.DoorUnlocked;
                break;
            case MatchBase.MatchType.EXTINGUISHER:
                stinger = SoundID.FireExtinguisherSpray;
                break;
            case MatchBase.MatchType.PLUNGER:
                stinger = SoundID.UnclogSuctionPop;
                break;
            case MatchBase.MatchType.EXITSIGN:
                stinger = SoundID.ExitSignMatched;
                break;
            case MatchBase.MatchType.DRAIN:
                stinger = SoundID.WaterDraining;
                break;
            default:
                return;
        }

        MusicManager.Instance.PlaySFX(stinger, Vector3.zero);
        */
    }

    /// <summary> Called after the post–MatchSuccess delay (runs on MusicManager so cutscene disables don't cancel the wait). </summary>
    public void InvokeMatchTypeStinger(MatchBase matchInfo) => TryPlayMatchTypeStinger(matchInfo);

    public void MatchComplete(MatchBase matchInfo)
    {
        GameplayMetricsCollector.RecordMatchComplete(matchInfo);
        Debug.Log(matchInfo.MatchTypeEnum);
        if (!matchInfo.isOptionalPuzzle)
            objectiveIndex++;
        // MatchSuccess first; puzzle-type stinger runs after matchStingerDelayAfterSuccess (unscaled seconds).
        MusicManager.Instance?.PlaySFX(SoundID.MatchSuccess, Vector3.zero);
        ScheduleMatchTypeStingerAfterSuccess(matchInfo);
        //GeneralUI.TestText2.text = "Match Complete on:" + matchInfo.MatchTypeEnum.ToString();
        Debug.Log("Match Complete Function");

        ObjectiveSystem.Instance.SetObjective(objectiveIndex);
        // Hide silhouettes when a match completes
        if (matchInfo != null) matchInfo.HideSilhouettes();
        if (_activeSilhouetteMatchRef == matchInfo)
        {
            _activeSilhouetteMatchRef = null;
            _activeSilhouetteGraceTimer = 0f;
            _activeSilhouetteCloseness = 0f;
        }

        matchInfo.isSolved = true;
        if (_lastApproachMatch == matchInfo)
            _lastApproachMatch = null;
        matchCompleteCoroutine = StartCoroutine(WaitMatchCompleteEffect(matchInfo)); //sequence of visual changes after match is complete

    }

    public IEnumerator WaitMatchCompleteEffect(MatchBase matchInfo)
    {
        //reset quad vertecies
        matchInfo.leftQuad.vertices.Clear();
        matchInfo.rightQuad.vertices.Clear();
        // Store in the ScriptableObject
        for (int i = 0; i < 4; i++)
        {
            matchInfo.leftQuad.vertices.Add(matchInfo.leftQuad.meshFilter.transform.TransformPoint(matchInfo.leftQuad.meshFilter.mesh.vertices[i]));
            matchInfo.rightQuad.vertices.Add(matchInfo.rightQuad.meshFilter.transform.TransformPoint(matchInfo.rightQuad.meshFilter.mesh.vertices[i]));
        }     
        
        //sequence of events that happen after a match is complete
        //INITIAL EFFECTS, PUZZLE CAM ACTIVATED
        CurrMatchState = MatchState.MATCHEFFECT;
        BeginMatchCutsceneFocusGuard();

        // Before puzzle cams move: hide clue popups and ObjectsToHide. (CutsceneUIEffect
        // used to do this only after the blend, which flashed UI during camera travel.)
        if (GeneralUI != null)
        {
            GeneralUI.HandleCutsceneUI(true);
            GeneralUI.HideAllPickupPopupsImmediate();
        }
        GameManager.Instance.SetCutsceneState(GameManager.CutsceneState.CUTSCENE);

        PlayerInputManager.Instance.SaveInputData();
        PlayerInputManager.Instance.SetAllPlayersInputPaused(true);
        CameraPrefabContext.Instance.SetPuzzleCamera(matchInfo);
        yield return new WaitForEndOfFrame();

        GeneralUI.SetFullBorders(true); 
        if(matchInfo.DoorToOpen ) matchInfo.DoorToOpen.SilhouetteOff();
        MusicManager.Instance?.PlaySFX(SoundID.MatchSuccess, Vector3.zero);
        CameraPrefabContext.Instance.SetCameraState(matchInfo, CameraPrefabContext.CameraState.PUZZLE, false);

        MusicManager.Instance?.IncrementMusic(); //trigger bgm update

        //WAIT FOR PUZZLE CAM
        while (CameraPrefabContext.Instance.LeftCinemachineBrain.IsBlending || CameraPrefabContext.Instance.RightCinemachineBrain.IsBlending) //wait until the camera is done switching
        {
            yield return new WaitForEndOfFrame();
        }
        //puzzlecam bump
        yield return new WaitForSeconds(3.0f);
        MusicManager.Instance?.PlaySFX(SoundID.UIMatchSnap, Vector3.zero);
        yield return StartCoroutine(AnimateZoomCurve(matchInfo, camBumpStrength, camBumpDuration));
        while (CameraPrefabContext.Instance.LeftCinemachineBrain.IsBlending || CameraPrefabContext.Instance.RightCinemachineBrain.IsBlending) //wait until the camera is done switching
        {
            yield return new WaitForEndOfFrame();
        }
        yield return new WaitForSeconds(PuzzleCamDuration);

        //SET ITEMS ACTIVE      
        //-----show model animation here
        GeneralUI.CutsceneUIEffect(true); //black bars come in
        GeneralUI.SetFullBorders(false); 
        if (matchInfo.completeItem)
        {
            matchInfo.completeItem.SetActive(true);

            if (matchInfo.NewObjectAnim)
            {
                matchInfo.NewObjectAnim.SetBool("isActive", true);
            }
        }
        foreach (GameObject obj in matchInfo.preCamShowObjects)
        {
            if (obj)
            {
                obj.SetActive(true);
            }
        }
        EnvironmentPrefabContext.Instance.ShowNextRoomObjects(matchInfo.TransformRoomEnum);

        //HIDE OBJECTS
        foreach (GameObject obj in matchInfo.preCamHideObjects)
        {
            if (obj)
            {
                obj.SetActive(false);
            }
        }
        EnvironmentPrefabContext.Instance.HidePrevRoomObjects(matchInfo.TransformRoomEnum);
        EnvironmentPrefabContext.Instance.ChangePlayerTransf(matchInfo.TransformRoomEnum);
        //DoorBase doorToOpen = PuzzlesPrefabContext.Instance.FindDoor(matchInfo.MatchTypeEnum); 
        
        //yield return new WaitForEndOfFrame(); 

        //dialogue to play during timeline
        if (matchInfo.completeMatchDialogue != "")
        {
            DialogueManager.Instance.StartDialogue(matchInfo.completeMatchDialogue);
            Debug.Log("Playing Dialogue: " + matchInfo.completeMatchDialogue);
        }

        //COMPLETE EFFECT CAM ACTIVATED
        if (matchInfo.completeItem)
        {
            CameraPrefabContext.Instance.SetCompletionCamera(matchInfo);
            yield return new WaitForEndOfFrame();
            CameraPrefabContext.Instance.SetAllPlayerBrainsActive(false);
            CameraPrefabContext.Instance.SetCameraState(matchInfo, CameraPrefabContext.CameraState.COMPLETEEFFECT, true); //show change in environment
            yield return new WaitForEndOfFrame();
            yield return StartCoroutine(CameraPrefabContext.Instance.CompletionCameraZoom(matchInfo, matchInfo.completeItemZoomDist));

            if (matchInfo.PlayableTimeline )
            {
                //play timeline
                yield return new WaitForEndOfFrame();
                CameraPrefabContext.Instance.PlayTimeline(matchInfo.PlayableTimeline);
                yield return WaitForMatchTimeline();
            }
        }
        else if (matchInfo.PlayableTimeline)
        {
            //CameraPrefabContext.Instance.SetAllPlayerBrainsActive(false);
            
            CameraPrefabContext.Instance.SetCameraState(matchInfo, CameraPrefabContext.CameraState.COMPLETEEFFECT, true); //show change in environment

            CameraPrefabContext.Instance.PlayTimeline(matchInfo.PlayableTimeline);

            //CameraPrefabContext.Instance.SetCameraState(matchInfo, CameraPrefabContext.CameraState.COMPLETEEFFECT, true); //show change in environment
            //yield return new WaitForEndOfFrame();
            //CameraPrefabContext.Instance.PlayTimeline(matchInfo.PlayableTimeline);
            yield return WaitForMatchTimeline();
        }

        //Room Transform
        foreach (GameEvents.GameActionEnum actionEnum in matchInfo.TriggeredHouseEvents)
        {
            GameEvents.GameTrigger(actionEnum);
        }
        EnvironmentPrefabContext.Instance.TriggerRoomEvents(matchInfo.TransformRoomEnum);
        if(matchInfo.DoorToOpen) matchInfo.DoorToOpen.OpenDoor();

        //SET EVERYTHING BACK TO NORMAL, MOVE CAM IS BACK
        foreach (GameObject obj in matchInfo.postCamHideObj)
        {
            if (obj)
            {
                obj.SetActive(false);
            }
        }
        foreach (GameObject obj in matchInfo.postCamShowObjects)
        {
            if (obj)
            {
                obj.SetActive(true);
            }
        }
        CameraPrefabContext.Instance.SetCameraState(matchInfo, CameraPrefabContext.CameraState.MOVE, true);
        CameraPrefabContext.Instance.SetAllPlayerBrainsActive(true);
        GeneralUI.CutsceneUIEffect(false); //ui
        yield return new WaitForEndOfFrame();
        
        PlayerInputManager.Instance.RestorePlayers();
        NearbyMatches.Remove(matchInfo);
        CurrMatchState = MatchState.SEARCHING;
        GameManager.Instance.SetCutsceneState(GameManager.CutsceneState.GAMEPLAY);
        EndMatchCutsceneFocusGuard();

    }

    public IEnumerator AutoCompleteMatch(MatchBase matchInfo, bool movePlayer)
    {
        if (matchInfo.isSolved) yield return null;

        matchInfo.isSolved = true;
        //SET ITEMS ACTIVE      
        //-----show model animation here
        if (matchInfo.completeItem)
        {
            matchInfo.completeItem.SetActive(true);

            if (matchInfo.NewObjectAnim)
            {
                matchInfo.NewObjectAnim.SetBool("isActive", true);
            }
        }
        foreach (GameObject obj in matchInfo.preCamShowObjects)
        {
            if (obj)
            {
                obj.SetActive(true);
            }
        }
        EnvironmentPrefabContext.Instance.ShowNextRoomObjects(matchInfo.TransformRoomEnum);

        //HIDE OBJECTS
        foreach (GameObject obj in matchInfo.preCamHideObjects)
        {
            if (obj)
            {
                obj.SetActive(false);
            }
        }
        foreach (GameEvents.GameActionEnum actionEnum in matchInfo.TriggeredHouseEvents)
        {
            GameEvents.GameTrigger(actionEnum);
        }
        EnvironmentPrefabContext.Instance.TriggerRoomEvents(matchInfo.TransformRoomEnum);
        EnvironmentPrefabContext.Instance.HidePrevRoomObjects(matchInfo.TransformRoomEnum);
        EnvironmentPrefabContext.Instance.ChangePlayerTransf(matchInfo.TransformRoomEnum);
        DoorBase doorToOpen = PuzzlesPrefabContext.Instance.FindDoor(matchInfo.MatchTypeEnum);
        if (doorToOpen)
        {
            doorToOpen.SilhouetteOff();
            doorToOpen.IsReadyToOpen = true;
            doorToOpen.OpenDoor();
        }
        yield return new WaitForEndOfFrame();
        
        foreach (GameObject obj in matchInfo.postCamHideObj)
        {
            if (obj)
            {
                obj.SetActive(false);
            }
        }
        foreach (GameObject obj in matchInfo.postCamShowObjects)
        {
            if (obj)
            {
                obj.SetActive(true);
            }
        }
        NearbyMatches.Remove(matchInfo);
        yield return new WaitForEndOfFrame();


        if (movePlayer)
        {
            //index 0 = studio, 1 = living room, 2= childrens bedroom, 3 = bathroom
            Transform leftTransform = EnvironmentPrefabContext.Instance.PlayerPositionList[0];
            Transform rightTransform= EnvironmentPrefabContext.Instance.PlayerPositionList[0];
            if (matchInfo.MatchTypeEnum == MatchBase.MatchType.BROCHURE ||
                matchInfo.MatchTypeEnum == MatchBase.MatchType.KEY)
            {
                leftTransform = EnvironmentPrefabContext.Instance.PlayerPositionList[0];
                rightTransform = EnvironmentPrefabContext.Instance.PlayerPositionList[0];
                rightTransform.position = new Vector3(rightTransform.position.x, rightTransform.position.y + 5.0f, rightTransform.position.z);
            }
            else if (matchInfo.MatchTypeEnum == MatchBase.MatchType.EXTINGUISHER ||
                     matchInfo.MatchTypeEnum == MatchBase.MatchType.PLUNGER)
            {
                leftTransform = EnvironmentPrefabContext.Instance.PlayerPositionList[1];
                rightTransform = EnvironmentPrefabContext.Instance.PlayerPositionList[1];
                rightTransform.position = new Vector3(rightTransform.position.x + 5.0f, rightTransform.position.y, rightTransform.position.z);
            }
            else if (matchInfo.MatchTypeEnum == MatchBase.MatchType.JIGSAW)
            {
                leftTransform = EnvironmentPrefabContext.Instance.PlayerPositionList[2];
                rightTransform = EnvironmentPrefabContext.Instance.PlayerPositionList[2];
                rightTransform.position = new Vector3(rightTransform.position.x + 5.0f , rightTransform.position.y , rightTransform.position.z);
            }
            else if (matchInfo.MatchTypeEnum == MatchBase.MatchType.DRAIN)
            {
                leftTransform = EnvironmentPrefabContext.Instance.PlayerPositionList[3];
                rightTransform = EnvironmentPrefabContext.Instance.PlayerPositionList[3];
                rightTransform.position = new Vector3(rightTransform.position.x + 5.0f, rightTransform.position.y, rightTransform.position.z);
            }
            
            PlayerInputManager.Instance.Player1.transform.position = leftTransform.position;
            PlayerInputManager.Instance.Player1.transform.rotation = leftTransform.rotation;
            Vector3 Ldir = -leftTransform.up;
            StartCoroutine(TransformRoom.ChangePlayerGrav(Ldir.normalized, true));
        
            PlayerInputManager.Instance.Player2.transform.position = rightTransform.position;
            PlayerInputManager.Instance.Player2.transform.rotation = rightTransform.rotation;
            Vector3 Rdir = -rightTransform.up;
            StartCoroutine(TransformRoom.ChangePlayerGrav(Rdir.normalized, false));
            
        }

        NearbyMatches.Remove(matchInfo);


    }

    IEnumerator AutoCompleteCamEffect(MatchBase matchInfo)
    {
        StartCoroutine(WaitMatchCompleteEffect(matchInfo));
        NearbyMatches.Add(matchInfo);
        yield return new WaitForEndOfFrame();
    }

    public IEnumerator StopCompleteEffect(MatchBase matchInfo)
    {
        if (matchCompleteCoroutine != null)
        {
            StopCoroutine(matchCompleteCoroutine);
            matchCompleteCoroutine = null;
        }
        CameraPrefabContext.Instance.Director.Stop();
        EndMatchCutsceneFocusGuard();
        
        if (matchInfo.completeItem)
        {
            matchInfo.completeItem.SetActive(true);

            if (matchInfo.NewObjectAnim)
            {
                matchInfo.NewObjectAnim.SetBool("isActive", true);
            }
        }
        foreach (GameObject obj in matchInfo.preCamShowObjects)
        {
            if (obj)
            {
                obj.SetActive(true);
            }
        }
        EnvironmentPrefabContext.Instance.ShowNextRoomObjects(matchInfo.TransformRoomEnum);

        //HIDE OBJECTS
        foreach (GameObject obj in matchInfo.preCamHideObjects)
        {
            if (obj)
            {
                obj.SetActive(false);
            }
        }
        CameraPrefabContext.Instance.SetAllPlayerBrainsActive(true);
        yield return new WaitForEndOfFrame();
        
        //Room Transform
        foreach (GameEvents.GameActionEnum actionEnum in matchInfo.TriggeredHouseEvents)
        {
            GameEvents.GameTrigger(actionEnum);
        }
        EnvironmentPrefabContext.Instance.TriggerRoomEvents(matchInfo.TransformRoomEnum);
        EnvironmentPrefabContext.Instance.HidePrevRoomObjects(matchInfo.TransformRoomEnum);
        EnvironmentPrefabContext.Instance.ChangePlayerTransf(matchInfo.TransformRoomEnum);
        EnvironmentPrefabContext.Instance.TriggerRoomEvents(matchInfo.TransformRoomEnum);
        DoorBase doorToOpen = PuzzlesPrefabContext.Instance.FindDoor(matchInfo.MatchTypeEnum);
        if(doorToOpen ) doorToOpen.SilhouetteOff();
        
        //SET EVERYTHING BACK TO NORMAL, MOVE CAM IS BACK
        foreach (GameObject obj in matchInfo.postCamHideObj)
        {
            if (obj)
            {
                obj.SetActive(false);
            }
        }
        foreach (GameObject obj in matchInfo.postCamShowObjects)
        {
            if (obj)
            {
                obj.SetActive(true);
            }
        }
        
        GeneralUI.CutsceneUIEffect(false); //ui
        PlayerInputManager.Instance.RestorePlayers();
        CameraPrefabContext.Instance.SetCameraState(matchInfo, CameraPrefabContext.CameraState.MOVE, true);

        NearbyMatches.Remove(matchInfo);
        CurrMatchState = MatchState.SEARCHING;
        GameManager.Instance.SetCutsceneState(GameManager.CutsceneState.GAMEPLAY);


    }



    private void BeginMatchCutsceneFocusGuard()
    {
        if (hasMatchCutsceneRunInBackgroundOverride)
            return;

        previousRunInBackground = Application.runInBackground;
        Application.runInBackground = true;
        hasMatchCutsceneRunInBackgroundOverride = true;
    }

    private void EndMatchCutsceneFocusGuard()
    {
        if (!hasMatchCutsceneRunInBackgroundOverride)
            return;

        Application.runInBackground = previousRunInBackground;
        hasMatchCutsceneRunInBackgroundOverride = false;
    }

    private IEnumerator WaitForMatchTimeline()
    {
        PlayableDirector director = CameraPrefabContext.Instance.Director;
        if (director == null)
            yield break;

        double duration = director.duration;
        float timeout = duration > 0.0 && !double.IsInfinity(duration)
            ? (float)duration
            : 30.0f;

        float elapsed = 0.0f;
        while (director.state == PlayState.Playing && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return new WaitForEndOfFrame();
        }

        if (director.state == PlayState.Playing)
        {
            Debug.LogWarning("Match timeline did not finish before timeout. Stopping it so player cameras can be restored.");
            director.Stop();
        }
    }

    private IEnumerator WaitForPuzzleCameraBlend()
    {
        const float blendTimeout = 5.0f;
        float elapsed = 0.0f;
        while (CameraPrefabContext.Instance.LeftCinemachineBrain.IsBlending ||
               CameraPrefabContext.Instance.RightCinemachineBrain.IsBlending)
        {
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= blendTimeout)
            {
                Debug.LogWarning("Puzzle camera blend exceeded timeout. Continuing cutscene to avoid soft-lock.");
                yield break;
            }

            yield return new WaitForEndOfFrame();
        }
    }

    public IEnumerator WaitAllMatchComplete(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        GeneralUI.ActivateLeftBorder(false);
        GeneralUI.ActivateRightBorder(false);
        GeneralUI.ActivateCompleteBorder(true);

        GeneralUI.SetMatchText("All Matches Complete!");
        AudioManager.Instance.PlayLevelCompleteAudio();
    }

    public void NoAvailableMatches()
    {
        GeneralUI.SetMatchText("");
    }

    public void AddNearbyMatch(MatchBase match)
    {
        if (!match.isSolved)
        {
            NearbyMatches.Add(match);
        }
    }

    public void RemoveNearbyMatch(MatchBase match)
    {
        NearbyMatches.Remove(match);
    }
    public bool IsNearbyMatch(MatchBase match)
    {
        return NearbyMatches.Contains(match);
    }

    private MatchBase FindClosestMatchForPlayer(int playerIndex)
    {
        Transform playerT = null;
        if (PlayerInputManager.Instance != null)
        {
            if (playerIndex == 0) playerT = PlayerInputManager.Instance.Player1 != null ? PlayerInputManager.Instance.Player1.transform : null;
            else playerT = PlayerInputManager.Instance.Player2 != null ? PlayerInputManager.Instance.Player2.transform : null;
        }
        if (playerT == null) return null;

        float best = float.PositiveInfinity;
        MatchBase bestMatch = null;
        for (int i = 0; i < NearbyMatches.Count; i++)
        {
            var m = NearbyMatches[i];
            if (m == null || m.isSolved) continue;
            Transform refT = (playerIndex == 0 ? (m.leftQuad != null ? m.leftQuad.transform : null)
                                               : (m.rightQuad != null ? m.rightQuad.transform : null));
            if (refT == null) refT = m.transform;
            float d = Vector3.Distance(playerT.position, refT.position);
            if (d < best)
            {
                best = d;
                bestMatch = m;
            }
        }
        return bestMatch;
    }

    public int GetNumCompletedMatches()
    {
        int numCompleted = 0;
        foreach (MatchBase match in MatchBaseList)
        {
            if (match.isSolved) numCompleted++;
        }
        return numCompleted;
    }
    
    IEnumerator AnimateZoomCurve(MatchBase matchInfo, float strength, float duration)
    {
        float baseValue = matchInfo.heightPercent;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;

            float curveValue = camBumpCurve.Evaluate(t);

            // apply curve offset
            matchInfo.heightPercent = baseValue + (curveValue) * strength;

            CameraPrefabContext.Instance.SetPuzzleCamera(matchInfo);

            yield return null;
        }

        matchInfo.heightPercent = baseValue;
        CameraPrefabContext.Instance.SetPuzzleCamera(matchInfo);
    }
}

