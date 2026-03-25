using UnityEngine;

namespace BountyHunter.Physics
{
    /// <summary>
    /// 漂移系统
    ///
    /// 设计参考：
    /// - 《QQ飞车》漂移充能 → 释放给 Nitro 加速（三段蓄力）
    /// - 《极品飞车：集结》漂移更偏写实，角度控制是关键
    ///
    /// 本实现采用类QQ飞车的"漂移蓄力"机制，提升爽快感
    /// </summary>
    public class DriftSystem : MonoBehaviour
    {
        [Header("Drift Parameters")]
        [Tooltip("漂移时的侧向摩擦系数（越低越滑）")]
        public float DriftLateralGrip    = 0.3f;
        [Tooltip("漂移最小触发速度")]
        public float MinDriftSpeed       = 5f;
        [Tooltip("漂移结束后 Boost 持续时间")]
        public float BoostDuration       = 1.5f;
        [Tooltip("Boost 额外推力")]
        public float BoostForce          = 800f;

        [Header("Charge Thresholds (QQ飞车三段蓄力)")]
        public float ChargeLevel1        = 0.5f;   // 蓝焰
        public float ChargeLevel2        = 1.2f;   // 橙焰
        public float ChargeLevel3        = 2.5f;   // 粉焰（完美漂移）

        // 状态
        public bool  IsDrifting          { get; private set; }
        public float ChargeTime          { get; private set; }
        public int   ChargeLevel         { get; private set; }   // 0/1/2/3
        public bool  IsBoosting          { get; private set; }

        private float _boostTimer;
        private Rigidbody _rb;

        private void Awake() => _rb = GetComponent<Rigidbody>();

        /// <summary>
        /// 每 FixedUpdate 调用，传入当前速度和玩家按键
        /// </summary>
        public void Tick(float speed, bool driftHeld)
        {
            if (driftHeld && speed > MinDriftSpeed)
            {
                if (!IsDrifting) BeginDrift();
                ChargeTime += Time.fixedDeltaTime;
                UpdateChargeLevel();
            }
            else if (IsDrifting)
            {
                EndDrift();
            }

            if (IsBoosting)
            {
                _boostTimer -= Time.fixedDeltaTime;
                _rb.AddForce(transform.forward * BoostForce, ForceMode.Force);
                if (_boostTimer <= 0f) IsBoosting = false;
            }
        }

        public float GetCurrentLateralGrip() => IsDrifting ? DriftLateralGrip : 1f;

        private void BeginDrift()
        {
            IsDrifting  = true;
            ChargeTime  = 0f;
            ChargeLevel = 0;
        }

        private void EndDrift()
        {
            IsDrifting = false;

            // 根据蓄力等级触发 Boost
            if (ChargeLevel > 0)
            {
                IsBoosting  = true;
                _boostTimer = BoostDuration * ChargeLevel;
                Debug.Log($"[Drift] Boost triggered! Level {ChargeLevel}");
            }

            ChargeTime  = 0f;
            ChargeLevel = 0;
        }

        private void UpdateChargeLevel()
        {
            if      (ChargeTime >= ChargeLevel3) ChargeLevel = 3;
            else if (ChargeTime >= ChargeLevel2) ChargeLevel = 2;
            else if (ChargeTime >= ChargeLevel1) ChargeLevel = 1;
            else                                  ChargeLevel = 0;
        }
    }
}
