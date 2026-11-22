using UnityEngine;

public class FountainControl : MonoBehaviour
{
    public FountainSpawner Ft;
    public Transform Location;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.M)) {

            Ft.BurstAt(transform.position);
        }
    }
}
