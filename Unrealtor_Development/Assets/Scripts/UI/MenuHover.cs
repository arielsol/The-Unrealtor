using UnityEngine;

using UnityEngine.UI;

using UnityEngine.EventSystems;

using TMPro;

using FMODUnity;
using Unity.AppUI.UI;
using Button = UnityEngine.UI.Button;
using Text = UnityEngine.UI.Text;


[RequireComponent(typeof(Button))]

public class MenuHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler

{

	[Header("Color Animation")] [SerializeField]
	private bool animateColor = true;

	[SerializeField] private Color normalColor = Color.white;

	[SerializeField] private Color hoverColor = new Color(0.8f, 0.9f, 1f);



	[Header("Scale Animation")] [SerializeField]
	private bool animateScale = true;

	[SerializeField] private float normalScale = 1f;

	[SerializeField] private float hoverScale = 1.1f;



	[Header("Animation Settings")] [SerializeField]
	private float animationSpeed = 10f;

	[SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);



	[Header("Text Color")] [SerializeField]
	private bool animateTextColor = false;

	[SerializeField] private Color normalTextColor = Color.white;

	[SerializeField] private Color hoverTextColor = Color.yellow;
	
	[Header("Audio")] [SerializeField] private string soundEffectOnHover;


	private StartMenu startMenu;

	private Image buttonImage;

	[SerializeField, Tooltip("Leave blank to text located in child object.")] private TextMeshProUGUI tmpText;

	private Text legacyText;

	private Vector3 originalScale;

	public bool isHovering {get; private set;} = false;

    private float currentTime = 0f;



    void Start()

    {
        startMenu = GetComponentInParent<StartMenu>();

        buttonImage = GetComponent<Image>();

        if (tmpText == null)
        {
	        tmpText = GetComponentInChildren<TextMeshProUGUI>();
        }

        legacyText = GetComponentInChildren<Text>();

        originalScale = transform.localScale;



        if (animateColor && buttonImage != null)

        {

            buttonImage.color = normalColor;

        }



        if (animateTextColor)

        {

            if (tmpText != null)

            {
                tmpText.color = normalTextColor;

            }

            else if (legacyText != null)

            {

                legacyText.color = normalTextColor;

            }

        }

    }



    void Update()

    {

        if (isHovering)

        {

            currentTime = Mathf.Min(currentTime + Time.unscaledDeltaTime * animationSpeed, 1f);


        }

        else

        {

            currentTime = Mathf.Max(currentTime - Time.unscaledDeltaTime * animationSpeed, 0f);

        }



        float t = animationCurve.Evaluate(currentTime);



        if (animateColor && buttonImage != null)

        {

            buttonImage.color = Color.Lerp(normalColor, hoverColor, t);

        }



        if (animateScale)

        {

            float targetScale = Mathf.Lerp(normalScale, hoverScale, t);

            transform.localScale = originalScale * targetScale;

        }



        if (animateTextColor)

        {
            Color textColor = Color.Lerp(normalTextColor, hoverTextColor, t);

            if (tmpText != null)

            {
                tmpText.color = textColor;

            }

            else if (legacyText != null)

            {

                legacyText.color = textColor;

            }

        }

    }



    public void OnPointerEnter(PointerEventData eventData)

    {
        startMenu.PlaySoundEfffect(soundEffectOnHover);

        isHovering = true;

    }



    public void OnPointerExit(PointerEventData eventData)

    {

        isHovering = false;

    }

    public bool GetHover() { return isHovering; }

    public void SetHover(bool b) { isHovering = b; }

}