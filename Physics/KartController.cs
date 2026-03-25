using UnityEngine;
using BountyHunter.Shared;

namespace BountyHunter.Physics
{
    /// <summary>
    /// 车辆主控制器 —— 替换 Karting Microgame 原版 KartController
    ///
    /// 技术对比：
    /// 《极品飞车：集结》：WheelCollider + 写实 Pacejka 参数，强调油门控制和真实重量感
    /// 《QQ飞车》        ：夸张侧向抓地，漂移易触发，物理服务于爽快感
    /// 《马里奥赛车》    ：几乎纯运动学，完全服务于游戏性，无真实物理
    ///
    /// 本实现：基于 Rigidbody + Pacejka 轮胎模型，可通过参数在"写实↔夸张"间调节
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(DriftSystem))]
    public class KartController : MonoBehaviour
    {
        [Header("Engine")]
        public float MaxMotorTorque  = 1500f;   // 最大驱动扭矩 (Nm)
        public float MaxBrakeTorque  = 3000f;
        public AnimationCurve TorqueCurve;       // 转速-扭矩曲线

        [Header("Steering")]
        public float MaxSteerAngle   = 28f;      // 最大前轮转角
        public float SteerSpeed      = 5f;       // 转向响应速度
        [Tooltip("速度越高，转向角自动减小（高速稳定性）")]
        public AnimationCurve SteerAngleBySpeed;

        [Header("Wheels")]
        public WheelCollider WheelFL, WheelFR;   // 前轮（转向）
        public WheelCollider WheelRL, WheelRR;   // 后轮（驱动）

        [Header("Tire Model")]
        public TireModel NormalTire  = new();
        [Tooltip("控制写实 vs 夸张的总体调节，1=写实(NFS)，0=夸张(QQ飞车)")]
        [Range(0f, 1f)]
        public float RealismFactor   = 0.7f;

        [Header("Anti-Roll")]
        public float AntiRollForce   = 5000f;

        // 运行时状态
        private Rigidbody  _rb;
        private DriftSystem _drift;
        private float      _currentSteer;
        private float      _speedKph;

        private void Awake()
        {
            _rb    = GetComponent<Rigidbody>();
            _drift = GetComponent<DriftSystem>();
            _rb.centerOfMass = new Vector3(0, -0.3f, 0.1f); // 降低重心
        }

        private void FixedUpdate()
        {
            // 由网络层或本地 InputHandler 传入 —— 此处为本地占位
            var input = new KartInput
            {
                Steering  = Input.GetAxis("Horizontal"),
                Throttle  = Mathf.Max(0, Input.GetAxis("Vertical")),
                Brake     = Mathf.Max(0, -Input.GetAxis("Vertical")),
                DriftHeld = Input.GetKey(KeyCode.Space)
            };

            ApplyInput(input);
        }

        /// <summary>
        /// 核心驱动入口，供本地输入和网络层统一调用
        /// </summary>
        public void ApplyInput(KartInput input)
        {
            _speedKph = _rb.velocity.magnitude * 3.6f;

            _drift.Tick(_speedKph / 3.6f, input.DriftHeld);

            ApplySteering(input.Steering, input.DriftHeld);
            ApplyDrive(input.Throttle, input.Brake);
            ApplyTireForces();
            ApplyAntiRoll();
        }

        // ─── 转向 ──────────────────────────────────────────────────────────────

        private void ApplySteering(float steerInput, bool drifting)
        {
            float speedFactor   = SteerAngleBySpeed != null
                ? SteerAngleBySpeed.Evaluate(_speedKph)
                : Mathf.Clamp01(1f - _speedKph / 200f);

            float targetAngle   = steerInput * MaxSteerAngle * speedFactor;

            // 漂移时增加额外转向响应（模拟反打方向盘）
            if (drifting) targetAngle *= 1.3f;

            _currentSteer = Mathf.Lerp(_currentSteer, targetAngle, Time.fixedDeltaTime * SteerSpeed);

            WheelFL.steerAngle = _currentSteer;
            WheelFR.steerAngle = _currentSteer;
        }

        // ─── 驱动 / 刹车 ──────────────────────────────────────────────────────

        private void ApplyDrive(float throttle, float brake)
        {
            float torque = MaxMotorTorque * throttle;
            if (TorqueCurve != null)
                torque *= TorqueCurve.Evaluate(_speedKph / 200f);

            WheelRL.motorTorque = torque;
            WheelRR.motorTorque = torque;

            float brakeTorque = MaxBrakeTorque * brake;
            WheelFL.brakeTorque = brakeTorque;
            WheelFR.brakeTorque = brakeTorque;
            WheelRL.brakeTorque = brakeTorque * 0.7f; // 前重后轻的刹车分配
            WheelRR.brakeTorque = brakeTorque * 0.7f;
        }

        // ─── Pacejka 侧向力修正 ────────────────────────────────────────────────

        private void ApplyTireForces()
        {
            float gripScale = _drift.GetCurrentLateralGrip();

            // 写实因子：0=完全夸张(QQ飞车)，1=写实(NFS)
            // 通过混合两种轮胎参数实现平滑过渡
            float effectiveD = Mathf.Lerp(NormalTire.D * 2f, NormalTire.D, RealismFactor);

            foreach (var wheel in new[] { WheelFL, WheelFR, WheelRL, WheelRR })
            {
                wheel.GetGroundHit(out WheelHit hit);
                if (!wheel.isGrounded) continue;

                // 滑移角 = arctan(侧向速度 / 纵向速度)
                float lateralVel    = Vector3.Dot(_rb.velocity, transform.right);
                float longitudinalVel = Mathf.Max(1f, Mathf.Abs(Vector3.Dot(_rb.velocity, transform.forward)));
                float slipAngle     = Mathf.Atan2(lateralVel, longitudinalVel);

                float lateralForce  = NormalTire.LateralForceCoeff(slipAngle)
                                      * effectiveD * wheel.sprungMass * 9.81f * gripScale;

                // 施加侧向修正力（WheelCollider 默认侧向力不够准确）
                _rb.AddForceAtPosition(
                    -transform.right * lateralForce,
                    wheel.transform.position,
                    ForceMode.Force
                );
            }
        }

        // ─── 防侧翻 ──────────────────────────────────────────────────────────

        private void ApplyAntiRoll()
        {
            ApplyAntiRollBar(WheelFL, WheelFR);
            ApplyAntiRollBar(WheelRL, WheelRR);
        }

        private void ApplyAntiRollBar(WheelCollider left, WheelCollider right)
        {
            left.GetGroundHit(out _);
            right.GetGroundHit(out _);

            float travelL = left.isGrounded
                ? (-left.transform.InverseTransformPoint(left.transform.position).y - left.radius) / left.suspensionDistance
                : 1f;
            float travelR = right.isGrounded
                ? (-right.transform.InverseTransformPoint(right.transform.position).y - right.radius) / right.suspensionDistance
                : 1f;

            float force = (travelL - travelR) * AntiRollForce;

            if (left.isGrounded)  _rb.AddForceAtPosition(left.transform.up  * -force, left.transform.position);
            if (right.isGrounded) _rb.AddForceAtPosition(right.transform.up *  force, right.transform.position);
        }
    }
}
