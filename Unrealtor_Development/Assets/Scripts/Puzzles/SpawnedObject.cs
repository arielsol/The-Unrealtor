using DG.Tweening;
using UnityEngine;

public class SpawnedObject : MonoBehaviour
{
    [SerializeField] public bool IsRotating = false;
    void Start()
    {
        
    }

    // Update is called once per frame
   /*void Update()
    {
        if(IsRotating)
        {
            transform.DORotate(new Vector3(0, 360, 0), 5f, RotateMode.FastBeyond360)
                 .SetLoops(-1, LoopType.Restart)
                 .SetEase(Ease.Linear).SetRelative();
        }
        
    }*/ 
}
