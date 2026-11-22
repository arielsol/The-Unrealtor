using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public enum ReverbType { Hallway, MediumRoom, LivingRoom, Bedroom, Bathroom }
public enum AmbienceType { General, Studio, LivingRoom, Water01, Water02, Exterior }
public enum FloorType { Stone, Wood, Carpet }

public class AudioZones : MonoBehaviour
{
    [SerializeField] public ReverbType reverbType;
    [SerializeField] public AmbienceType ambienceType;
    [SerializeField] public FloorType floorType;
    [SerializeField] private AudioZones parentZone;
    [SerializeField] private bool hasParent;

    public void UpdateZoneLeft()
    {
        RuntimeManager.StudioSystem.setParameterByName("ReverbTypeLeft", (int)reverbType);
        RuntimeManager.StudioSystem.setParameterByName("AmbienceTypeLeft", (int)ambienceType);
        RuntimeManager.StudioSystem.setParameterByName("FloorTypeLeft", (int)floorType);
    }

    public void UpdateLeftFloor()
    {
        if (!hasParent)
        {
            return;
        }
        else
        {
            //Debug.Log("[AZ] left" + " new floor type: " + parentZone.floorType);
            int parentType = (int)parentZone.floorType;
            RuntimeManager.StudioSystem.setParameterByName("FloorTypeLeft", parentType);
        }
    }

    public void UpdateZoneRight()
    {
        Debug.Log("[AZ] update right zone location");
        RuntimeManager.StudioSystem.setParameterByName("ReverbTypeRight", (int)reverbType);
        RuntimeManager.StudioSystem.setParameterByName("AmbienceTypeRight", (int)ambienceType);
        RuntimeManager.StudioSystem.setParameterByName("FloorTypeRight", (int)floorType);
    }

    public void UpdateRightFloor()
    {
        if (!hasParent)
        {
            return;
        }
        else
        {
            //Debug.Log("[AZ] right" + " new floor type: " + parentZone.floorType);
            int parentType = (int)parentZone.floorType;
            RuntimeManager.StudioSystem.setParameterByName("FloorTypeRight", parentType);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        //Debug.Log("[AZ] name: " + other.gameObject.name + " floor type: " + floorType);
        string name = other.gameObject.name;
        if (name == "Player1_Left")
        {
            UpdateZoneLeft();
        }
        if (name == "Player2_Right")
        {
            UpdateZoneRight();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        //Debug.Log("[AZ] exit: " + other.gameObject.name + " floor type: " + floorType);
        string name = other.gameObject.name;
        if (name == "Player1_Left")
        {
            UpdateLeftFloor();
        }
        if (name == "Player2_Right")
        {
            UpdateRightFloor();
        }
    }
}