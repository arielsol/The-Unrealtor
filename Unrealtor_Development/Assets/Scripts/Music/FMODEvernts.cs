/*using UnityEngine;
using FMODUnity;

// This script is OPTIONAL! to keep all the event references in one place.
public class FMODEvernts : MonoBehaviour
{
    //This is an example for one-shot sfx, because we have "private set" it doesn't show up automaticly in unity editor, so we need to add "field:Header"

    //[field: Header("Coin SFX")]
    //[field: SerializeField] public EventReference coinCollected { get; private set; } 

    [field: Header("Player SFX")]
    [field: SerializedField] public EventReference playerFootsteps { get; private set; }

    public static FMODEvents instance { get; private set; }

    private void Awake()
    {
        if (instance != null)
        {
            Debug.LogError("Found more than one FMOD Events instance in the scene.");
        }
        instance = this;
    }

}
*/
