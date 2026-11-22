using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EdgarCharacterController : MonoBehaviour
{
    [Header("Components")]
    public NavMeshAgent agent;
    public Animator animator;

    [Header("Movement Parameters")]
    public float arrivalThreshold = 0.2f;
    [Header("Animation Parameters")]
    [SerializeField] float idleSpeedThreshold = 0.05f;
    [SerializeField] float maxAnimatorSpeed = 2f; // matches player blend tree (0=idle,1=walk,2=run)
    [SerializeField] float speedBlendLerp = 10f;
    [Header("Hide Animation")]
    [SerializeField] float hideDuration = 1.2f;
    [SerializeField] float hideSinkDistance = 2f;
    [SerializeField] float hideSpinSpeed = 180f;

    EdgarPoint currentTarget;
    bool isMoving;
    float currentBlendSpeed;
    Coroutine hideRoutine;

    public bool IsMoving
    {
        get { return isMoving; }
    }

    public Action<EdgarPoint> OnReachedPoint; // Manager can subscribe to this

    void Reset()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        UpdateAnimatorFromAgent();

        // Stop any hide animation when moving again
        if (isMoving && hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        if (isMoving && currentTarget != null && !agent.pathPending)
        {
            if (agent.remainingDistance <= arrivalThreshold)
            {
                StopMovement();
                if (OnReachedPoint != null)
                {
                    OnReachedPoint.Invoke(currentTarget);
                }
            }
        }
    }

    public void MoveTo(EdgarPoint targetPoint)
    {
        if (targetPoint == null) return;

        currentTarget = targetPoint;
        isMoving = true;

        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        agent.isStopped = false;
        agent.SetDestination(targetPoint.transform.position);
        // Animator will follow agent velocity automatically
    }

    public void TeleportTo(EdgarPoint targetPoint)
    {
        if (targetPoint == null) return;

        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        agent.Warp(targetPoint.transform.position);
        currentTarget = targetPoint;
        StopMovement();
    }

    void StopMovement()
    {
        isMoving = false;

        if (agent != null)
        {
            agent.isStopped = true;
        }

        UpdateAnimator(0f, agent != null ? agent.speed : 1f);
    }

    void UpdateAnimatorFromAgent()
    {
        if (animator == null || agent == null)
            return;

        float moveSpeed = isMoving ? agent.velocity.magnitude : 0f;
        UpdateAnimator(moveSpeed, agent.speed);
    }

    void UpdateAnimator(float moveSpeed, float maxSpeed)
    {
        float normalizedSpeed = maxSpeed > 0.01f ? Mathf.Clamp01(moveSpeed / maxSpeed) : 0f;
        float targetBlend = Mathf.Lerp(0f, maxAnimatorSpeed, normalizedSpeed);

        currentBlendSpeed = speedBlendLerp > 0f
            ? Mathf.Lerp(currentBlendSpeed, targetBlend, Time.deltaTime * speedBlendLerp)
            : targetBlend;

        animator.SetFloat("Speed", currentBlendSpeed);
        animator.SetBool("Grounded", true);
        animator.SetBool("IsWalking", moveSpeed > idleSpeedThreshold);
    }

    public IEnumerator HideAndSink()
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
        }
        hideRoutine = StartCoroutine(HideRoutine());
        yield return hideRoutine;
    }

    IEnumerator HideRoutine()
    {
        isMoving = false;
        if (agent != null)
        {
            agent.isStopped = true;
        }

        if (animator != null)
        {
            animator.SetTrigger("Sink");
        }

        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + Vector3.down * hideSinkDistance;
        float elapsed = 0f;

        while (elapsed < hideDuration)
        {
            float t = hideDuration > 0.001f ? elapsed / hideDuration : 1f;
            transform.position = Vector3.Lerp(startPos, endPos, t);
            transform.Rotate(Vector3.up, hideSpinSpeed * Time.deltaTime, Space.World);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = endPos;
        hideRoutine = null;
    }

    // ====== Animation helper methods (adjust based on your Animator parameters) ======

    public void SetTalking(bool talking)
    {
        if (animator != null)
        {
            animator.SetBool("IsTalking", talking);
        }
    }

    public void PlayPunch()
    {
        if (animator != null)
        {
            animator.SetTrigger("Punch");
        }
    }

    public void PlaySpecialPose()
    {
        if (animator != null)
        {
            animator.SetTrigger("SpecialPose");
        }
    }

    public void PlaySink()
    {
        if (animator != null)
        {
            animator.SetTrigger("Sink");
        }
    }

    public void PlayAnimation(string triggerName)
    {
        if (animator != null && !string.IsNullOrEmpty(triggerName))
        {
            animator.SetTrigger(triggerName);
        }
    }

    public void ShowFullBody()
    {
        // Enable full body model, disable face-only model if separate
        gameObject.SetActive(true);
        // TODO: If you have separate prefabs/models, switch them here
    }

    public void ShowFaceOnly()
    {
        // Enable face-only model, disable full body if separate
        gameObject.SetActive(true);
        // TODO: If you have separate prefabs/models, switch them here
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void RotateToDirection(float yRotationDegrees)
    {
        if (transform != null)
        {
            Vector3 eulerAngles = transform.eulerAngles;
            eulerAngles.y = yRotationDegrees;
            transform.rotation = Quaternion.Euler(eulerAngles);
        }
    }

    public void LookAt(Transform target)
    {
        if (transform != null && target != null)
        {
            Vector3 direction = target.position - transform.position;
            direction.y = 0f; // Keep rotation on horizontal plane
            if (direction.magnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }
    }

    public void LookAt(Vector3 targetPosition)
    {
        if (transform != null)
        {
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f; // Keep rotation on horizontal plane
            if (direction.magnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }
    }
}


