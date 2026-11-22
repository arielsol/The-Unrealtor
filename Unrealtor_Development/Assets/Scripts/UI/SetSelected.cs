using UnityEngine;
using UnityEngine.EventSystems;

public class SetSelected : MonoBehaviour
{
    public GameObject button;

    public void SetSelectedButton()
    {
        EventSystem.current.SetSelectedGameObject(button);
    }
}
