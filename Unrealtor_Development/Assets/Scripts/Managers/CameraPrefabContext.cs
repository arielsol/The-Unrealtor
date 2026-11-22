using System;
using System.Collections;
using Cinemachine;
using UnityCommunity.UnitySingleton;
using UnityEngine;
using UnityEngine.Playables;

public class CameraPrefabContext : MonoSingleton<CameraPrefabContext>
{
    //MOSTLY FOR PUZZLES
    public enum CameraPriority
    {
        HIGH = 30,
        MEDIUM = 20,
        LOW = 10
    }

    public enum CameraState
    {
        MOVE,
        PUZZLE,
        COMPLETEEFFECT
    }

    [Header("Camera Brains")]
    [SerializeField] public CinemachineBrain LeftCinemachineBrain;
    [SerializeField] public CinemachineBrain RightCinemachineBrain;
    [SerializeField] public CinemachineBrain CutsceneCinemachineBrain;

    [Header("Move Cameras")]
    [SerializeField]  public CinemachineVirtualCamera LeftMoveCamera;
    [SerializeField] public CinemachineVirtualCamera RightMoveCamera;

    [Header("Puzzle Cameras")]
    [SerializeField] public CinemachineVirtualCamera LeftPuzzleCamera;
    [SerializeField] public CinemachineVirtualCamera RightPuzzleCamera;
    
    [Header("Effects Cameras")]
    [SerializeField] public CinemachineVirtualCamera CutsceneCamera;

    [SerializeField] public CinemachineVirtualCamera CompletedEffectCamera;
    [SerializeField] public PlayableDirector Director; 

    public CameraState CurrCameraState { get; private set; }
    public MatchUI GeneralUI { get; private set; }
    
    private void Start()
    {
        GeneralUI = GameObject.Find("GeneralUI").GetComponent<MatchUI>();
        
        LeftPuzzleCamera.Priority = (int)CameraPriority.LOW;
        RightPuzzleCamera.Priority = (int)CameraPriority.LOW;
        
        SetAllPlayerBrainsActive(true);
    }

    public void SetPuzzleCamera(MatchBase matchInfo)
    {
        float fov = 60.0f * Mathf.Deg2Rad;
        
        //rotation
        LeftPuzzleCamera.transform.rotation= Quaternion.LookRotation(-matchInfo.leftQuad.transform.forward, matchInfo.leftQuad.transform.up);
        RightPuzzleCamera.transform.rotation= Quaternion.LookRotation(-matchInfo.rightQuad.transform.forward, matchInfo.rightQuad.transform.up);

        Vector3 leftCamPos = (matchInfo.leftQuad.vertices[0] + matchInfo.leftQuad.vertices[2])/2.0f ;
        Vector3 rightCamPos =  (matchInfo.rightQuad.vertices[1] + matchInfo.rightQuad.vertices[3])/2.0f;
        
        //local z
        float leftQuadHeight = Vector3.Distance(matchInfo.leftQuad.vertices[0], matchInfo.leftQuad.vertices[2]) ;
        float rightQuadHeight = Vector3.Distance(matchInfo.rightQuad.vertices[0], matchInfo.rightQuad.vertices[2]);

        float leftZ = leftQuadHeight/(2 * matchInfo.heightPercent * Mathf.Tan(fov * 0.5f ));
        float rightZ = rightQuadHeight/(2 * matchInfo.heightPercent * Mathf.Tan(fov * 0.5f ));
        
        leftCamPos += leftZ * matchInfo.leftQuad.transform.forward;
        rightCamPos +=rightZ * matchInfo.rightQuad.transform.forward;
        
        //Debug.Log("z: "+ leftZ + " " + rightZ);
        
        //local x
        float aspect = Screen.width / (float)Screen.height;
        float leftHeight = 2f * leftZ * Mathf.Tan(fov * 0.5f );
        float leftWidth = leftHeight * aspect;
        float rightHeight = 2f * rightZ * Mathf.Tan(fov * 0.5f );
        float rightWidth = rightHeight * aspect;
        
        float leftDiff = leftWidth / 4.0f;
        leftCamPos += leftDiff * matchInfo.leftQuad.transform.right ;

        float rightDiff = rightWidth / 4.0f;
        rightCamPos -= rightDiff * matchInfo.rightQuad.transform.right ;
        
        //Debug.Log("diff " + leftDiff + " " + rightDiff);
        //Debug.Log("left " + matchInfo.leftQuad.meshFilter.transform.position + " " + leftCamPos);
        //Debug.Log("right " + matchInfo.rightQuad.meshFilter.transform.position + " " + rightCamPos);
        
        LeftPuzzleCamera.transform.position = leftCamPos ;
        RightPuzzleCamera.transform.position = rightCamPos;
        
        //Debug.Log("quad pos: "+ matchInfo.leftQuad.vertices[0] + " " + matchInfo.rightQuad.vertices[1]);
        //Debug.Log("cam pos: "+ matchInfo.LeftCam.transform.position + " " + matchInfo.RightCam.transform.position);
        
    }

    
    public void SetCompletionCamera(MatchBase matchInfo)
    {
        float fov = 60.0f * Mathf.Deg2Rad;
        
        CompletedEffectCamera.transform.rotation = matchInfo.completeItem.transform.rotation;
        CompletedEffectCamera.transform.RotateAround(matchInfo.completeItem.transform.position,
            matchInfo.completeItem.transform.up, 180);
        
        Vector3 camPos = matchInfo.completeItem.transform.position;
        //local z
        
        //instead of calculating height, i can also have ppl set the height manually?
        MeshRenderer childMesh =  matchInfo.completeItem.transform.GetComponentInChildren<MeshRenderer>();
        float itemHeight = 0.0f;
        if (childMesh && false)
        {
            Bounds bounds = childMesh.bounds;
            itemHeight =Mathf.Max(Mathf.Max( bounds.size.y, bounds.size.x ), bounds.size.z);
            //itemHeight = Mathf.Min(Mathf.Max(bounds.size.y, bounds.size.z), Mathf.Max(bounds.size.x, bounds.size.z), Mathf.Max(bounds.size.y, bounds.size.x));
        }
        else
        {
            itemHeight = matchInfo.completeItemHeight;
        }
        float newZ = itemHeight/(2 * matchInfo.heightPercent * Mathf.Tan(fov * 0.5f ));
        camPos += newZ * matchInfo.completeItem.transform.forward;
        
        Debug.Log("completion cam z: "+ newZ);
        CompletedEffectCamera.transform.position = camPos ;
    }
    
    public void SetSinglePuzzleCamera(MatchBase matchInfo, bool isRight)
    {
        float fov = 60.0f * Mathf.Deg2Rad;

       // float objectHeight = matchInfo.completeItemHeight;
        if (isRight)
        {
            RightPuzzleCamera.transform.rotation = matchInfo.rightQuad.transform.rotation;
            RightPuzzleCamera.transform.RotateAround(matchInfo.rightQuad.transform.position,
                matchInfo.rightQuad.transform.up, 180);
        
            Vector3 camPos = matchInfo.rightQuad.transform.position;
            //local z
            Bounds bounds = matchInfo.rightQuad.transform.GetComponentInChildren<MeshRenderer>().bounds;
            float objectHeight = Mathf.Min(Mathf.Max(bounds.size.y, bounds.size.z), Mathf.Max(bounds.size.x, bounds.size.z), Mathf.Max(bounds.size.y, bounds.size.x));
            float newZ = objectHeight/(2 * matchInfo.heightPercent * Mathf.Tan(fov * 0.5f ));
            camPos += newZ * matchInfo.rightQuad.transform.forward;
        
            RightPuzzleCamera.transform.position = camPos ;
        }
        else
        {
            LeftPuzzleCamera.transform.rotation = matchInfo.leftQuad.transform.rotation;
            LeftPuzzleCamera.transform.RotateAround(matchInfo.leftQuad.transform.position,
                matchInfo.leftQuad.transform.up, 180);
        
            Vector3 camPos = matchInfo.leftQuad.transform.position;
            //local z
            Bounds bounds = matchInfo.leftQuad.transform.GetComponentInChildren<MeshRenderer>().bounds;
            float objectHeight = Mathf.Min(Mathf.Max(bounds.size.y, bounds.size.z), Mathf.Max(bounds.size.x, bounds.size.z), Mathf.Max(bounds.size.y, bounds.size.x));;
            float newZ = objectHeight/(2 * matchInfo.heightPercent * Mathf.Tan(fov * 0.5f ));
            camPos += newZ * matchInfo.leftQuad.transform.forward;
        
            LeftPuzzleCamera.transform.position = camPos ;
        }
        
    }
    
    public void SetCameraState(MatchBase matchInfo, CameraState camState, bool isCut)
    {
        //if isMoveCameraOn is true, then change back to move cameraas
        //if isMoveCameraOn is false, then change to puzzle cameras

        CurrCameraState  = camState;
        Debug.Log(camState);
        
        if (!CompletedEffectCamera )
        {
            Debug.Log("ERROR: No Completion camera on " + matchInfo.name);
            return;
        }
        
        if (!LeftMoveCamera || !RightMoveCamera)
        {
            Debug.Log("ERROR: Move camera not set");
            return;
        }

        if (! LeftPuzzleCamera || ! LeftPuzzleCamera)
        {
            Debug.Log("ERROR: Match camera not set");
            return;
        }

        if (isCut)
        {
            CinemachineCore.GetBlendOverride = GetBlendOverrideCut;
        }
        else
        {
            CinemachineCore.GetBlendOverride = null;
        }
        
        if (camState == CameraState.COMPLETEEFFECT)
        {
            //turn on completion camera
            LeftMoveCamera.Priority = (int) CameraPriority.LOW;
            RightMoveCamera.Priority = (int) CameraPriority.LOW;
            
            LeftPuzzleCamera.Priority = (int)CameraPriority.LOW;
            RightPuzzleCamera.Priority = (int)CameraPriority.LOW;

            CompletedEffectCamera.Priority = (int) CameraPriority.HIGH;
        }
        else if (camState == CameraState.PUZZLE)
        {
            //turn off move camera, turn on puzzle cam
            LeftMoveCamera.Priority = (int)CameraPriority.LOW;
            RightMoveCamera.Priority = (int)CameraPriority.LOW;

            LeftPuzzleCamera.Priority = (int)CameraPriority.HIGH;
            RightPuzzleCamera.Priority = (int)CameraPriority.HIGH;
            
            /*LeftPuzzleCamera.Priority = (int)CameraPriority.HIGH;
            RightPuzzleCamera.Priority = (int)CameraPriority.HIGH;*/
            
            CompletedEffectCamera.Priority = (int) CameraPriority.LOW;
        }
        else if (camState == CameraState.MOVE)
        {
            //turn on move camera
            LeftMoveCamera.Priority = (int)CameraPriority.HIGH;
            RightMoveCamera.Priority = (int)CameraPriority.HIGH;

            LeftPuzzleCamera.Priority = (int)CameraPriority.LOW;
            RightPuzzleCamera.Priority = (int)CameraPriority.LOW;
            
            CompletedEffectCamera.Priority = (int) CameraPriority.LOW;
        }


        if (camState == CameraState.PUZZLE)
        {
            //find shortest distance between player and camera
            //move camera to player's distance (local z)
            //move camera left or right(local x)
            //float distanceToPlayer = UnityEngine.Vector3.Distance(matchInfo.LeftCam.transform.position, PlayerManager.Instance.player1.transform.position);

            //matchInfo.LeftCam.transform.position = PlayerManager.Instance.player1.transform.position + (PlayerManager.Instance.player1.transform.forward * 2.0f) - (PlayerManager.Instance.player1.transform.right * 0.5f) + (UnityEngine.Vector3.up * 1.5f);
        }
    }

    CinemachineBlendDefinition GetBlendOverrideCut(
        ICinemachineCamera fromvcam,
        ICinemachineCamera tovcam,
        CinemachineBlendDefinition defaultblend,
        MonoBehaviour owner)
    {
        return new CinemachineBlendDefinition { m_Style = CinemachineBlendDefinition.Style.Cut };
    }

    public IEnumerator CompletionCameraZoom(MatchBase matchInfo, float zoomDist)
    {
        float lerpProgress = 0.0f;
        Vector3 newPos = CompletedEffectCamera.transform.position - CompletedEffectCamera.transform.forward * zoomDist;
        Vector3 oldPos = CompletedEffectCamera.transform.position;
        Vector3 currentPos = CompletedEffectCamera.transform.forward;
        
        while (lerpProgress < 1.0f)
        {
            lerpProgress += MatchManager.Instance.CompleteCamZoomSpeed * Time.unscaledDeltaTime;
            currentPos = Vector3.Lerp(oldPos, newPos, lerpProgress);

            //Debug.Log("completion cam zoom pos: "+ currentPos);
            CompletedEffectCamera.transform.position = currentPos ;
            
            yield return new WaitForEndOfFrame();
        }
        yield return new WaitForEndOfFrame();
    }

    public IEnumerator ChangeViewport(bool isScreenWidening, bool isGradualTransition, bool isRight)
    {
        Debug.Log("Change Viewport");
        Rect tempLeftRect = LeftCinemachineBrain.OutputCamera.rect;
        Rect tempRightRect = RightCinemachineBrain.OutputCamera.rect;
        Vector3 linePos = GeneralUI.MiddleLine.transform.position;
        float width = Screen.width / 2;

        float lerpProgress = 0.0f;

        if (isScreenWidening)
        {
            LeftCinemachineBrain.gameObject.SetActive(false);
            while (tempRightRect.x > 0.0001)
            {
                if (isGradualTransition)
                {
                    lerpProgress += MatchManager.Instance.ViewportShiftSpeed * Time.deltaTime;
                }
                else
                {
                    lerpProgress = 1.0f;
                }

                tempLeftRect.x = Mathf.Lerp(0.0f, -0.4999f, lerpProgress);
                tempRightRect.width = Mathf.Lerp(0.5f, 1f, lerpProgress);
                tempRightRect.x = Mathf.Lerp(0.5f, 0.0001f, lerpProgress);

                //Debug.Log(lerpProgress + " lwidth " + tempLeftRect.width + " r width " + tempRightRect.width + " RX " + tempRightRect.x);

                LeftCinemachineBrain.OutputCamera.rect = tempLeftRect;
                RightCinemachineBrain.OutputCamera.rect = tempRightRect;

                //UI
                linePos.x = Mathf.Lerp(width, 0.0f, lerpProgress);
                GeneralUI.MiddleLine.transform.position = linePos;

                yield return new WaitForEndOfFrame();
            }
            //LeftCinemachineBrain.gameObject.SetActive(false);
            MatchManager.Instance.IsViewportOneCamera = true;
        }
        else
        {
            LeftCinemachineBrain.gameObject.SetActive(true);
            while (tempRightRect.x < 0.5)
            {
                if (isGradualTransition)
                {
                    lerpProgress += MatchManager.Instance.ViewportShiftSpeed * Time.deltaTime;
                }
                else
                {
                    lerpProgress = 1.0f;
                }

                tempLeftRect.x = Mathf.Lerp(-0.4999f, 0.0f, lerpProgress);
                tempRightRect.width = Mathf.Lerp(1.0f, 0.5f, lerpProgress);
                tempRightRect.x = Mathf.Lerp(0.0001f, 0.5f, lerpProgress);

                //Debug.Log(lerpProgress + " lwidth " + tempLeftRect.width + " r width " + tempRightRect.width + " RX " + tempRightRect.x);

                LeftCinemachineBrain.OutputCamera.rect = tempLeftRect;
                RightCinemachineBrain.OutputCamera.rect = tempRightRect;

                //UI
                linePos.x = Mathf.Lerp(0, width, lerpProgress);
                GeneralUI.MiddleLine.transform.position = linePos;

                yield return new WaitForEndOfFrame();
            }
            LeftCinemachineBrain.gameObject.SetActive(true);
            MatchManager.Instance.IsViewportOneCamera = false;

        }

        yield return new WaitForEndOfFrame();
    }
    public void SetAllPlayerBrainsActive(bool isPlayerBrainActive)
    {
        if (LeftCinemachineBrain != null)
            LeftCinemachineBrain.gameObject.SetActive(isPlayerBrainActive);
        if (RightCinemachineBrain != null)
            RightCinemachineBrain.gameObject.SetActive(isPlayerBrainActive);
        if (CutsceneCinemachineBrain != null)
            CutsceneCinemachineBrain.gameObject.SetActive(!isPlayerBrainActive);
    }

    public void PlayTimeline(PlayableAsset playable)
    {
        Director.playableAsset = playable;
        Director.Play();
        
        Debug.Log("Play Timeline " + playable.name);
    }
}
