using UnityEngine;
using System.Collections;

public class ObjectMove : MonoBehaviour
{
    public enum Axis { X, Y, Z }
    public Transform targetTransform;
    public Axis rotationAxis = Axis.Y;
    public float stepDegrees = 15f;
    public bool useAngleLimits = false;
    public float minAngle = 0f;
    public float maxAngle = 360f;
    public bool animate = true;
    public float duration = 0.15f;
    public AnimationCurve ease = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    Coroutine anim;

    void Awake()
    {
        if (targetTransform == null) targetTransform = transform;
    }

    public void RotateForward() { ApplyStep(Mathf.Abs(stepDegrees)); }
    public void RotateBackward() { ApplyStep(-Mathf.Abs(stepDegrees)); }

    void ApplyStep(float step)
    {
        float current = GetCurrentAngle();
        float target = Normalize(current + step);
        if (useAngleLimits) target = Mathf.Clamp(target, minAngle, maxAngle);
        SetAngle(target);
    }

    float GetCurrentAngle()
    {
        Vector3 e = targetTransform.localEulerAngles;
        return rotationAxis == Axis.X ? e.x : rotationAxis == Axis.Y ? e.y : e.z;
    }

    void SetAngle(float angle)
    {
        if (animate && duration > 0f)
        {
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(AnimateTo(angle, duration));
        }
        else
        {
            ApplyAngle(angle);
        }
    }

    void ApplyAngle(float angle)
    {
        Vector3 e = targetTransform.localEulerAngles;
        if (rotationAxis == Axis.X) e.x = angle;
        else if (rotationAxis == Axis.Y) e.y = angle;
        else e.z = angle;
        targetTransform.localEulerAngles = e;
    }

    IEnumerator AnimateTo(float targetAngle, float time)
    {
        float start = GetCurrentAngle();
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / time;
            float k = ease.Evaluate(Mathf.Clamp01(t));
            ApplyAngle(Mathf.LerpAngle(start, targetAngle, k));
            yield return null;
        }
        ApplyAngle(targetAngle);
        anim = null;
    }

    static float Normalize(float a)
    {
        a %= 360f; if (a < 0f) a += 360f; return a;
    }
}
