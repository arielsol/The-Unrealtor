using UnityEngine;

using UnityEngine.InputSystem;



public class PlayerInteractor : MonoBehaviour

{

    [Header("Interaction Settings")]

    [SerializeField] private float _interactRange = 2f;

    [SerializeField] private LayerMask _interactableLayer;

    [Tooltip("Walls and static blockers. Leave at Nothing (0) to skip the occlusion pass; facing + 'can interact' still apply via InteractionBase.CanAcceptInteractAtProbe.")]
    [SerializeField] private LayerMask _lineOfSightBlockerLayer;



    [SerializeField] private BasePlayerController _playerController;

    private InputAction _interactAction;

    private IInteractable _currentTarget;

    private Collider _currentHitCollider;


    private void Awake()

    {


        if (_playerController == null)

        {

            Debug.LogError("PlayerInteractor requires a BasePlayerController component!");
            enabled = false;
            return;

        }

    }



    private void Start()

    {
        if (!enabled || _playerController == null) return;
        if (PlayerInputManager.Instance == null) return;
        TwoPlayerInput controls = _playerController.playerID == BasePlayerController.PlayerID.Player1
            ? PlayerInputManager.Instance.Player1Controls
            : PlayerInputManager.Instance.Player2Controls;



        controls.Gameplay.Enable();



        _interactAction = controls.Gameplay.PlayerInteract;

        _interactAction.Enable();

        _interactAction.performed += OnInteractPerformed;

        _interactAction.canceled += OnInteractCanceled;

    }



    private void OnEnable()

    {

        if (_interactAction != null)

        {

            //_interactAction.performed += OnInteractPerformed;

        }

    }



    private void OnDisable()

    {

        if (_interactAction != null)

        {

            _interactAction.performed -= OnInteractPerformed;

            _interactAction.canceled -= OnInteractCanceled;

        }



        ClearCurrent();

    }

    

    private void OnInteractCanceled(InputAction.CallbackContext context)

    {

        if (_playerController == null)

            return;

        if (!IsInteractFromThisPlayer(context))
            return;

        UIManager.Instance?.NotifyPickupPopupInteractReleased(_playerController.playerID);

    }



    private void OnInteractPerformed(InputAction.CallbackContext context)

    {
        if (!IsInteractFromThisPlayer(context))
            return;

        bool closedHint = PickUpObject.TryConsumeInteractToClosePickupPopup(_playerController.playerID);
        if (closedHint)
            return;

        if (UIManager.Instance != null && UIManager.Instance.IsPlayerPickupPopupLocked(_playerController.playerID))
            return;

        if (UIManager.Instance != null && UIManager.Instance.IsPickupWorldOpenSuppressedUntilInteractReleased(_playerController.playerID))
            return;

        if (_currentTarget != null)
        {
            MusicManager.Instance?.PlaySFX(SoundID.InteractibleClicked, Vector3.zero);
            _currentTarget.OnInteract(_playerController);
        }

    }

    private bool IsInteractFromThisPlayer(InputAction.CallbackContext context)
    {
        if (_playerController == null)
            return false;

        PlayerInputManager pim = PlayerInputManager.Instance;
        if (pim == null)
            return true;

        if (pim.CurrentControllerType == PlayerInputManager.ControllerType.SinglePlayer)
            return pim.CurrPlayerEnum == _playerController.playerID;

        InputControl triggeringControl = context.control;
        if (triggeringControl == null || triggeringControl.device == null)
            return true;

        InputDevice triggeringDevice = triggeringControl.device;
        InputDevice myDevice = _playerController.playerID == BasePlayerController.PlayerID.Player1
            ? pim.Player1Device
            : pim.Player2Device;

        if (myDevice == null)
            return true;

        return triggeringDevice == myDevice;
    }

    private void Update()

    { 

        DetectInteractable();

    }



    private void LateUpdate()

    { 

        // DetectInteractable();

    }



  



       private void DetectInteractable()

    {

        Vector3 probe = GetInteractProbeWorld();

        Collider[] hits = Physics.OverlapSphere(probe, _interactRange, _interactableLayer, QueryTriggerInteraction.Collide);

        IInteractable bestInteractable = null;

        Collider bestHitCollider = null;

        float bestDot = float.NegativeInfinity;

        float bestDistance = float.PositiveInfinity;



        foreach (Collider colliderHit in hits)

        {

            IInteractable interactable = colliderHit.GetComponentInParent<IInteractable>();
            if (interactable is not InteractionBase ibase)
                continue;

            // Only count targets the player is actually aimed at and allowed to use (facing, CanInteract, optional LOS).
            if (!ibase.CanAcceptInteractAtProbe(
                    _playerController, probe, _interactableLayer, _lineOfSightBlockerLayer, colliderHit))
                continue;
            
            Vector3 targetPoint = colliderHit.ClosestPoint(probe);

            Vector3 direction = (targetPoint - probe).normalized;

            float distance = Vector3.Distance(probe, targetPoint);

            Vector3 up = (-_playerController.GravityDirection).normalized;
            Vector3 dirPlanar = Vector3.ProjectOnPlane(direction, up);
            if (dirPlanar.sqrMagnitude < 1e-8f)
                continue;
            dirPlanar.Normalize();

            float dot = Vector3.Dot(_playerController.GetCameraPlanarForward(), dirPlanar);

            if (bestInteractable == null || dot > bestDot || (Mathf.Approximately(dot, bestDot) && distance < bestDistance))

            {

                bestInteractable = interactable;

                bestHitCollider = colliderHit;

                bestDot = dot;

                bestDistance = distance;

            }

        }



        if (_currentTarget != bestInteractable)
        {
            ClearCurrent();

            _currentTarget = bestInteractable;
            _currentHitCollider = bestHitCollider;
            if (_currentTarget is InteractionBase ib)
            {
                MusicManager.Instance?.PlaySFX(SoundID.InteractibleHovered, Vector3.zero);
                Debug.Log("New Target: " + bestHitCollider.name + " on object " + bestHitCollider.gameObject.name);
                ib.ShowPrompt(true, _playerController);
            }
        }
        else
        {
            if (_currentTarget is InteractionBase ib && bestInteractable != null)
            {
                _currentHitCollider = bestHitCollider;
                ib.ShowPrompt(true, _playerController);
            }
        }

    }



    private void ClearCurrent()

    {

        if (_currentTarget != null)

        {

            if (_currentTarget is InteractionBase ib)
            {
                ib.ShowPrompt(false, _playerController);
            }
                

        _currentTarget = null;

            _currentHitCollider = null;

        }

    }

    private Vector3 GetInteractProbeWorld()
    {
        if (_playerController == null) return transform.position;
        if (_playerController.Rigidbody != null)
            return _playerController.Rigidbody.position;
        return _playerController.transform.position;
    }

}



