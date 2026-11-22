using System;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

public class MatchUI : MonoBehaviour
{
    public TextMeshProUGUI MatchText;
    public TextMeshProUGUI EffectText;
    public Image MiddleLine;

    [Header("Matching Alignment Borders")]
    [SerializeField] private GameObject LeftSideAlignedBorder;
    [SerializeField] private GameObject RightSideAlignedBorder;
    [SerializeField]private List<Image> TopLeft_Left;
    [SerializeField]private List<Image> TopLeft_Top;
    [SerializeField]private List<Image> BottomLeft_Left;
    [SerializeField]private List<Image> BottomLeft_Bot;
    [SerializeField]private List<Image> TopRight_Right;
    [SerializeField]private List<Image> TopRight_Top;
    [SerializeField]private List<Image> BottomRight_Right;
    [SerializeField]private List<Image> BottomRight_Bot;
    [Tooltip("Max percent of the threshold that you will see a lerp")]
    [SerializeField] [Range(0,1)]private float lerpPercent; //percent of distance that the alpha lerp will happen, afterwards alpha = 1 
    [Tooltip("Max percent of the screen that corners will tighten")]
    [SerializeField] [Range(0, 1)]private float cornerTightenPercent;
    [SerializeField] private Ease borderCurve;
    //[SerializeField] private Image LSubmitFill;
    //[SerializeField] private Image RSubmitFill;
    
    private bool IsLeftBorderOn = false;
    private bool IsRightBorderOn = false;
    private bool IsCompleteBorderOn = false;
    private float LeftBorderTimer = 0;
    private float RightBorderTimer = 0;
    private float OtherBorderDimension = 3.0f;
    private List<Image> LeftBorderImageList = new List<Image>();
    private List<Image> RightBorderImageList = new List<Image>();

    [Header("Blink Parameters")]
    [SerializeField] private GameObject BlinkTop;
    [SerializeField] private GameObject BlinkBottom;
    [SerializeField] private float BlinkOffset = 450.0f;
    [SerializeField] public float BlinkSpeed = 1.0f;
    [SerializeField] public Ease CloseEyesEase = Ease.OutQuart;
    [SerializeField] private Ease OpenEyesEase = Ease.InQuart;

    [Header("Cutscene Settings")]
    [SerializeField] private GameObject[] ObjectsToHide;

    [SerializeField] public TextMeshProUGUI TestText;
    [SerializeField] public TextMeshProUGUI TestText2;
    private InputAction BlinkAction;
    private TwoPlayerInput PlayerControls;
    private bool IsFlickering = false;
    private Vector2 LeftTopBorderImagePos;
    private Vector2 LeftBotBorderImagePos;
    private Vector2 RightTopBorderImagePos;
    private Vector2 RightBotBorderImagePos;
    Coroutine FlickerCoroutine;
    private Coroutine BlackBarsCoroutine;

    public enum PopUpType
        {
            KEYCLUE_1 = 14,
            KEYCLUE_2 = 15,
            PLUNGERCLUE_1 = 0,
            PLUNGERCLUE_2 = 1,
            EXTINGUISHERCLUE_1 = 2,
            EXTINGUISHERCLUE_2= 3,
            EXTINGUISHERCLUE_3= 16,
            DOORKNOBCLUE_1= 4,
            DOORKNOBCLUE_2= 5,
            DOORKNOBCLUE_3= 6,
            DRAINCLUE_1= 7,
            DRAINCLUE_2= 8, 
            DRAINCLUE_3= 9,
            LAMPCLUE_1= 10,
            LAMPCLUE_2= 11,
            EXITSIGNCLUE_1= 12,
            EXITSIGNCLUE_2= 13
        }
    public enum PopUpFormat
    {
        NOTEBOOK = 0,
        FLYER = 1,
        CARD = 2,
        BOOK = 3, 
        PAPER = 4
    }
    public enum PopUpSide
    {
        Left = 0,
        Right = 1
    }
    [System.Serializable] public class PopUpObjects
    {
        public string id;
        public PopUpType type;
        public PopUpFormat format;
        public string text;
    }
    public List<PopUpObjects> PopUpList;
    
    [System.Serializable] public class PopUpImages
    {
        public string id;
        public PopUpFormat format;
        public PopUpSide side;
        public Image image;
        public TextMeshProUGUI uiText ;
    }
    [SerializeField]private List<PopUpImages> PopUpFormatList;
    //public Image PopUpObject;



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //this is just for testing
        PlayerControls = PlayerInputManager.Instance.Controls;
        //BlinkAction = PlayerControls.Gameplay.Jump;
        //BlinkAction.Enable();
        //BlinkAction.performed += Blink;
        
        //PopUpObject.gameObject.SetActive(false);
        foreach (PopUpImages item in PopUpFormatList)
        {
            item.image.gameObject.SetActive(false);
            item.uiText.gameObject.SetActive(false);
        }
        
        //fill border image lists
        if (LeftSideAlignedBorder)
        {
            foreach (Transform child in LeftSideAlignedBorder.transform.GetComponentsInChildren<Transform>())
            {
                Image img = child.GetComponent<Image>();
                if (img != null)
                {
                    LeftBorderImageList.Add(img);
                }
            }
        }
        if (RightSideAlignedBorder)
        {
            foreach (Transform child in RightSideAlignedBorder.transform.GetComponentsInChildren<Transform>())
            {
                Image img = child.GetComponent<Image>();
                if (img != null)
                {
                    RightBorderImageList.Add(img);
                }
            }
        }

        ActivateLeftBorder(false);
        ActivateRightBorder(false);
        IsFlickering =  false;
        
        for(int i = 0; i<2; i++)
        {
            RectTransform topTransf = TopLeft_Left[i].gameObject.GetComponent<RectTransform>();
            /*TopLeft_Left[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(otherDimension, height + (i * 15));
            BottomLeft_Left[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(otherDimension, height + (i * 15));*/
            TopLeft_Top[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(0, OtherBorderDimension);
            BottomLeft_Bot[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(0,OtherBorderDimension);
            TopRight_Top[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(0, OtherBorderDimension);
            BottomRight_Bot[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(0, OtherBorderDimension);
            TopLeft_Left[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(OtherBorderDimension, 0);
            BottomLeft_Left[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(OtherBorderDimension, 0);
            TopRight_Right[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(OtherBorderDimension, 0);
            BottomRight_Right[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(OtherBorderDimension, 0);

        }
        
    }


    public void HideAllPickupPopupsImmediate()
    {
        if (PopUpFormatList == null) return;
        foreach (PopUpImages entry in PopUpFormatList)
        {
            if (entry == null) continue;
            if (entry.image != null)
                entry.image.gameObject.SetActive(false);
            if (entry.uiText != null)
                entry.uiText.gameObject.SetActive(false);
        }
    }

    public void UpdatePopUp(bool isActive, PopUpType type, BasePlayerController.PlayerID playerID)
    {
        PopUpObjects currObj = PopUpList.Find(popup => popup.type == type);
        if (currObj == null)
            return;

        PopUpSide side = playerID == BasePlayerController.PlayerID.Player1 ? PopUpSide.Left : PopUpSide.Right;
        PopUpImages currImage = PopUpFormatList.Find(images => images.format == currObj.format && images.side == side);
        if (currImage == null)
            return;

        if (!isActive)
        {
            currImage.image.gameObject.SetActive(isActive);
            currImage.uiText.gameObject.SetActive(isActive);
            

                UIManager.Instance.CallUnlockPlayerForPickupPopup(playerID);
            
            /*currImage.uiText.gameObject.gameObject.transform.DOPunchScale(new Vector3(0.1f, 0.1f, 0.1f), 0.3f, 5, 1.0f);
            currImage.image.gameObject.gameObject.transform.DOPunchScale(new Vector3(0.1f, 0.1f, 0.1f), 0.3f, 5, 1.0f)
                .OnComplete(() =>
                {
                    currImage.image.gameObject.SetActive(isActive);
                    currImage.uiText.gameObject.SetActive(isActive);
                    DOVirtual.DelayedCall(0.2f, () =>
                    {

                        UIManager.Instance.CallUnlockPlayerForPickupPopup(playerID);
                    });
                });*/
        }
        else
        {
            currImage.image.gameObject.SetActive(isActive);
            currImage.uiText.gameObject.SetActive(isActive);
            currImage.image.gameObject.gameObject.transform.DOPunchScale(new Vector3(0.1f, 0.1f, 0.1f), 0.2f, 5, 1.0f);
            currImage.uiText.gameObject.gameObject.transform.DOPunchScale(new Vector3(0.1f, 0.1f, 0.1f), 0.2f, 5, 1.0f);
        }
        
        if (isActive)
            currImage.uiText.text = currObj.text;

        ApplyPickupWorldSpaceControlContext(currImage, playerID, isActive);
    }

    /// <summary>
    /// World-space KB vs gamepad hints follow the player who opened the popup: single-player uses that
    /// device's bindings; two-player shows only that side's column so mixed KBM + pad does not show the wrong player's icons.
    /// </summary>
    private static void ApplyPickupWorldSpaceControlContext(PopUpImages currImage, BasePlayerController.PlayerID playerID, bool isActive)
    {
        Transform anchor = currImage.image != null ? currImage.image.transform : currImage.uiText != null ? currImage.uiText.transform : null;
        if (anchor == null)
            return;

        Transform searchRoot = anchor.parent != null ? anchor.parent : anchor;
        WorldSpaceControlSchemeUI[] uis = searchRoot.GetComponentsInChildren<WorldSpaceControlSchemeUI>(true);
        if (uis == null || uis.Length == 0)
            return;

        BasePlayerController.PlayerID? ctx = isActive ? playerID : (BasePlayerController.PlayerID?)null;
        foreach (WorldSpaceControlSchemeUI ui in uis)
        {
            if (ui == null) continue;
            ui.SetDeviceContextPlayer(ctx);
            if (isActive)
                ui.ForceRefresh();
        }
    }

    private static PopUpSide SideForPlayer(BasePlayerController.PlayerID playerId) =>
        playerId == BasePlayerController.PlayerID.Player1 ? PopUpSide.Left : PopUpSide.Right;

    /// <summary>
    /// True if any pickup popup row for this player's side (left = P1, right = P2) is currently shown.
    /// </summary>
    public bool IsPickupPopupOpenForPlayer(BasePlayerController.PlayerID playerId)
    {
        PopUpSide side = SideForPlayer(playerId);
        if (PopUpFormatList == null)
            return false;

        foreach (PopUpImages entry in PopUpFormatList)
        {
            if (entry == null || entry.side != side)
                continue;
            bool imgOn = entry.image != null && entry.image.gameObject.activeSelf;
            bool txtOn = entry.uiText != null && entry.uiText.gameObject.activeSelf;
            if (imgOn || txtOn)
                return true;
        }

        return false;
    }

    /// <summary>
    /// If a pickup popup is visible for this player's side, hides it and returns the clue type (for gameplay sync).
    /// </summary>
    public bool TryClosePickupPopupForInteract(BasePlayerController.PlayerID playerId, out PopUpType closedType)
    {
        closedType = default;
        PopUpSide side = SideForPlayer(playerId);
        if (PopUpFormatList == null)
            return false;

        PopUpImages visibleEntry = null;
        foreach (PopUpImages entry in PopUpFormatList)
        {
            if (entry == null || entry.side != side)
                continue;
            bool imgOn = entry.image != null && entry.image.gameObject.activeSelf;
            bool txtOn = entry.uiText != null && entry.uiText.gameObject.activeSelf;
            if (imgOn || txtOn)
            {
                visibleEntry = entry;
                break;
            }
        }

        if (visibleEntry == null)
            return false;

        PopUpObjects matched = PopUpList != null
            ? PopUpList.Find(p => p.format == visibleEntry.format)
            : null;

        if (matched != null)
        {
            closedType = matched.type;
            UpdatePopUp(false, matched.type, playerId);
        }
        else
        {
            if (visibleEntry.image != null)
                visibleEntry.image.gameObject.SetActive(false);
            if (visibleEntry.uiText != null)
                visibleEntry.uiText.gameObject.SetActive(false);
        }

        return true;
    }

    public void ActivateLeftBorder(bool isLeftActivated)
    {
        LeftSideAlignedBorder.SetActive(isLeftActivated);
        //Debug.Log("Set Left Border Active: "  + isLeftActivated);
    }

    public void ActivateRightBorder(bool isRightActivated)
    {
        RightSideAlignedBorder.SetActive(isRightActivated);
        //Debug.Log("Set Right Border Active: "  + isRightActivated);
    }

    public void LerpBorder(float xCloseness , float yCloseness, QuadBase.QuadType quadType)
    {
        
            float xLerpFactor = xCloseness;
            xLerpFactor = DOVirtual.EasedValue(0, 1, xLerpFactor, borderCurve);
            xLerpFactor = Mathf.Clamp(xLerpFactor, 0, 1);
            float width = 694 *  xLerpFactor;
            
            float yLerpFactor = yCloseness;
            yLerpFactor = DOVirtual.EasedValue(0, 1, yLerpFactor, borderCurve);
            yLerpFactor = Mathf.Clamp(yLerpFactor, 0, 1);
            float height = 344 * yLerpFactor;
            //Debug.Log(lerpFactor + " " + quadType);

            if (quadType == QuadBase.QuadType.LeftQuad)
            {
                Color leftColor = LeftBorderImageList[0].color;
                leftColor.a = (xLerpFactor * yLerpFactor)/ 1.0f;

                foreach (Image img in LeftBorderImageList)
                {
                    img.color = leftColor;
                }
                
                 for(int i = 0; i<2; i++)
                 {
                     TopLeft_Left[i].color = leftColor;
                     BottomLeft_Left[i].color = leftColor;
                     TopLeft_Top[i].color = leftColor;
                     BottomLeft_Bot[i].color = leftColor;
                     
                     TopLeft_Top[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(width + (i * 11), OtherBorderDimension);
                     BottomLeft_Bot[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(width + (i * 11), OtherBorderDimension);
                     
                     TopLeft_Left[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(OtherBorderDimension, height + (i * 11));
                     BottomLeft_Left[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(OtherBorderDimension, height + (i * 11));

                 }

            }
            else if (quadType == QuadBase.QuadType.RightQuad)
            {
                Color rightColor = RightBorderImageList[0].color;
                rightColor.a = (xLerpFactor * yLerpFactor)/ 1.0f;
                foreach (Image img in RightBorderImageList)
                {
                    img.color = rightColor;
                }
                
                for(int i = 0; i<2; i++)
                {
                    TopRight_Right[i].color = rightColor;
                    BottomRight_Right[i].color = rightColor;
                    TopRight_Top[i].color = rightColor;
                    BottomRight_Bot[i].color = rightColor;

                    TopRight_Top[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(width + (i * 11), OtherBorderDimension);
                    BottomRight_Bot[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(width + (i * 11), OtherBorderDimension);
                    
                    TopRight_Right[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(OtherBorderDimension, height + (i * 11));
                    BottomRight_Right[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(OtherBorderDimension, height + (i * 11));

                }
            }
        
    } 

    public void LerpBorderY(float closeness , QuadBase.QuadType quadType)
    {
        float lerpFactor = closeness;
        lerpFactor = DOVirtual.EasedValue(0, 1, lerpFactor, borderCurve);
        lerpFactor = Mathf.Clamp(lerpFactor, 0, 1);
        float height = 344 * lerpFactor;

        //Debug.Log("Lerp border y:" + closeness );
        if (quadType == QuadBase.QuadType.LeftQuad)
        {
            for(int i = 0; i<2; i++)
            {
                TopLeft_Left[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(OtherBorderDimension, height + (i * 11));
                BottomLeft_Left[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(OtherBorderDimension, height + (i * 11));
            }
        }

        else if (quadType == QuadBase.QuadType.RightQuad)
        {
            for(int i = 0; i<2; i++)
            {
                TopRight_Right[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(OtherBorderDimension, height + (i * 11));
                BottomRight_Right[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(OtherBorderDimension, height + (i * 11));
            }
        }

    }
    
    public void SetFullBorders(bool isFullBorders)
    {
        float width = 0;
        float height = 0;
        if (isFullBorders)
        {
            width = 694 ;
            height = 344;
        }
        else
        {
            width = 0 ;
            height = 0;
        }

        Color newColor = LeftBorderImageList[0].color;
        newColor.a = 1;

        foreach (Image img in LeftBorderImageList)
        {
            img.color = newColor;
        }
        foreach (Image img in RightBorderImageList)
        {
            img.color = newColor;
        }
        
         for(int i = 0; i<2; i++)
         {
             TopLeft_Left[i].color = newColor;
             BottomLeft_Left[i].color = newColor;
             TopLeft_Top[i].color = newColor;
             BottomLeft_Bot[i].color = newColor;
             
             TopRight_Right[i].color = newColor;
             BottomRight_Right[i].color = newColor;
             TopRight_Top[i].color = newColor;
             BottomRight_Bot[i].color = newColor;
             
             
             TopLeft_Top[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(width + (i * 11), OtherBorderDimension);
             BottomLeft_Bot[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(width + (i * 11), OtherBorderDimension);
             TopRight_Top[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(width + (i * 11), OtherBorderDimension);
             BottomRight_Bot[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(width + (i * 11), OtherBorderDimension);

             TopLeft_Left[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(OtherBorderDimension, height + (i * 11));
             BottomLeft_Left[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(OtherBorderDimension, height + (i * 11));
             TopRight_Right[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(OtherBorderDimension, height + (i * 11));
             BottomRight_Right[i].gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(OtherBorderDimension, height + (i * 11));
         

        }
    }

    public void Flicker(bool flickerInput)
    {
        if (flickerInput && !IsFlickering)
        {
            IsFlickering = flickerInput;

            FlickerCoroutine = StartCoroutine(FlickerLoop());
        }
        else if (!flickerInput)
        {
            if (IsFlickering)
            {
                foreach (Image img in LeftBorderImageList)
                {
                    if (img)
                    {
                        img.color = Color.white;
                    }
                }
                foreach (Image img in RightBorderImageList)
                {
                    if (img)
                    {
                        img.color = Color.white;
                    }
                }
            }
            
            IsFlickering = false;
            if (FlickerCoroutine != null)
            {
                StopCoroutine(FlickerCoroutine);
            }
            FlickerCoroutine  = null;
            
        }
    

}

    IEnumerator FlickerLoop()
    {
        bool isBorderOn = true;
        Color borderOnColor = Color.white;
        borderOnColor.a = 0.8f;
        Color borderOffColor = Color.white;
        borderOffColor.a = 0.0f;

        Color currColor;
        
        while (IsFlickering)
        {
            if (isBorderOn)
            {
                currColor = borderOnColor;
            }
            else
            {
                currColor = borderOffColor;
            }
            
            foreach (Image img in LeftBorderImageList)
            {
                if (img)
                {
                    img.color = currColor;
                }
            }
            foreach (Image img in RightBorderImageList)
            {
                if (img)
                {
                    img.color = currColor;
                }
            }
            isBorderOn = !isBorderOn;
            yield return new WaitForSeconds(0.5f);

            if (!IsFlickering)
            {
                foreach (Image img in LeftBorderImageList)
                {
                    if (img)
                    {
                        img.color = Color.white;
                    }
                }
                foreach (Image img in RightBorderImageList)
                {
                    if (img)
                    {
                        img.color = Color.white;
                    }
                }
            }
        }
    }
    
    public void ActivateCompleteBorder(bool isCompleteActivated)
    {
        //LevelCompletedBorder.SetActive(isCompleteActivated);
    }

    public void SetMatchText(string newText)
    {
        MatchText.text = newText;
    }
    
    public void SetEffectText(string newText)
    {
        MatchText.text = newText;
    }

    public IEnumerator WaitAndResetText(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        MatchText.text = "";
        EffectText.text = "";
    }

    public IEnumerator WaitAndResetBorder(float waitTime, GameObject thisObject)
    {
        yield return new WaitForSeconds(waitTime);
        thisObject.SetActive(false);
    }

    public void ResetMatchUI()
    {
        
        LeftSideAlignedBorder.SetActive(false);
        RightSideAlignedBorder.SetActive(false);
        MatchText.text = "";
        EffectText.text = "";
    }

    public void CutsceneUIEffect(bool isCutscene)
    {
        if (BlackBarsCoroutine != null)
        {
            StopCoroutine(BlackBarsCoroutine);
        }
        BlackBarsCoroutine  = null;
        BlackBarsCoroutine = StartCoroutine(BlackBars(isCutscene));
        
        if (isCutscene)
        {
            HandleCutsceneUI(true); // Should be refactored into MatchManager probably

            ActivateLeftBorder(false);
            ActivateRightBorder(false);
        }
        else
        {
            HandleCutsceneUI(false); // Should be refactored into MatchManager probably
        }
    }

    public IEnumerator BlackBars(bool isCutscene)
    {
        float originalHeight = 0.0f;
        float expandedHeight = 60.0f;

        float lerpFactor = 0.0f;
        float height = 0.0f;
        if (!isCutscene)
        {
            originalHeight = 60.0f;
            expandedHeight = 0.0f;
        }
        bool isDone = false;

        float targetTime = 1.0f;
        float currTime = 0.0f;
        //Debug.Log( "black bars coming into frame? " + isCutscene);
        
        while(!isDone)
        {
            currTime  += Time.deltaTime;
            Canvas uiCanvas = gameObject.GetComponent<Canvas>();
            float scaleFactor = uiCanvas.referencePixelsPerUnit;
            //Debug.Log( "black bars "+ height +  " " + lerpFactor + " " + scaleFactor + " " + Time.deltaTime);
            lerpFactor = currTime / targetTime;

            height = DOVirtual.EasedValue(originalHeight,expandedHeight, lerpFactor, OpenEyesEase) ;
     
            BlinkTop.gameObject.GetComponent<RectTransform>().sizeDelta = new Vector3(2000,height);
            BlinkBottom.gameObject.GetComponent<RectTransform>().sizeDelta = new Vector3(2000, height);

             if (currTime >= targetTime)
                {
                    Debug.Log( "black bars cutscene"+ height +  " " + lerpFactor);

                    isDone = true;
                }
            
            
            yield return new WaitForEndOfFrame();
        }
        yield return null;
    }

    public IEnumerator ChangeViewportUI(float speed, bool isCompleteEffect)
    {
        float lerpProgress = 0.0f;
        Vector3 linePos = MiddleLine.transform.position;

        if (isCompleteEffect)
        {
            while (linePos.x > 800)
            {
                lerpProgress += speed * Time.deltaTime;
                linePos.x = Mathf.Lerp(0.0f, -800.0f, lerpProgress);
                MiddleLine.transform.position = linePos;
                
                yield return new WaitForEndOfFrame();
            }
        }
        else
        {
            while (linePos.x < 0.0f)
            {
                lerpProgress += speed * Time.deltaTime;
                linePos.x = Mathf.Lerp(-800.0f, 0.0f, lerpProgress);
                MiddleLine.transform.position = linePos;
                
                yield return new WaitForEndOfFrame();
            }
        }

    }

    public void SetSubmitFill(float fillPercent, bool isLeft)
    {
        /*LSubmitFill.fillAmount = fillPercent/2;
        RSubmitFill.fillAmount = fillPercent/2;
        
        return;
        if (isLeft)
        {
            LSubmitFill.fillAmount = fillPercent/2;
        }
        else //right
        {
            RSubmitFill.fillAmount = fillPercent/2;
        }*/
    }

    
    public void HandleCutsceneUI(bool isCutscene)
    {
        foreach (GameObject obj in ObjectsToHide)
            if (isCutscene)
            {
                obj.SetActive(false);
            }
            else
            {
                obj.SetActive(true);
            }
    }
    
    
}
