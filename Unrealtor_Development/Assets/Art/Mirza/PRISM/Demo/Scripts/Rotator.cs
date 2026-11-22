using UnityEngine;

namespace Mirza.PRISM
{
    public class Rotator : MonoBehaviour
    {
        public Vector3 rotation;
        public Space space = Space.World;

        void Start()
        {

        }

        void Update()
        {
            transform.Rotate(rotation * Time.deltaTime, space);
        }
    }
}