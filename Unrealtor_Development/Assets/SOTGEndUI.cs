using UnityEngine;

public class SOTGEndUI : MonoBehaviour
{
    public GameObject UIToShow;
    public GameObject[] UIToHide;
    private bool isShowingUI = false;


    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Period))
        {
            if (!isShowingUI)
            {
                foreach (GameObject obj in UIToHide)
                {
                    obj.SetActive(false);
                }
                UIToShow.SetActive(true);
                isShowingUI = true;
            }
            else
            {
                foreach (GameObject obj in UIToHide)
                {
                    obj.SetActive(true);
                }
                UIToShow.SetActive(false);
                isShowingUI = false;
            }
        }
    }
}
