using UnityEngine;

public class FountainSpawner : MonoBehaviour
{
    [Header("Prefab & Size")]
    public GameObject prefab;                  // 要喷出的可选择物体（建议预制体自带 Rigidbody + Collider）
    [Tooltip("固定缩放；若 <=0 则使用 prefab 自身缩放")]
    public float uniformScale = 1.0f;          // 统一缩放
    [Tooltip("随机缩放附加范围，比如 0.3 表示在 [scale*(1-0.3), scale*(1+0.3)]")]
    public float randomScaleFraction = 0.25f;  // 尺寸随机幅度

    [Header("Burst")]
    public int count = 12;                     // 一次喷出多少个
    public float baseForce = 8f;               // 基础力度（m/s 量级，实际转成 AddForce Impulse）
    public float upwardBias = 1.2f;            // 向上偏置（>1 更偏上）
    [Range(0f, 90f)] public float spreadAngleDeg = 35f; // 扇形扩散角（与竖直方向的夹角）
    public float randomForceFraction = 0.35f;  // 力度随机比重（0~1）

    [Header("Physics")]
    public float torqueImpulse = 2.5f;         // 扭矩强度
    public float lifeTime = 8f;                // 多少秒后自动销毁（<=0 不销毁）
    public LayerMask ignoreCollisionWith;      // 可选：忽略与某些层的碰撞

    public void BurstAt(Vector3 origin)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[FountainSpawner] Prefab is null.");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(prefab, origin, Random.rotation);
            // 尺寸
            if (uniformScale > 0f)
            {
                float rand = 1f + Random.Range(-randomScaleFraction, randomScaleFraction);
                go.transform.localScale = Vector3.one * uniformScale * rand;
            }

            // 物理组件兜底
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // 忽略与指定层碰撞（可选）
            if (ignoreCollisionWith.value != 0)
            {
                int objLayer = go.layer;
                for (int layer = 0; layer < 32; layer++)
                {
                    if ((ignoreCollisionWith.value & (1 << layer)) != 0)
                        Physics.IgnoreLayerCollision(objLayer, layer, true);
                }
            }

            // 方向：围绕“竖直向上”在一个圆锥内随机
            Vector3 forward = transform.forward;

            // 在 forward 周围随机一个角度，形成扇形/圆锥喷射
            Quaternion cone = Quaternion.AngleAxis(Random.Range(-spreadAngleDeg, spreadAngleDeg), Random.onUnitSphere);
            Vector3 dir = (cone * forward).normalized;

            // 如果仍然想加一点 upwardBias，可以这样（可选）：
            dir = (dir + Vector3.up * upwardBias).normalized;

            // 力度
            float force = baseForce * (1f + Random.Range(-randomForceFraction, randomForceFraction));
            rb.AddForce(dir * force, ForceMode.Impulse);

            // 扭矩（让物体有旋转飞溅感）
            Vector3 torque = Random.onUnitSphere * torqueImpulse;
            rb.AddTorque(torque, ForceMode.Impulse);

            // 生命期
            if (lifeTime > 0f) Destroy(go, lifeTime);
        }
    }
}
