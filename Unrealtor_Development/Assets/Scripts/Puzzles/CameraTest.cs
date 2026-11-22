using Cinemachine;
using UnityEngine;

public class CameraTest : MonoBehaviour
{
    public CinemachineBrain LeftCinemachineBrain;
    public CinemachineBrain RightCinemachineBrain;

    public GameObject testObject;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        Vector2 left = LeftCinemachineBrain.GetComponent<Camera>().WorldToScreenPoint(testObject.transform.position);
        Vector2 Right = RightCinemachineBrain.GetComponent<Camera>().WorldToScreenPoint(testObject.transform.position);

        Debug.Log(gameObject.name +  " Left: " + left + " Right: " + Right);
    }
}