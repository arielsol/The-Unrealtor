using System;
using UnityEngine;

public class ExtinguisherFragment : MonoBehaviour
{
    public enum IdleMotionType
    {
        None,
        VerticalOscillation,
        FollowLeaderFigureEight
    }

    [Header("Targets")]
    [SerializeField] private Transform assembledAnchor;
    [SerializeField] private Transform idleOrigin;

    [Header("Idle Motion")]
    [SerializeField] private IdleMotionType idleMotionType = IdleMotionType.VerticalOscillation;
    [SerializeField] private float oscillationAmplitude = 0.25f;
    [SerializeField] private float oscillationSpeed = 1.25f;

    [SerializeField] private Transform leader;
    [SerializeField] private float followDelaySeconds = 0.15f;
    [SerializeField] private float figureEightRadiusX = 0.4f;
    [SerializeField] private float figureEightRadiusY = 0.25f;
    [SerializeField] private float figureEightSpeed = 0.9f;

    [Header("Assemble Motion")]
    [SerializeField] private float assembleMoveSpeed = 4.0f;
    [SerializeField] private float assembleRotateSpeed = 270.0f;

    private Vector3 initialIdleLocalPosition;
    private Quaternion initialIdleLocalRotation;
    private float startTime;
    private bool isAssembling;

    void Awake()
    {
        startTime = Time.time;
        if (idleOrigin == null)
        {
            idleOrigin = transform.parent != null ? transform.parent : transform;
        }
        initialIdleLocalPosition = transform.localPosition;
        initialIdleLocalRotation = transform.localRotation;
    }

    void Update()
    {
        if (isAssembling)
        {
            MoveTowardsAssembled();
            return;
        }

        switch (idleMotionType)
        {
            case IdleMotionType.None:
                break;
            case IdleMotionType.VerticalOscillation:
                ApplyVerticalOscillation();
                break;
            case IdleMotionType.FollowLeaderFigureEight:
                ApplyFigureEightFollow();
                break;
        }
    }

    public void BeginAssemble()
    {
        isAssembling = true;
    }

    public void EndAssemble()
    {
        isAssembling = false;
    }

    public void SetAssembledAnchor(Transform target)
    {
        assembledAnchor = target;
    }

    private void MoveTowardsAssembled()
    {
        if (assembledAnchor == null) return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            assembledAnchor.position,
            assembleMoveSpeed * Time.deltaTime);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            assembledAnchor.rotation,
            assembleRotateSpeed * Time.deltaTime);
    }

    private void ApplyVerticalOscillation()
    {
        if (idleOrigin == null) return;
        float t = (Time.time - startTime) * oscillationSpeed;
        Vector3 baseLocal = initialIdleLocalPosition;
        float yOffset = Mathf.Sin(t) * oscillationAmplitude;
        transform.localPosition = baseLocal + new Vector3(0f, yOffset, 0f);
        transform.localRotation = initialIdleLocalRotation;
    }

    private void ApplyFigureEightFollow()
    {
        if (leader == null && idleOrigin == null) return;

        float timeOffset = followDelaySeconds;
        float t = (Time.time - startTime - timeOffset) * figureEightSpeed;
        float x = figureEightRadiusX * Mathf.Sin(t);
        float y = figureEightRadiusY * Mathf.Sin(2f * t);

        Vector3 center = leader != null ? leader.position : idleOrigin.position;
        Vector3 targetWorld = center + new Vector3(x, y, 0f);
        transform.position = targetWorld;
    }
}


