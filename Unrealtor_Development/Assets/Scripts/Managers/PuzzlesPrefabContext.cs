using System.Collections.Generic;
using UnityCommunity.UnitySingleton;
using UnityEngine;
using UnityEngine.ProBuilder.Shapes;

public class PuzzlesPrefabContext : MonoSingleton<PuzzlesPrefabContext>
{

    [SerializeField] public List<MatchBase> MatchBaseList = new List<MatchBase>();
    [SerializeField] public List<DoorBase> DoorBaseList = new List<DoorBase>();
    [SerializeField] public List<GravitySwitchZone> GravityStairsList = new List<GravitySwitchZone>();
    [SerializeField] public List<PickUpObject> PickUpObjectList = new List<PickUpObject>();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        foreach (MatchBase match in MatchBaseList)
        {
            match?.gameObject.SetActive(false);
        }

        foreach (GravitySwitchZone grav in GravityStairsList)
        {
            grav?.gameObject.SetActive(false);
        }

        foreach (DoorBase door in DoorBaseList)
        {
            door?.gameObject.SetActive(false);
        }
    }

    public DoorBase CheckDoor(MatchBase.MatchType matchType)
    {
        DoorBase targetDoor = null;
        foreach (DoorBase door in DoorBaseList) //search through doors
        {
            foreach (MatchBase.MatchType key in door.doorKeyList) //search through key list for matching key
            {
                if (key == matchType)
                {
                    //once found, make sure all they keys are valid
                    
                    foreach (MatchBase.MatchType checkKey in door.doorKeyList)
                    {
                        MatchBase currMatch = FindMatch(checkKey);
                        if (currMatch != null)
                        {
                            if (!currMatch.isSolved)
                            {
                                break;
                            }
                        }
                    }
                    targetDoor = door;
                }
            }
            
        }
        return targetDoor;
    }

    public DoorBase FindDoor(MatchBase.MatchType matchType)
    {
        DoorBase targetDoor = null;
        foreach (DoorBase door in DoorBaseList) //search through doors
        {
            foreach (MatchBase.MatchType key in door.doorKeyList) //search through key list for matching key
            {
                if (key == matchType)
                {
                    targetDoor = door;
                }
            }
            
        }
        return targetDoor;
    }
    
    

    public MatchBase FindMatch(MatchBase.MatchType matchType)
    {
        return MatchBaseList.Find(match => match.MatchTypeEnum ==  matchType);
    }
    
    

}
