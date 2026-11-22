using UnityEngine;
using System.Collections;
using Unrealtor.Player;
using UnityEngine.UI;
using System;
//hi diego
public class PlayerPickup2 : MonoBehaviour
{
    [Header("Pickup Settings")]
    public float pickupRange = 5f;

    [Header("Layer Masks")]
    public LayerMask itemLayerA;
    public LayerMask itemLayerB;
    public LayerMask spyGlassLayer;
    public LayerMask placementZoneLayer;

    [Header("Camera")]
    public Camera playerCamera;

    [Header("SpyGlass Control")]
    [SerializeField] private SpyGlassController spyGlassController;
    [Header("Glass Orb Control")]
    [SerializeField] private GlassOrb glassOrb;
    [Header("Pocket Mirror Control")]
    [SerializeField] private PocketMirrorController pocketMirror;
    [Header("Square Mirror Control")]
    [SerializeField] private SquareMirror squareMirror;
    [Header("SpyGlass Identification (fallbacks)")]
    public string spyGlassTag = "SpyGlass";

    private GameObject selectedItem = null;
    private bool selectedIsSpyGlass = false;
    private bool selectedIsGlassOrb = false;
    private bool selectedIsPocketMirror = false;
    private Transform selectedOriginalParent;
    private Vector3 selectedOriginalPosition;
    private Quaternion selectedOriginalRotation;
    private Vector3 selectedOriginalScale;

    private enum ItemType { None, TypeA, TypeB, SpyGlass }

    void Awake()
    {
        if (spyGlassController == null)
        {
            spyGlassController = FindObjectOfType<SpyGlassController>();
        }
        if (playerCamera == null)
        {
            var mainCam = Camera.main;
            if (mainCam != null) playerCamera = mainCam;
        }
        if (glassOrb == null)
        {
            glassOrb = FindObjectOfType<GlassOrb>();
        }
        if (pocketMirror == null)
        {
            pocketMirror = FindObjectOfType<PocketMirrorController>();
        }
        if (squareMirror == null)
        {
            squareMirror = FindObjectOfType<SquareMirror>();
        }
    }

    void OnDisable()
    {
        RestoreSelectionAndOverlays();
    }

    void OnApplicationQuit()
    {
        RestoreSelectionAndOverlays();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (selectedItem == null)
            {
                if (TryFindSpyGlassTarget(out GameObject spyglassTarget))
                {
                    PickupSelected(spyglassTarget);
                }
                else if (TryFindPickupTarget(out GameObject target))
                {
                    PickupSelected(target);
                }
            }
            else
            {
                GameObject newTarget = null;
                GameObject newSpy = null;
                bool hasTarget = TryFindPickupTarget(out newTarget);
                bool hasSpy = !hasTarget && TryFindSpyGlassTarget(out newSpy);
                if (hasTarget || hasSpy)
                {
                    GameObject target = hasTarget ? newTarget : newSpy;
                    if (target != null)
                    {
                        DropSelected();
                        PickupSelected(target);
                    }
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.G))
        {
            DropSelected();
        }
        if (selectedItem != null && Input.GetKeyDown(KeyCode.R))
        {
            float step = 90f;
            bool holdingCtrlOrCmd =
                Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
                Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);

            if (selectedIsSpyGlass)
            {
                if (holdingCtrlOrCmd && spyGlassController != null)
                {
                    spyGlassController.RotateInUse(0f, 0f, step);
                }
            }
            else
            {
                selectedOriginalRotation = Quaternion.Euler(0f, step, 0f) * selectedOriginalRotation;
            }
        }

        Debug.DrawRay(playerCamera.transform.position, playerCamera.transform.forward * pickupRange, Color.red);
    }

    bool TryGetRaycastHit(out RaycastHit hit)
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        int combinedMask = itemLayerA | itemLayerB | spyGlassLayer;
        return Physics.Raycast(ray, out hit, pickupRange, combinedMask);
    }

    bool TryGetRaycastHit(LayerMask mask, out RaycastHit hit)
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        return Physics.Raycast(ray, out hit, pickupRange, mask);
    }

    bool TryFindSpyGlassTarget(out GameObject target)
    {
        target = null;
        if (playerCamera == null)
        {
            return false;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (TryGetRaycastHit(spyGlassLayer, out RaycastHit layerHit))
        {
            target = GetSpyGlassRoot(layerHit.collider.gameObject);
            if (target != null) return true;
        }

        RaycastHit[] hits = Physics.RaycastAll(ray, pickupRange);
        for (int i = 0; i < hits.Length; i++)
        {
            GameObject go = hits[i].collider.gameObject;
            if (IsOnLayer(go, spyGlassLayer) || (!string.IsNullOrEmpty(spyGlassTag) && go.CompareTag(spyGlassTag)) || go.name.Contains("SpyGlass"))
            {
                target = GetSpyGlassRoot(go);
                if (target != null) return true;
            }
        }
        return false;
    }

    bool TryFindPickupTarget(out GameObject target)
    {
        target = null;
        if (TryGetRaycastHit(out RaycastHit hit))
        {
            target = hit.collider.gameObject;
            if (IsSpyGlassObject(target)) target = GetSpyGlassRoot(target);
            return true;
        }
        return false;
    }

    GameObject GetSpyGlassRoot(GameObject child)
    {
        Transform t = child.transform;
        while (t != null)
        {
            if (t.name.Contains("SpyGlass")) return t.gameObject;
            t = t.parent;
        }
        return child;
    }

    void PickupSelected(GameObject item)
    {
        selectedItem = item;
        selectedOriginalParent = item.transform.parent;
        selectedOriginalPosition = item.transform.position;
        selectedOriginalRotation = item.transform.rotation;
        selectedOriginalScale = item.transform.localScale;

        selectedIsSpyGlass = IsSpyGlassObject(item);
        selectedIsGlassOrb = IsGlassOrbObject(item);
        selectedIsPocketMirror = IsPocketMirrorObject(item);
        if (selectedIsSpyGlass && spyGlassController != null)
        {
            spyGlassController.enabled = true;
            spyGlassController.SetAutoSpawnOnStart(true);
            if (pocketMirror != null) pocketMirror.gameObject.SetActive(false);
            if (squareMirror != null) squareMirror.SetActive(false);
        }
        else if (selectedIsGlassOrb)
        {
            if (glassOrb != null)
            {
                glassOrb.enabled = true;
                glassOrb.SetActive(true);
            }

            if (pocketMirror != null) pocketMirror.gameObject.SetActive(false);
            if (squareMirror != null) squareMirror.SetActive(false);
        }
        else if (selectedIsPocketMirror)
        {
            if (pocketMirror == null)
            {
                pocketMirror = FindObjectOfType<PocketMirrorController>();
            }
            if (pocketMirror != null)
            {
                pocketMirror.gameObject.SetActive(true);
                pocketMirror.enabled = true;
                if (squareMirror != null) squareMirror.SetActive(true);
                ToggleSimpleMirror(true);
            }

            if (glassOrb != null)
            {
                glassOrb.SetActive(false);
            }
            if (spyGlassController != null)
            {
                spyGlassController.SetAutoSpawnOnStart(false);
                spyGlassController.enabled = false;
            }
            ClearSimpleMirror();
        }
        else
        {
            if (pocketMirror != null)
            {
                pocketMirror.enabled = false;
                pocketMirror.gameObject.SetActive(false);
            }

            if (squareMirror != null) squareMirror.SetActive(false);
            ToggleSimpleMirror(false);
            ClearSimpleMirror();
        }


        item.SetActive(false);
    }

    void DropSelected()
    {
        if (selectedItem == null) return;

        if (selectedIsSpyGlass && spyGlassController != null)
        {
            spyGlassController.SetAutoSpawnOnStart(false);
            spyGlassController.enabled = false;
            if (glassOrb != null) glassOrb.SetActive(false);
        }
        else if (selectedIsGlassOrb)
        {
            if (glassOrb != null) glassOrb.SetActive(false);
        }
        else if (selectedIsPocketMirror)
        {
            if (pocketMirror != null)
            {
                pocketMirror.enabled = false;
                pocketMirror.gameObject.SetActive(false);
            }
        }

        selectedItem.transform.SetParent(null);
        selectedItem.transform.localScale = selectedOriginalScale;
        selectedItem.SetActive(true);

        Vector3 dropPos = GetGroundDropPosition();
        selectedItem.transform.position = dropPos;
        selectedItem.transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        Rigidbody rb = selectedItem.GetComponent<Rigidbody>();
        if (rb == null) rb = selectedItem.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;


        selectedItem = null;
        selectedIsSpyGlass = false;
        selectedOriginalParent = null;
    }

    void RestoreSelectionAndOverlays()
    {
        if (selectedItem != null)
        {

            selectedItem.transform.SetParent(selectedOriginalParent);
            selectedItem.transform.position = selectedOriginalPosition;
            selectedItem.transform.rotation = selectedOriginalRotation;
            selectedItem.transform.localScale = selectedOriginalScale;
            selectedItem.SetActive(true);
        }

        if (spyGlassController != null)
        {
            spyGlassController.SetAutoSpawnOnStart(false);
            spyGlassController.enabled = false;
        }
        if (glassOrb != null)
        {
            glassOrb.SetActive(false);
        }
        if (pocketMirror != null)
        {
            pocketMirror.enabled = false;
            pocketMirror.gameObject.SetActive(false);
        }
    }

    bool IsOnLayer(GameObject obj, LayerMask mask)
    {
        return (mask.value & (1 << obj.layer)) != 0;
    }

    bool IsSpyGlassObject(GameObject obj)
    {
        if (obj == null) return false;
        if (IsOnLayer(obj, spyGlassLayer)) return true;
        if (!string.IsNullOrEmpty(spyGlassTag) && obj.CompareTag(spyGlassTag)) return true;
        Transform t = obj.transform;
        while (t != null)
        {
            if (t.name.Contains("SpyGlass")) return true;
            t = t.parent;
        }
        return false;
    }

    bool IsGlassOrbObject(GameObject obj)
    {
        if (obj == null) return false;
        Transform t = obj.transform;
        while (t != null)
        {
            if (t.name == "GlassOrb") return true;
            t = t.parent;
        }
        return false;
    }

    bool IsPocketMirrorObject(GameObject obj)
    {
        if (obj == null) return false;
        Transform t = obj.transform;
        while (t != null)
        {
            if (t.name == "PocketMirror") return true;
            t = t.parent;
        }
        return false;
    }

    Vector3 GetGroundDropPosition()
    {
        Transform originT = playerCamera != null ? playerCamera.transform : transform;
        Vector3 start = originT.position + originT.forward * 1.0f + Vector3.up * 1.0f;
        if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, 10f))
        {
            return hit.point + Vector3.up * 0.1f;
        }
        return originT.position + originT.forward * 1.0f;
    }

    void ToggleSimpleMirror(bool active)
    {
        var simpleMirror = FindObjectOfType<SimpleMirrorSetup>();
        if (simpleMirror == null) return;
        if (simpleMirror.mirrorCamera != null)
        {
            simpleMirror.mirrorCamera.enabled = active;
        }
        if (simpleMirror.mirrorSurface != null)
        {
            simpleMirror.mirrorSurface.enabled = active;
        }
    }

    void ClearSimpleMirror()
    {
        var simpleMirror = FindObjectOfType<SimpleMirrorSetup>();
        if (simpleMirror == null) return;
        simpleMirror.ClearAndDisable();
    }
}
