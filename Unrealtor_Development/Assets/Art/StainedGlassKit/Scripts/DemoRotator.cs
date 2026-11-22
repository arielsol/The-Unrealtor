using UnityEngine;

namespace StainedGlassKit
{
    /// <summary>
    /// Slow rotation for demo objects in showcase scenes.
    /// Attach to cubes/spheres to make them rotate in Play mode.
    /// </summary>
    public class DemoRotator : MonoBehaviour
    {
        [Header("Rotation")]
        public float speed = 15f;
        public Vector3 axis = Vector3.up;

        void Update()
        {
            transform.Rotate(axis, speed * Time.deltaTime, Space.World);
        }
    }
}
