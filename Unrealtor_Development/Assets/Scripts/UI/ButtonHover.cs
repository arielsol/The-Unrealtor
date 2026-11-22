using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Image buttonImage;
    private Color originalColor;

    void Start()
    {
        buttonImage = GetComponent<Image>();
        if (buttonImage != null)
            originalColor = buttonImage.color;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (buttonImage != null)
        {
            Color hoverColor = originalColor;
            hoverColor.a = 0.6f;
            buttonImage.color = hoverColor;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (buttonImage != null)
            buttonImage.color = originalColor;
    }
}