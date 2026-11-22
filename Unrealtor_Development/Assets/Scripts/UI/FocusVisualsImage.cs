using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FocusVisualsImage : MonoBehaviour
{
    [Header("Overlay Images")]
    [SerializeField] private Image player1Overlay;
    [SerializeField] private Image player2Overlay;
    
    [Header("Text Elements")]
    [SerializeField] private TextMeshProUGUI[] textLeft;
    [SerializeField] private TextMeshProUGUI[] textRight;
    
    [Header("Animation Settings")]
    [SerializeField] private float fadeInTime = 1f;
    [SerializeField] private float fadeOutTime = 1f;
    [SerializeField] private float maxAlpha = 0.7f; // Maximum alpha for the overlay
    
    [Header("Input Settings")]
    [SerializeField] private KeyCode player1FocusKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode player2FocusKey = KeyCode.RightShift;
    
    // Animation state
    private float currentTime1 = 0f;
    private float currentTime2 = 0f;
    private bool isPlayer1Holding = false;
    private bool isPlayer2Holding = false;
    
    // Initial colors
    private Color p1InitialColor;
    private Color p2InitialColor;
    private Color[] textLeftInitialColors;
    private Color[] textRightInitialColors;

    void Start()
    {
        // Store initial colors
        if (player1Overlay != null)
        {
            p1InitialColor = player1Overlay.color;
            p1InitialColor.a = 0f; // Start transparent
            player1Overlay.color = p1InitialColor;
        }
        
        if (player2Overlay != null)
        {
            p2InitialColor = player2Overlay.color;
            p2InitialColor.a = 0f; // Start transparent
            player2Overlay.color = p2InitialColor;
        }
        
        // Store initial text colors
        if (textLeft != null)
        {
            textLeftInitialColors = new Color[textLeft.Length];
            for (int i = 0; i < textLeft.Length; i++)
            {
                if (textLeft[i] != null)
                {
                    textLeftInitialColors[i] = textLeft[i].color;
                }
            }
        }
        
        if (textRight != null)
        {
            textRightInitialColors = new Color[textRight.Length];
            for (int i = 0; i < textRight.Length; i++)
            {
                if (textRight[i] != null)
                {
                    textRightInitialColors[i] = textRight[i].color;
                }
            }
        }
    }

    void Update()
    {
        // Handle Player 1 input
        HandlePlayerInput(KeyCode.LeftShift, ref isPlayer1Holding, ref currentTime1, 
                         player1Overlay, textLeft, textLeftInitialColors);
        
        // Handle Player 2 input
        HandlePlayerInput(KeyCode.RightShift, ref isPlayer2Holding, ref currentTime2, 
                         player2Overlay, textRight, textRightInitialColors);
    }
    
    private void HandlePlayerInput(KeyCode key, ref bool isHolding, ref float currentTime, 
                                  Image overlay, TextMeshProUGUI[] texts, Color[] initialTextColors)
    {
        // Check for key press/release
        if (Input.GetKeyDown(key))
        {
            isHolding = true;
            currentTime = 0f;
        }
        else if (Input.GetKeyUp(key))
        {
            isHolding = false;
            currentTime = 0f;
        }
        
        // Animate based on holding state
        if (isHolding)
        {
            // Fade in
            if (currentTime < fadeInTime)
            {
                float t = currentTime / fadeInTime;
                t = EaseOutQuad(t); // Smooth easing
                
                // Animate overlay
                if (overlay != null)
                {
                    Color color = overlay.color;
                    color.a = Mathf.Lerp(0f, maxAlpha, t);
                    overlay.color = color;
                }
                
                // Animate text (fade out)
                if (texts != null && initialTextColors != null)
                {
                    for (int i = 0; i < texts.Length; i++)
                    {
                        if (texts[i] != null && i < initialTextColors.Length)
                        {
                            Color textColor = initialTextColors[i];
                            textColor.a = Mathf.Lerp(1f, 0f, t);
                            texts[i].color = textColor;
                        }
                    }
                }
                
                currentTime += Time.deltaTime;
            }
            else
            {
                // Ensure max values are set
                if (overlay != null)
                {
                    Color color = overlay.color;
                    color.a = maxAlpha;
                    overlay.color = color;
                }
                
                if (texts != null && initialTextColors != null)
                {
                    for (int i = 0; i < texts.Length; i++)
                    {
                        if (texts[i] != null && i < initialTextColors.Length)
                        {
                            Color textColor = initialTextColors[i];
                            textColor.a = 0f;
                            texts[i].color = textColor;
                        }
                    }
                }
            }
        }
        else
        {
            // Fade out
            if (currentTime < fadeOutTime)
            {
                float t = currentTime / fadeOutTime;
                t = EaseOutQuad(t); // Smooth easing
                
                // Animate overlay
                if (overlay != null)
                {
                    Color color = overlay.color;
                    color.a = Mathf.Lerp(maxAlpha, 0f, t);
                    overlay.color = color;
                }
                
                // Animate text (fade in)
                if (texts != null && initialTextColors != null)
                {
                    for (int i = 0; i < texts.Length; i++)
                    {
                        if (texts[i] != null && i < initialTextColors.Length)
                        {
                            Color textColor = initialTextColors[i];
                            textColor.a = Mathf.Lerp(0f, 1f, t);
                            texts[i].color = textColor;
                        }
                    }
                }
                
                currentTime += Time.deltaTime;
            }
            else
            {
                // Ensure values are fully reset
                if (overlay != null)
                {
                    Color color = overlay.color;
                    color.a = 0f;
                    overlay.color = color;
                }
                
                if (texts != null && initialTextColors != null)
                {
                    for (int i = 0; i < texts.Length; i++)
                    {
                        if (texts[i] != null && i < initialTextColors.Length)
                        {
                            texts[i].color = initialTextColors[i];
                        }
                    }
                }
            }
        }
    }
    
    // Smooth easing function (quadratic ease out)
    private float EaseOutQuad(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }
    
    // Public methods for external control if needed
    public void SetPlayer1Focus(bool focus)
    {
        isPlayer1Holding = focus;
        currentTime1 = 0f;
    }
    
    public void SetPlayer2Focus(bool focus)
    {
        isPlayer2Holding = focus;
        currentTime2 = 0f;
    }
    
    public bool IsPlayer1Focused()
    {
        return isPlayer1Holding;
    }
    
    public bool IsPlayer2Focused()
    {
        return isPlayer2Holding;
    }
}
