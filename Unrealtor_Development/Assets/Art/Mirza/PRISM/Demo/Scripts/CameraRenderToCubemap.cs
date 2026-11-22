using UnityEngine;

namespace Mirza.PRISM
{
    [ExecuteAlways]
    public class CameraRenderToCubemap : MonoBehaviour
    {
        Camera camera;
        public RenderTexture target;

        void Start()
        {

        }

        void LateUpdate()
        {
            if (!camera)
            {
                camera = GetComponent<Camera>();
            }

            camera.RenderToCubemap(target);
        }
    }
}