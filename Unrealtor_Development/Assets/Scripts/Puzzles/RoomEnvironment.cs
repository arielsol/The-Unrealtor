using UnityEngine;

public class RoomEnvironment : MonoBehaviour
{
    public enum RoomEnvironmentType
    {
        STUDIOROOM,
        ACTONEROOM,
        ACTTWOROOM,
        ACTTHREEROOM,
        NONE
    }
    [SerializeField] public RoomEnvironmentType ThisRoomEnvironmentType;
}
