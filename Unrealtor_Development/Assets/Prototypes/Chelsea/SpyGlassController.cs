using UnityEngine;
using Cinemachine;

namespace Unrealtor.Player
{
    public class SpyGlassController : MonoBehaviour
    {
        [Header("SpyGlass Visual")]
        [SerializeField] private Transform CameraTransform;
        [SerializeField] private Camera PlayerCamera;
        [SerializeField] private CinemachineVirtualCamera VirtualCamera;
        [SerializeField] private GameObject SpyGlassPrefab;
        [SerializeField] private Vector3 HeldOffset = new Vector3(0.05f, -0.05f, 0.25f);
        [SerializeField] private Vector3 InUseOffset = new Vector3(-0.15f, -0.07f, 0.35f);
        [SerializeField] private float RaiseSpeed = 10f;

        [Header("Zoom Settings")]
        [SerializeField] private float ZoomSpeed = 5f;

        private readonly float[] ZoomFOVs = new float[] { 73.74f, 39.6f, 10.3f };
        private int CurrentZoomIndex = 0;
        private bool UsingSpyGlass = false;

        [Header("Behavior")]
        public bool AutoSpawnOnStart = false;

        private GameObject SpyGlassInstance;
        private Transform SpyGlassTransform;
        private Transform HeldSpyGlassTransform;
        private bool IsAutoSpawned = false;
        private Vector3 InUseRotationEuler = Vector3.zero;

        private void Start()
        {
            if (AutoSpawnOnStart && SpyGlassPrefab != null && CameraTransform != null)
            {
                SpyGlassInstance = Instantiate(SpyGlassPrefab);
                SpyGlassTransform = SpyGlassInstance.transform;
                IsAutoSpawned = true;

                SpyGlassTransform.position = CameraTransform.position + CameraTransform.TransformVector(HeldOffset);
                SpyGlassTransform.rotation = CameraTransform.rotation;
                SpyGlassTransform.localScale = Vector3.one;

                SpyGlassInstance.SetActive(false);
            }
        }

        private void Update()
        {
            bool holdingCtrlOrCmd =
                Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
                Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);

            bool canUseSpyGlass = IsAutoSpawned && SpyGlassTransform != null;

            if (canUseSpyGlass && holdingCtrlOrCmd && !UsingSpyGlass)
            {
                UsingSpyGlass = true;
                SetSpyGlass(true);
                SetHeldSpyGlassVisible(true);
                CurrentZoomIndex = Mathf.Clamp(1, 0, ZoomFOVs.Length - 1);
            }
            else if ((!holdingCtrlOrCmd || !canUseSpyGlass) && UsingSpyGlass)
            {
                UsingSpyGlass = false;
                SetSpyGlass(false);
                SetHeldSpyGlassVisible(false);
            }

            if (UsingSpyGlass)
            {
                MaintainSpyGlassPosition();
                UpdateZoom();
            }
            else
            {
                float defaultFOV = 60f;
                if (VirtualCamera != null)
                {
                    VirtualCamera.m_Lens.FieldOfView =
                        Mathf.Lerp(VirtualCamera.m_Lens.FieldOfView, defaultFOV, Time.deltaTime * ZoomSpeed);
                }
                else if (PlayerCamera != null)
                {
                    PlayerCamera.fieldOfView =
                        Mathf.Lerp(PlayerCamera.fieldOfView, defaultFOV, Time.deltaTime * ZoomSpeed);
                }
            }
        }

        private void MaintainSpyGlassPosition()
        {
            if (!UsingSpyGlass) return;

            Transform targetTransform = null;
            Vector3 offset = HeldOffset;

            if (HeldSpyGlassTransform != null)
            {
                targetTransform = HeldSpyGlassTransform;
                offset = InUseOffset;
            }
            else if (IsAutoSpawned && SpyGlassTransform != null)
            {
                targetTransform = SpyGlassTransform;
                offset = InUseOffset;
            }

            if (targetTransform == null || CameraTransform == null) return;

            Vector3 targetPosition = CameraTransform.TransformPoint(offset);
            targetTransform.position = Vector3.Lerp(targetTransform.position, targetPosition, Time.deltaTime * RaiseSpeed);
            Quaternion offsetRot = Quaternion.Euler(InUseRotationEuler.y, InUseRotationEuler.x, InUseRotationEuler.z);
            targetTransform.rotation = CameraTransform.rotation * offsetRot;
        }

        public void UpdateZoom()
        {
            if (!UsingSpyGlass) return;

            float scrollInput = Input.GetAxis("Mouse ScrollWheel");
            if (scrollInput > 0f)
                CurrentZoomIndex = (CurrentZoomIndex + 1) % ZoomFOVs.Length;
            else if (scrollInput < 0f)
                CurrentZoomIndex = (CurrentZoomIndex - 1 + ZoomFOVs.Length) % ZoomFOVs.Length;

            if (Input.GetKeyDown(KeyCode.Alpha1)) CurrentZoomIndex = 0;
            if (Input.GetKeyDown(KeyCode.Alpha2)) CurrentZoomIndex = 1;
            if (Input.GetKeyDown(KeyCode.Alpha5)) CurrentZoomIndex = 2;

            float targetFOV = ZoomFOVs[CurrentZoomIndex];

            if (VirtualCamera != null)
            {
                VirtualCamera.m_Lens.FieldOfView =
                    Mathf.Lerp(VirtualCamera.m_Lens.FieldOfView, targetFOV, Time.deltaTime * ZoomSpeed);
            }
            else if (PlayerCamera != null)
            {
                PlayerCamera.fieldOfView =
                    Mathf.Lerp(PlayerCamera.fieldOfView, targetFOV, Time.deltaTime * ZoomSpeed);
            }
        }

        public void SetSpyGlass(bool state)
        {
            if (IsAutoSpawned && SpyGlassInstance != null)
                SpyGlassInstance.SetActive(state);
        }

        public void RegisterHeldSpyGlass(Transform spyGlassTransform)
        {
            HeldSpyGlassTransform = spyGlassTransform;
            SetHeldSpyGlassVisible(false);
            HeldSpyGlassTransform.localPosition = Vector3.zero;
            HeldSpyGlassTransform.localRotation = Quaternion.identity;
        }

        public void UnregisterHeldSpyGlass(Transform spyGlassTransform)
        {
            if (HeldSpyGlassTransform == spyGlassTransform)
            {
                SetObjectVisible(spyGlassTransform, true);
                HeldSpyGlassTransform = null;
            }
        }

        private void SetHeldSpyGlassVisible(bool visible)
        {
            if (HeldSpyGlassTransform == null) return;
            SetObjectVisible(HeldSpyGlassTransform, visible);
        }

        private void SetObjectVisible(Transform target, bool visible)
        {
            if (target == null) return;
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = visible;
            }
        }

        public void SetAutoSpawnOnStart(bool enabled)
        {
            AutoSpawnOnStart = enabled;
            if (enabled)
            {
                if (!IsAutoSpawned)
                {
                    if (SpyGlassPrefab == null || CameraTransform == null) return;
                    SpyGlassInstance = Instantiate(SpyGlassPrefab);
                    SpyGlassTransform = SpyGlassInstance.transform;
                    IsAutoSpawned = true;

                    SpyGlassTransform.position = CameraTransform.position + CameraTransform.TransformVector(HeldOffset);
                    SpyGlassTransform.rotation = CameraTransform.rotation;
                    SpyGlassTransform.localScale = Vector3.one;

                    SpyGlassInstance.SetActive(UsingSpyGlass);
                }
                else if (SpyGlassInstance != null)
                {
                    SpyGlassInstance.SetActive(UsingSpyGlass);
                }
            }
            else
            {
                if (IsAutoSpawned && SpyGlassInstance != null)
                {
                    Destroy(SpyGlassInstance);
                }
                SpyGlassInstance = null;
                SpyGlassTransform = null;
                IsAutoSpawned = false;
                InUseRotationEuler = Vector3.zero;
            }
        }

        public void RotateInUse(float yawDegrees, float pitchDegrees)
        {
            InUseRotationEuler.x += yawDegrees;
            InUseRotationEuler.y = Mathf.Clamp(InUseRotationEuler.y + pitchDegrees, -45f, 45f);
        }

        public void RotateInUse(float yawDegrees, float pitchDegrees, float rollDegrees)
        {
            InUseRotationEuler.x += yawDegrees;
            InUseRotationEuler.y = Mathf.Clamp(InUseRotationEuler.y + pitchDegrees, -45f, 45f);
            InUseRotationEuler.z += rollDegrees;
        }
    }
}