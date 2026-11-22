using UnityEngine;
using FMODUnity;
using System.Collections.Generic;

public enum SoundID
{
    // General
    Ambience, //In
    // Footsteps
    FootstepWood, //In Game
    FootstepConcrete, //In Game 
    FootstepSmooth, //In Game
    FootstepRug, //In Game
    // Puzzle
    MatchSuccess, //In Game
    PuzzlePieceApproached, //In Game
    GravityStairsUsed,
    // Interactible
    InteractibleHovered, //In Game
    InteractibleClicked, //In Game
    InteractibleDismissed, //In Game
    // Door
    DoorOpen, //In Game
    // Main Menu
    MainMenuHovered, //In
    MainMenuClicked, //In
    // Audio
    AudioUpClicked, //In
    AudioDownClicked, //In
    // Back
    BackClicked, //In
    // Start
    StartClicked, //In
    // Pause Menu
    PauseMenuHovered, //In
    PauseMenuClick, //In
    PauseMenuOpened, //In
    PauseMenuClosed, //In
    // Studio
    StudioAmbience, //In
    KeyOpeningDoor, //In
    BuzzingFluorescentLights, //In
    // Title Screen
    TitleScreenStinger,
    // Smartphone
    SmartphoneRing,
    // Final BGM
    FinalBGM, //In
    // Light turning on
    LightTurningOn, //In
    // Living Room / Bedroom Ambience
    LivingRoomBedroomAmbience, //In
    // Water Ambience
    WaterAmbience,
    // Fire Burning
    FireBurning,
    // Clog Noises
    ClogNoises,
    // Game Room Speaker
    GameRoomSpeaker,
    // Unclog/Suction Pop
    UnclogSuctionPop,
    // Fire Extinguisher Spray
    FireExtinguisherSpray, //In
    // Water Ambience 2
    WaterAmbience2,
    // Shower
    Shower,
    // Toilet Flush
    ToiletFlush,
    // Water Draining
    WaterDraining, //In
    // Stuck Door Rattling
    StuckDoorRattling,
    // Door Unlocked
    DoorUnlocked, //In
    // Exit Sign Matched
    ExitSignMatched,
    // Ending Stinger
    EndingStinger,
    // Apartment Collapse
    ApartmentCollapse,
    // Exterior Ambience
    ExteriorAmbience,

    //Ambient Right

    RightAmbience,

    //Ambient Left

    LeftAmbience,     FootstepLeft, //In Game    FootstepRight, //In Game    UIBoxCheck,    UIBoxUncheck,    UIStartButtonHover,    SilentUtility, //Used for the music emitter    UIMatchSnap, //In Game    SFXWallFall, //In Game    TitleAppear //In Game
}

public enum SoundCategory
{
    Music,
    SFX,
    Ambience,
    VO,
    UI
}

[System.Serializable]
public class SoundEntry
{
    public SoundID soundID;
    [BankRef] public string bankName;
    public EventReference eventRef;
    public bool isLooping;
    [Range(0f, 1f)] public float defaultVolume = 1f;
    [Range(0.5f, 2f)] public float pitch = 1f;
    public SoundCategory category = SoundCategory.SFX;
    [Range(0, 10)] public int priority = 0;
}

[CreateAssetMenu(fileName = "MusicData", menuName = "Scriptable Objects/MusicData")]
public class MusicData : ScriptableObject
{
    [Header("Sound Entries")]
    public List<SoundEntry> soundEntries = new List<SoundEntry>();

    [Header("Footstep Settings")]
    [Range(0.1f, 1f)]
    public float footstepInterval = 0.5f;
}
