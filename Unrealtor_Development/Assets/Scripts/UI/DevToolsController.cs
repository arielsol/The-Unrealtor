using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;
using Cursor = UnityEngine.Cursor;
using Image = UnityEngine.UI.Image;

public class DevToolsController : MonoBehaviour
{
    class ButtonInfo
    {
        public MatchBase match;
        public bool isSelected;
    }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public GameObject ShortcutMenu;

    private PlayerInputManager PlayerManager;

    // This is maybe not the right way to handle two player InputActions - made by Ari
    private InputAction shortcutAction1;
    private InputAction shortcutAction2;

    [SerializeField]private List<GameObject> Buttons = new List<GameObject>();
    [SerializeField] private float moveSpeedMultiplier = 2f;
    private List<MatchBase> AutoCompleteMatches = new List<MatchBase>();
    private Dictionary<GameObject, ButtonInfo> ButtonPair = new Dictionary<GameObject, ButtonInfo>();
    private int ButtonIndex = 0;

    private bool IsAutoCocmpletePanelOn = false;
    private bool MoveMultiplierToggled = false;
    
    public Color selectedColor;
    public Color unselectedColor;
    
    
    void Start()
    {
        PlayerManager = PlayerInputManager.Instance;

        shortcutAction1 = PlayerManager.Player1Controls.Gameplay.ShortcutMenu;
        //shortcutAction2 = PlayerManager.Player2Controls.Gameplay.ShortcutMenu;

        shortcutAction1.started += OnShortcutActionStart;
        //shortcutAction2.started += OnShortcutActionStart;
        
        SetShortcutButtons();
        
        EventSystem.current.SetSelectedGameObject(null);

    }


    private void Update()
    {
        
        //Debug.Log("Cursor state " +  Cursor.lockState);

        if (IsAutoCocmpletePanelOn)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        
        #if DEVELOPMENT_BUILD || UNITY_EDITOR
	    if (Input.GetKeyDown(KeyCode.M))
	    {
		    ToggleMoveMultiplier();
	    }
        #endif
    }

    void OnShortcutActionStart(InputAction.CallbackContext context)
    {
#if UNITY_EDITOR
        ShortcutMenu.SetActive(!ShortcutMenu.activeSelf);
        IsAutoCocmpletePanelOn =  ShortcutMenu.activeSelf;
#endif
    }

    private void ToggleMoveMultiplier()
    {
	    MoveMultiplierToggled = !MoveMultiplierToggled;
	    float multiplier = (MoveMultiplierToggled) ? moveSpeedMultiplier : 1 / moveSpeedMultiplier;
	    
	    foreach (var player in FindObjectsByType<BasePlayerController>(FindObjectsSortMode.None))
	    {
		    player.moveSpeed *= multiplier;
	    }
    }
    
    void SetShortcutButtons()
    {
        foreach (MatchBase match in PuzzlesPrefabContext.Instance.MatchBaseList)
        {
            //if (match.gameObject.activeInHierarchy)
            {
                Buttons[ButtonIndex].GetComponentInChildren<TextMeshProUGUI>().text = match.name;
                
                ButtonInfo newButtonInfo = new ButtonInfo();
                newButtonInfo.isSelected = false;
                newButtonInfo.match = match;
                ButtonPair.Add(Buttons[ButtonIndex], newButtonInfo);
                
                Buttons[ButtonIndex].GetComponent<Image>().color = unselectedColor;
                Buttons[ButtonIndex].GetComponent<Selectable>().transition = Selectable.Transition.None;
                
                ButtonIndex++;
                
            }
        }

        for (int i = ButtonIndex; i < Buttons.Count; i++)
        {
            Buttons[i].SetActive(false);
        }
        
    }

    public void MarkCompletedButton(GameObject button)
    {
        //submit puzzles to be completed
        
        button.GetComponent<Selectable>().transition = Selectable.Transition.None;
        ButtonInfo thisButtonInfo = ButtonPair[button];
        MatchBase match = thisButtonInfo.match;

        //Debug.Log(match.name + " click");
        if (!match.isSolved && thisButtonInfo.isSelected) //"deselect"
        {
            button.GetComponent<Image>().color = unselectedColor;
            if (AutoCompleteMatches.Contains(match))
            {
                AutoCompleteMatches.Remove(match);
            }
            thisButtonInfo.isSelected = false;
        }
        else
        {
            button.GetComponent<Image>().color = selectedColor;
            if (!AutoCompleteMatches.Contains(match) && !match.isSolved)
            {
                AutoCompleteMatches.Add(match);
            }
            thisButtonInfo.isSelected = true;
        }

        Cursor.lockState = CursorLockMode.None;
    
    }
    
    public void SubmitButton()
    {
        //GameManager.GameState skipToState = GameManager.GameState.TUTORIAL;
        foreach (MatchBase match in AutoCompleteMatches)
        {
            //Debug.Log(match.name + " is auto completed");
            StartCoroutine(MatchManager.Instance.AutoCompleteMatch(match, true));
            /*if (match.GameStateEnum > skipToState)
            {
                skipToState = match.GameStateEnum;
            }*/
        }

        foreach (MatchBase match in AutoCompleteMatches)
        {
            if (match.MatchTypeEnum == MatchBase.MatchType.EXITSIGN)
            {
                foreach (MatchBase allMatches in PuzzlesPrefabContext.Instance.MatchBaseList)
                {
                    if (!allMatches.isSolved)
                    {
                        StartCoroutine(MatchManager.Instance.AutoCompleteMatch(allMatches, false));
                        GameObject button = GetButton(allMatches);
                        MarkCompletedButton(button);
                    }
                }

                break;
            }
            
            MatchBase currPrevMatch = match;
            while (true)
            {
                currPrevMatch = PuzzlesPrefabContext.Instance.MatchBaseList.Find(m => m.MatchTypeEnum == currPrevMatch.PrevMatchEnum);
                if(currPrevMatch != null)
                {
                    if (!currPrevMatch.isSolved)
                    {
                        StartCoroutine (MatchManager.Instance.AutoCompleteMatch(currPrevMatch, false));
                        GameObject button = GetButton(currPrevMatch);
                        MarkCompletedButton(button);
                    }
                    else
                    {
                        break;
                    }
                }
                else if (currPrevMatch == null)
                {
                    break;
                }
            }
        }

        
        //iterate through doors and complete them if possible
        foreach (DoorBase door in PuzzlesPrefabContext.Instance.DoorBaseList)
        {
            if (door.gameObject.activeInHierarchy)
            {
                if (door.CheckDoorPrereqs())
                {
                    door.OpenDoor();
                }
            }
        }
        
        ShortcutMenu.SetActive(false);
        IsAutoCocmpletePanelOn =  false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    GameObject GetButton(MatchBase match)
    {
        foreach (KeyValuePair<GameObject, ButtonInfo> pair in ButtonPair)
        {
            if(pair.Value.match == match) return pair.Key;
        }
        return null;
    }

    public void ResetMatches()
    {
        foreach (MatchBase match in PuzzlesPrefabContext.Instance.MatchBaseList)
        {
            if (match)
            {
                match.isSolved = false;
                
                MatchManager.Instance.AddNearbyMatch(match);
                
                foreach (GameObject obj in match.preCamHideObjects)
                {
                    if (obj)
                    {
                        obj.SetActive(true);
                    }
                }
                foreach (GameObject obj in match.postCamHideObj)
                {
                    if (obj)
                    {
                        obj.SetActive(true);
                    }
                }
            }
        }
        
        ShortcutMenu.SetActive(false);
        IsAutoCocmpletePanelOn =  false;
        Cursor.lockState = CursorLockMode.Locked;
    }
    
}
