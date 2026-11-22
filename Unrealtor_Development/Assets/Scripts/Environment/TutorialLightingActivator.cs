using System;
using UnityEngine;

public class TutorialLightingActivator : MonoBehaviour
{
    //called after puzzle is completed, this script is attached to an object shown from matchbase
    private void OnEnable()
    {
        TutorialLighting.Instance.BrightenScene();
    }
}
