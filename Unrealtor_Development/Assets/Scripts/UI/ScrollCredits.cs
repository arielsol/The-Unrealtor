using UnityEngine;
//using Unity.UI;
using System.Collections;

public class ScrollCredits : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    RectTransform selfTransform;
    public MenuController mc;
    public bool isMoving = false;
    public float speed = 0.0f;
    public float time = 0.0f;
    public float waitTime = 10.0f;

    void Start()
    {
        selfTransform = GetComponent<RectTransform>();
    }

    void OnEnable()
    {
        SetMoving();
    }

    // Update is called once per frame
    void Update()
    {
        if (isMoving)
        {
            float newLoc = selfTransform.offsetMin.y;
            newLoc += speed * Time.deltaTime;
            selfTransform.offsetMin = new Vector2(selfTransform.offsetMin.x, newLoc);
            selfTransform.offsetMax = new Vector2(selfTransform.offsetMax.x, newLoc);
        }
        //if (Input.GetKeyDown(KeyCode.N) && !isMoving)
        //{
        //    SetMoving();
        //}
    }

    public void SetMoving()
    {
        // Debug.Log("[SC] moving");
        if (isMoving) return;
        isMoving = true;
        StartCoroutine(MoveTimer());
    }

    private IEnumerator MoveTimer()
    {
        DialogueManager.Instance.StartDialogue("CreditsScreen");
        yield return new WaitForSeconds(time);
        isMoving = false;
        yield return new WaitForSeconds(waitTime);
        mc.LoadMainMenu();
    }
}
