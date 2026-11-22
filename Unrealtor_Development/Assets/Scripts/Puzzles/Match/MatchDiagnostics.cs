using UnityEngine;

public class MatchDiagnostics : MonoBehaviour
{
    [SerializeField] private MatchBase targetMatch;
    [SerializeField] private bool showOnGUI = true;
    [SerializeField] private Color ok = Color.green;
    [SerializeField] private Color warn = Color.yellow;
    [SerializeField] private Color bad = Color.red;

    private Camera leftCam;
    private Camera rightCam;

    void Start()
    {
        if (targetMatch == null) targetMatch = GetComponent<MatchBase>();
        var mm = MatchManager.Instance;
        if (mm != null)
        {
            leftCam = CameraPrefabContext.Instance.LeftCinemachineBrain != null ? CameraPrefabContext.Instance.LeftCinemachineBrain.GetComponent<Camera>() : null;
            rightCam = CameraPrefabContext.Instance.RightCinemachineBrain != null ? CameraPrefabContext.Instance.RightCinemachineBrain.GetComponent<Camera>() : null;
        }
    }

    void OnGUI()
    {
        if (!showOnGUI || targetMatch == null || leftCam == null || rightCam == null) return;

        Vector2 tl = Vector2.zero, bl = Vector2.zero, tr = new Vector2(100000f, 0), br = new Vector2(100000f, 0);

        Vector2 biggestLeft = Vector2.zero, secondBiggestLeft = Vector2.zero;
        for (int i = 0; i < targetMatch.leftQuad.vertices.Count; i++)
        {
            Vector3 v = targetMatch.leftQuad.vertices[i];
            Vector2 sp = leftCam.WorldToScreenPoint(v);
            if (sp.x > biggestLeft.x || sp.x > secondBiggestLeft.x)
            {
                if (sp.x > biggestLeft.x)
                {
                    secondBiggestLeft = biggestLeft;
                    biggestLeft = sp;
                }
                else
                {
                    secondBiggestLeft = sp;
                }
            }
        }
        tl = biggestLeft.y > secondBiggestLeft.y ? biggestLeft : secondBiggestLeft;
        bl = biggestLeft.y > secondBiggestLeft.y ? secondBiggestLeft : biggestLeft;

        Vector2 biggestRight = new Vector2(10000f, 0), secondBiggestRight = new Vector2(10000f, 0);
        for (int i = 0; i < targetMatch.rightQuad.vertices.Count; i++)
        {
            Vector3 v = targetMatch.rightQuad.vertices[i];
            Vector2 sp = rightCam.WorldToScreenPoint(v);
            if (sp.x < biggestRight.x || sp.x < secondBiggestRight.x)
            {
                if (sp.x < biggestRight.x)
                {
                    secondBiggestRight = biggestRight;
                    biggestRight = sp;
                }
                else
                {
                    secondBiggestRight = sp;
                }
            }
        }
        tr = biggestRight.y > secondBiggestRight.y ? biggestRight : secondBiggestRight;
        br = biggestRight.y > secondBiggestRight.y ? secondBiggestRight : biggestRight;

        float xMid = Screen.width / 2.0f;
        float topLeftDiff = Mathf.Abs(xMid - tl.x);
        float botLeftDiff = Mathf.Abs(xMid - bl.x);
        float topRightDiff = Mathf.Abs(xMid - tr.x);
        float botRightDiff = Mathf.Abs(xMid - br.x);
        float topDelta = Mathf.Abs(tl.y - tr.y);
        float botDelta = Mathf.Abs(bl.y - br.y);

        float yThreshold = Screen.height * targetMatch.yThresholdPercent / 100;
        float leftXThreshold = Screen.width * targetMatch.leftXThresholdPercent / 100;
        float rightXThreshold = Screen.width * targetMatch.rightXThresholdPercent / 100;
        
        bool leftOK = topLeftDiff <= leftXThreshold && botLeftDiff <= leftXThreshold;
        bool rightOK = topRightDiff <= rightXThreshold && botRightDiff <= rightXThreshold;
        bool yOK = topDelta <= yThreshold && botDelta <= yThreshold;

        GUI.color = leftOK ? ok : bad;
        GUILayout.Label($"Left X: top {topLeftDiff:F1}, bot {botLeftDiff:F1} (thr {leftXThreshold:F1})");
        GUI.color = rightOK ? ok : bad;
        GUILayout.Label($"Right X: top {topRightDiff:F1}, bot {botRightDiff:F1} (thr {rightXThreshold:F1})");
        GUI.color = yOK ? ok : bad;
        GUILayout.Label($"Y Align: top {topDelta:F1}, bot {botDelta:F1} (thr {yThreshold:F1})");
        GUI.color = Color.white;
    }
}


