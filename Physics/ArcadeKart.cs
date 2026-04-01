using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.VFX;

namespace KartGame.KartSystems
{
    public class ArcadeKart : MonoBehaviour
    {
        [System.Serializable]
        public class StatPowerup
        {
            public ArcadeKart.Stats modifiers;
            public string PowerUpID;
            public float ElapsedTime;
            public float MaxTime;
        }

        [System.Serializable]
        public struct Stats
        {
            [Header("Movement Settings")]
            [Min(0.001f), Tooltip("Top speed attainable when moving forward.")]
            public float TopSpeed;

            [Tooltip("How quickly the kart reaches top speed.")]
            public float Acceleration;

            [Min(0.001f), Tooltip("Top speed attainable when moving backward.")]
            public float ReverseSpeed;

            [Tooltip("How quickly the kart reaches top speed, when moving backward.")]
            public float ReverseAcceleration;

            [Tooltip("How quickly the kart starts accelerating from 0. A higher number means it accelerates faster sooner.")]
            [Range(0.2f, 1)]
            public float AccelerationCurve;

            [Tooltip("How quickly the kart slows down when the brake is applied.")]
            public float Braking;

            [Tooltip("How quickly the kart will reach a full stop when no inputs are made.")]
            public float CoastingDrag;

            [Range(0.0f, 1.0f)]
            [Tooltip("The amount of side-to-side friction.")]
            public float Grip;

            [Tooltip("How tightly the kart can turn left or right.")]
            public float Steer;

            [Tooltip("Additional gravity for when the kart is in the air.")]
            public float AddedGravity;

            // allow for stat adding for powerups.
            public static Stats operator +(Stats a, Stats b)
            {
                return new Stats
                {
                    Acceleration        = a.Acceleration + b.Acceleration,
                    AccelerationCurve   = a.AccelerationCurve + b.AccelerationCurve,
                    Braking             = a.Braking + b.Braking,
                    CoastingDrag        = a.CoastingDrag + b.CoastingDrag,
                    AddedGravity        = a.AddedGravity + b.AddedGravity,
                    Grip                = a.Grip + b.Grip,
                    ReverseAcceleration = a.ReverseAcceleration + b.ReverseAcceleration,
                    ReverseSpeed        = a.ReverseSpeed + b.ReverseSpeed,
                    TopSpeed            = a.TopSpeed + b.TopSpeed,
                    Steer               = a.Steer + b.Steer,
                };
            }
        }

        public Rigidbody Rigidbody { get; private set; }
        public InputData Input     { get; private set; }
        public float AirPercent    { get; private set; }
        public float GroundPercent { get; private set; }

        public ArcadeKart.Stats baseStats = new ArcadeKart.Stats
        {
            TopSpeed            = 10f,
            Acceleration        = 5f,
            AccelerationCurve   = 4f,
            Braking             = 10f,
            ReverseAcceleration = 5f,
            ReverseSpeed        = 5f,
            Steer               = 5f,
            CoastingDrag        = 4f,
            Grip                = .95f,
            AddedGravity        = 1f,
        };

        [Header("Vehicle Visual")] 
        public List<GameObject> m_VisualWheels;

        [Header("Vehicle Physics")]
        [Tooltip("The transform that determines the position of the kart's mass.")]
        public Transform CenterOfMass;

        [Range(0.0f, 20.0f), Tooltip("Coefficient used to reorient the kart in the air. The higher the number, the faster the kart will readjust itself along the horizontal plane.")]
        public float AirborneReorientationCoefficient = 3.0f;

        [Header("Drifting")]
        [Range(0.01f, 1.0f), Tooltip("The grip value when drifting.")]
        public float DriftGrip = 0.4f;
        [Range(0.0f, 10.0f), Tooltip("Additional steer when the kart is drifting.")]
        public float DriftAdditionalSteer = 5.0f;
        [Range(1.0f, 30.0f), Tooltip("The higher the angle, the easier it is to regain full grip.")]
        public float MinAngleToFinishDrift = 10.0f;
        [Range(0.01f, 0.99f), Tooltip("Mininum speed percentage to switch back to full grip.")]
        public float MinSpeedPercentToFinishDrift = 0.5f;
        [Range(1.0f, 20.0f), Tooltip("The higher the value, the easier it is to control the drift steering.")]
        public float DriftControl = 10.0f;
        [Range(0.0f, 20.0f), Tooltip("The lower the value, the longer the drift will last without trying to control it by steering.")]
        public float DriftDampening = 10.0f;

        // ── BountyHunter: Pacejka 轮胎模型 ──────────────────────────────────
        // 参考《极品飞车：集结》写实物理：轮胎侧向力随滑移角动态变化
        // 对比原版固定 DriftGrip 值，Pacejka 在大滑移角时自然衰减，漂移更流畅
        [Header("BountyHunter: Pacejka 轮胎模型")]
        [Tooltip("B 刚度系数：越大在小滑移角时侧向力越强")]
        public float PacejkaB = 8f;
        [Tooltip("C 形状系数")]
        public float PacejkaC = 1.9f;
        [Tooltip("漂移状态下 D 峰值（越小越滑，对应 QQ飞车 夸张漂移）")]
        public float PacejkaDriftD = 0.35f;
        [Tooltip("E 曲率系数")]
        public float PacejkaE = 0.97f;

        // ── BountyHunter: 漂移蓄力 Boost（参考《QQ飞车》三段蓄力机制）────
        [Header("BountyHunter: 漂移蓄力 Boost")]
        [Tooltip("达到一级蓄力所需漂移时间（蓝焰）")]
        public float DriftChargeLevel1Time = 0.5f;
        [Tooltip("达到二级蓄力所需漂移时间（橙焰）")]
        public float DriftChargeLevel2Time = 1.2f;
        [Tooltip("达到三级蓄力所需漂移时间（粉焰·完美）")]
        public float DriftChargeLevel3Time = 2.5f;
        // ── BountyHunter: 横向摩擦力 + 转向响应 + 涡轮加速 ─────────────────
        [Tooltip("触发漂移所需的最低速度百分比（相对最高速），降低可在低速弯道也能漂移，默认 0.2）")]
        public float DriftMinSpeedPercent = 0.2f;
        [Tooltip("漂移时 WheelCollider 横向摩擦力刚度（原版≈1，降低允许侧滑）")]
        public float DriftSidewaysFriction = 0.2f;
        [Tooltip("漂移时转向灵敏度倍率（>1 使反打方向盘更灵敏）")]
        public float DriftSteerMultiplier = 1.8f;
        [Tooltip("涡轮加速持续时间（秒），按蓄力等级缩放")]
        public float TurboBoostDuration = 2.0f;
        [Tooltip("涡轮期间速度上限加成（m/s）")]
        public float TurboSpeedBonus = 5f;
        [Tooltip("涡轮期间加速度加成，越大冲向新上限越快，产生'往前猛冲'感")]
        public float TurboAccelBonus = 20f;
        [Tooltip("涡轮触发后阻力豁免时间（秒），防止松油门瞬间把冲量抵消")]
        public float BoostBypassDuration = 0.4f;
        // ────────────────────────────────────────────────────────────────────

        [Header("VFX")]
        [Tooltip("VFX that will be placed on the wheels when drifting.")]
        public ParticleSystem DriftSparkVFX;
        [Range(0.0f, 0.2f), Tooltip("Offset to displace the VFX to the side.")]
        public float DriftSparkHorizontalOffset = 0.1f;
        [Range(0.0f, 90.0f), Tooltip("Angle to rotate the VFX.")]
        public float DriftSparkRotation = 17.0f;
        [Tooltip("VFX that will be placed on the wheels when drifting.")]
        public GameObject DriftTrailPrefab;
        [Range(-0.1f, 0.1f), Tooltip("Vertical to move the trails up or down and ensure they are above the ground.")]
        public float DriftTrailVerticalOffset;
        [Tooltip("VFX that will spawn upon landing, after a jump.")]
        public GameObject JumpVFX;
        [Tooltip("VFX that is spawn on the nozzles of the kart.")]
        public GameObject NozzleVFX;
        [Tooltip("List of the kart's nozzles.")]
        public List<Transform> Nozzles;

        [Header("Suspensions")]
        [Tooltip("The maximum extension possible between the kart's body and the wheels.")]
        [Range(0.0f, 1.0f)]
        public float SuspensionHeight = 0.2f;
        [Range(10.0f, 100000.0f), Tooltip("The higher the value, the stiffer the suspension will be.")]
        public float SuspensionSpring = 20000.0f;
        [Range(0.0f, 5000.0f), Tooltip("The higher the value, the faster the kart will stabilize itself.")]
        public float SuspensionDamp = 500.0f;
        [Tooltip("Vertical offset to adjust the position of the wheels relative to the kart's body.")]
        [Range(-1.0f, 1.0f)]
        public float WheelsPositionVerticalOffset = 0.0f;

        [Header("Physical Wheels")]
        [Tooltip("The physical representations of the Kart's wheels.")]
        public WheelCollider FrontLeftWheel;
        public WheelCollider FrontRightWheel;
        public WheelCollider RearLeftWheel;
        public WheelCollider RearRightWheel;

        [Tooltip("Which layers the wheels will detect.")]
        public LayerMask GroundLayers = Physics.DefaultRaycastLayers;

        // the input sources that can control the kart
        IInput[] m_Inputs;

        const float k_NullInput = 0.01f;
        const float k_NullSpeed = 0.01f;
        Vector3 m_VerticalReference = Vector3.up;

        // Drift params
        public bool WantsToDrift { get; private set; } = false;
        public bool IsDrifting { get; private set; } = false;
        float m_CurrentGrip = 1.0f;
        float m_DriftTurningPower = 0.0f;
        float m_PreviousGroundPercent = 1.0f;

        // BountyHunter: 漂移蓄力状态
        float m_DriftChargeTime  = 0f;
        public int   DriftChargeLevel  { get; private set; } = 0; // 0=无 1=蓝焰 2=橙焰 3=粉焰
        public float TurboTimer        { get; private set; } = 0f; // >0 表示涡轮激活中，UI 直接读此值
        public int   TurboLevel        { get; private set; } = 0;  // 本次涡轮的等级（1/2/3）
        float m_BoostBypassTimer       = 0f; // 速度上限豁免期：Boost 触发后短暂无视 TopSpeed 限制
        float m_NormalSidewaysFriction = 1f;
        bool  m_DriftKeyHeld           = false;
        readonly List<(GameObject trailRoot, WheelCollider wheel, TrailRenderer trail)> m_DriftTrailInstances = new List<(GameObject, WheelCollider, TrailRenderer)>();
        readonly List<(WheelCollider wheel, float horizontalOffset, float rotation, ParticleSystem sparks)> m_DriftSparkInstances = new List<(WheelCollider, float, float, ParticleSystem)>();

        // can the kart move?
        bool m_CanMove = true;
        List<StatPowerup> m_ActivePowerupList = new List<StatPowerup>();
        ArcadeKart.Stats m_FinalStats;

        Quaternion m_LastValidRotation;
        Vector3 m_LastValidPosition;
        Vector3 m_LastCollisionNormal;
        bool m_HasCollision;
        bool m_InAir = false;

        public void AddPowerup(StatPowerup statPowerup) => m_ActivePowerupList.Add(statPowerup);
        public void SetCanMove(bool move) => m_CanMove = move;
        public float GetMaxSpeed() => Mathf.Max(m_FinalStats.TopSpeed, m_FinalStats.ReverseSpeed);

        private void ActivateDriftVFX(bool active)
        {
            foreach (var vfx in m_DriftSparkInstances)
            {
                if (active && vfx.wheel.GetGroundHit(out WheelHit hit))
                {
                    if (!vfx.sparks.isPlaying)
                        vfx.sparks.Play();
                }
                else
                {
                    if (vfx.sparks.isPlaying)
                        vfx.sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
                    
            }

            foreach (var trail in m_DriftTrailInstances)
                trail.Item3.emitting = active && trail.wheel.GetGroundHit(out WheelHit hit);
        }

        private void UpdateDriftVFXOrientation()
        {
            foreach (var vfx in m_DriftSparkInstances)
            {
                vfx.sparks.transform.position = vfx.wheel.transform.position - (vfx.wheel.radius * Vector3.up) + (DriftTrailVerticalOffset * Vector3.up) + (transform.right * vfx.horizontalOffset);
                vfx.sparks.transform.rotation = transform.rotation * Quaternion.Euler(0.0f, 0.0f, vfx.rotation);
            }

            foreach (var trail in m_DriftTrailInstances)
            {
                trail.trailRoot.transform.position = trail.wheel.transform.position - (trail.wheel.radius * Vector3.up) + (DriftTrailVerticalOffset * Vector3.up);
                trail.trailRoot.transform.rotation = transform.rotation;
            }
        }

        void UpdateSuspensionParams(WheelCollider wheel)
        {
            wheel.suspensionDistance = SuspensionHeight;
            wheel.center = new Vector3(0.0f, WheelsPositionVerticalOffset, 0.0f);
            JointSpring spring = wheel.suspensionSpring;
            spring.spring = SuspensionSpring;
            spring.damper = SuspensionDamp;
            wheel.suspensionSpring = spring;
        }

        void Awake()
        {
            Rigidbody = GetComponent<Rigidbody>();
            m_Inputs = GetComponents<IInput>();

            UpdateSuspensionParams(FrontLeftWheel);
            UpdateSuspensionParams(FrontRightWheel);
            UpdateSuspensionParams(RearLeftWheel);
            UpdateSuspensionParams(RearRightWheel);

            m_CurrentGrip = baseStats.Grip;
            // BountyHunter: 记录初始横向摩擦力
            m_NormalSidewaysFriction = RearLeftWheel.sidewaysFriction.stiffness;

            if (DriftSparkVFX != null)
            {
                AddSparkToWheel(RearLeftWheel, -DriftSparkHorizontalOffset, -DriftSparkRotation);
                AddSparkToWheel(RearRightWheel, DriftSparkHorizontalOffset, DriftSparkRotation);
            }

            if (DriftTrailPrefab != null)
            {
                AddTrailToWheel(RearLeftWheel);
                AddTrailToWheel(RearRightWheel);
            }

            if (NozzleVFX != null)
            {
                foreach (var nozzle in Nozzles)
                {
                    Instantiate(NozzleVFX, nozzle, false);
                }
            }
        }

        void AddTrailToWheel(WheelCollider wheel)
        {
            GameObject trailRoot = Instantiate(DriftTrailPrefab, gameObject.transform, false);
            TrailRenderer trail = trailRoot.GetComponentInChildren<TrailRenderer>();
            trail.emitting = false;
            m_DriftTrailInstances.Add((trailRoot, wheel, trail));
        }

        void AddSparkToWheel(WheelCollider wheel, float horizontalOffset, float rotation)
        {
            GameObject vfx = Instantiate(DriftSparkVFX.gameObject, wheel.transform, false);
            ParticleSystem spark = vfx.GetComponent<ParticleSystem>();
            spark.Stop();
            m_DriftSparkInstances.Add((wheel, horizontalOffset, -rotation, spark));
        }

        void FixedUpdate()
        {
            UpdateSuspensionParams(FrontLeftWheel);
            UpdateSuspensionParams(FrontRightWheel);
            UpdateSuspensionParams(RearLeftWheel);
            UpdateSuspensionParams(RearRightWheel);

            GatherInputs();

            // apply our powerups to create our finalStats
            TickPowerups();

            // BountyHunter: 涡轮计时器 + 速度豁免期倒计时
            if (TurboTimer > 0f)
            {
                TurboTimer -= Time.fixedDeltaTime;
                if (TurboTimer <= 0f) { TurboTimer = 0f; TurboLevel = 0; }
            }
            if (m_BoostBypassTimer > 0f)
                m_BoostBypassTimer -= Time.fixedDeltaTime;

            // apply our physics properties
            Rigidbody.centerOfMass = transform.InverseTransformPoint(CenterOfMass.position);

            int groundedCount = 0;
            if (FrontLeftWheel.isGrounded && FrontLeftWheel.GetGroundHit(out WheelHit hit))
                groundedCount++;
            if (FrontRightWheel.isGrounded && FrontRightWheel.GetGroundHit(out hit))
                groundedCount++;
            if (RearLeftWheel.isGrounded && RearLeftWheel.GetGroundHit(out hit))
                groundedCount++;
            if (RearRightWheel.isGrounded && RearRightWheel.GetGroundHit(out hit))
                groundedCount++;

            // calculate how grounded and airborne we are
            GroundPercent = (float) groundedCount / 4.0f;
            AirPercent = 1 - GroundPercent;

            // apply vehicle physics
            if (m_CanMove)
            {
                MoveVehicle(Input.Accelerate, Input.Brake, Input.TurnInput);
            }
            GroundAirbourne();

            m_PreviousGroundPercent = GroundPercent;

            UpdateDriftVFXOrientation();
        }

        void GatherInputs()
        {
            // reset input
            Input = new InputData();
            WantsToDrift = false;

            // gather nonzero input from our sources
            for (int i = 0; i < m_Inputs.Length; i++)
            {
                Input = m_Inputs[i].GenerateInput();
                // [BountyHunter] 原版仅 Brake 触发漂移；新增专用漂移键 Space，贴近《QQ飞车》操作习惯
                // WantsToDrift = Input.Brake && Vector3.Dot(Rigidbody.velocity, transform.forward) > 0.0f;
                m_DriftKeyHeld = Input.Brake || UnityEngine.Input.GetKey(KeyCode.Space);
                WantsToDrift = m_DriftKeyHeld && Vector3.Dot(Rigidbody.velocity, transform.forward) > 0.0f;
            }
        }

        void TickPowerups()
        {
            // remove all elapsed powerups
            m_ActivePowerupList.RemoveAll((p) => { return p.ElapsedTime > p.MaxTime; });

            // zero out powerups before we add them all up
            var powerups = new Stats();

            // add up all our powerups
            for (int i = 0; i < m_ActivePowerupList.Count; i++)
            {
                var p = m_ActivePowerupList[i];

                // add elapsed time
                p.ElapsedTime += Time.fixedDeltaTime;

                // add up the powerups
                powerups += p.modifiers;
            }

            // add powerups to our final stats
            m_FinalStats = baseStats + powerups;

            // clamp values in finalstats
            m_FinalStats.Grip = Mathf.Clamp(m_FinalStats.Grip, 0, 1);
        }

        void GroundAirbourne()
        {
            // while in the air, fall faster
            if (AirPercent >= 1)
            {
                Rigidbody.velocity += Physics.gravity * Time.fixedDeltaTime * m_FinalStats.AddedGravity;
            }
        }

        public void Reset()
        {
            Vector3 euler = transform.rotation.eulerAngles;
            euler.x = euler.z = 0f;
            transform.rotation = Quaternion.Euler(euler);
        }

        public float LocalSpeed()
        {
            if (m_CanMove)
            {
                float dot = Vector3.Dot(transform.forward, Rigidbody.velocity);
                if (Mathf.Abs(dot) > 0.1f)
                {
                    float speed = Rigidbody.velocity.magnitude;
                    return dot < 0 ? -(speed / m_FinalStats.ReverseSpeed) : (speed / m_FinalStats.TopSpeed);
                }
                return 0f;
            }
            else
            {
                // use this value to play kart sound when it is waiting the race start countdown.
                return Input.Accelerate ? 1.0f : 0.0f;
            }
        }

        // BountyHunter: 统一设置四轮 WheelCollider 横向摩擦力刚度
        // 漂移开始时降低（允许侧滑），漂移结束时恢复（恢复抓地）
        void SetWheelSidewaysFriction(float stiffness)
        {
            foreach (var wheel in new[] { FrontLeftWheel, FrontRightWheel, RearLeftWheel, RearRightWheel })
            {
                WheelFrictionCurve sf = wheel.sidewaysFriction;
                sf.stiffness = stiffness;
                wheel.sidewaysFriction = sf;
            }
        }

        // BountyHunter: Pacejka Magic Formula 内联实现
        // F = D * sin(C * arctan(B*slip - E*(B*slip - arctan(B*slip))))
        // 参考：H.B. Pacejka "Tire and Vehicle Dynamics"
        // 在《极品飞车：集结》等写实赛车游戏中广泛用于轮胎侧向力计算
        float PacejkaLateral(float slipAngleRad, float b, float c, float d, float e)
        {
            float slip  = slipAngleRad * Mathf.Rad2Deg;
            float bSlip = b * slip;
            return d * Mathf.Sin(c * Mathf.Atan(bSlip - e * (bSlip - Mathf.Atan(bSlip))));
        }

        void OnCollisionEnter(Collision collision) => m_HasCollision = true;
        void OnCollisionExit(Collision collision) => m_HasCollision = false;

        void OnCollisionStay(Collision collision)
        {
            m_HasCollision = true;
            m_LastCollisionNormal = Vector3.zero;
            float dot = -1.0f;

            foreach (var contact in collision.contacts)
            {
                if (Vector3.Dot(contact.normal, Vector3.up) > dot)
                    m_LastCollisionNormal = contact.normal;
            }
        }

        void MoveVehicle(bool accelerate, bool brake, float turnInput)
        {
            float accelInput = (accelerate ? 1.0f : 0.0f) - (brake ? 1.0f : 0.0f);

            // manual acceleration curve coefficient scalar
            float accelerationCurveCoeff = 5;
            Vector3 localVel = transform.InverseTransformVector(Rigidbody.velocity);

            bool accelDirectionIsFwd = accelInput >= 0;
            bool localVelDirectionIsFwd = localVel.z >= 0;

            // use the max speed for the direction we are going--forward or reverse.
            float maxSpeed = localVelDirectionIsFwd ? m_FinalStats.TopSpeed : m_FinalStats.ReverseSpeed;
            float accelPower = accelDirectionIsFwd ? m_FinalStats.Acceleration : m_FinalStats.ReverseAcceleration;

            float currentSpeed = Rigidbody.velocity.magnitude;
            float accelRampT = currentSpeed / maxSpeed;
            float multipliedAccelerationCurve = m_FinalStats.AccelerationCurve * accelerationCurveCoeff;
            float accelRamp = Mathf.Lerp(multipliedAccelerationCurve, 1, accelRampT * accelRampT);

            bool isBraking = (localVelDirectionIsFwd && brake) || (!localVelDirectionIsFwd && accelerate);

            // if we are braking (moving reverse to where we are going)
            // use the braking accleration instead
            float finalAccelPower = isBraking ? m_FinalStats.Braking : accelPower;

            float finalAcceleration = finalAccelPower * accelRamp;

            // apply inputs to forward/backward
            float turningPower = IsDrifting ? m_DriftTurningPower : turnInput * m_FinalStats.Steer;

            Quaternion turnAngle = Quaternion.AngleAxis(turningPower, transform.up);
            Vector3 fwd = turnAngle * transform.forward;
            Vector3 movement = fwd * accelInput * finalAcceleration * ((m_HasCollision || GroundPercent > 0.0f) ? 1.0f : 0.0f);

            // forward movement
            bool wasOverMaxSpeed = currentSpeed >= maxSpeed;

            // if over max speed, cannot accelerate faster.
            if (wasOverMaxSpeed && !isBraking) 
                movement *= 0.0f;

            Vector3 newVelocity = Rigidbody.velocity + movement * Time.fixedDeltaTime;
            newVelocity.y = Rigidbody.velocity.y;

            //  clamp max speed if we are on ground
            bool boostBypassing = m_BoostBypassTimer > 0f;
            if (GroundPercent > 0.0f && !wasOverMaxSpeed)
            {
                newVelocity = Vector3.ClampMagnitude(newVelocity, maxSpeed);
            }

            // coasting is when we aren't touching accelerate
            // [BountyHunter] 豁免期内跳过 CoastingDrag，否则松键瞬间阻力会抵消 Boost 冲量
            if (Mathf.Abs(accelInput) < k_NullInput && GroundPercent > 0.0f && !boostBypassing)
            {
                newVelocity = Vector3.MoveTowards(newVelocity, new Vector3(0, Rigidbody.velocity.y, 0), Time.fixedDeltaTime * m_FinalStats.CoastingDrag);
            }

            Rigidbody.velocity = newVelocity;

            // Drift
            if (GroundPercent > 0.0f)
            {
                if (m_InAir)
                {
                    m_InAir = false;
                    Instantiate(JumpVFX, transform.position, Quaternion.identity);
                }

                // manual angular velocity coefficient
                float angularVelocitySteering = 0.4f;
                float angularVelocitySmoothSpeed = 20f;

                // turning is reversed if we're going in reverse and pressing reverse
                if (!localVelDirectionIsFwd && !accelDirectionIsFwd) 
                    angularVelocitySteering *= -1.0f;

                var angularVel = Rigidbody.angularVelocity;

                // move the Y angular velocity towards our target
                angularVel.y = Mathf.MoveTowards(angularVel.y, turningPower * angularVelocitySteering, Time.fixedDeltaTime * angularVelocitySmoothSpeed);

                // apply the angular velocity
                Rigidbody.angularVelocity = angularVel;

                // rotate rigidbody's velocity as well to generate immediate velocity redirection
                // manual velocity steering coefficient
                float velocitySteering = 25f;

                // If the karts lands with a forward not in the velocity direction, we start the drift
                if (GroundPercent >= 0.0f && m_PreviousGroundPercent < 0.1f)
                {
                    Vector3 flattenVelocity = Vector3.ProjectOnPlane(Rigidbody.velocity, m_VerticalReference).normalized;
                    if (Vector3.Dot(flattenVelocity, transform.forward * Mathf.Sign(accelInput)) < Mathf.Cos(MinAngleToFinishDrift * Mathf.Deg2Rad))
                    {
                        IsDrifting = true;
                        m_CurrentGrip = DriftGrip;
                        m_DriftTurningPower = 0.0f;
                    }
                }

                // [BountyHunter] 原版漂移管理已注释，替换为 Pacejka + 三段蓄力系统
                // 原版问题：固定 DriftGrip 导致漂移"粘滞"，松方向盘立即停漂
                // 新版改进：DriftDampening 减弱 + Boost 在漂移结束时释放
                //
                // if (!IsDrifting) { if ((WantsToDrift||isBraking)&&speed>threshold) { IsDrifting=true; m_CurrentGrip=DriftGrip; } }
                // if (IsDrifting)  { ... m_DriftTurningPower ... canEndDrift ... IsDrifting=false; m_CurrentGrip=Grip; }

                // BountyHunter: 漂移触发
                if (!IsDrifting)
                {
                    if ((WantsToDrift || isBraking) && currentSpeed > maxSpeed * DriftMinSpeedPercent)
                    {
                        IsDrifting        = true;
                        m_DriftTurningPower = turningPower + (Mathf.Sign(turningPower) * DriftAdditionalSteer);
                        m_CurrentGrip     = DriftGrip;
                        m_DriftChargeTime = 0f;
                        DriftChargeLevel  = 0;
                        // BountyHunter: 降低 WheelCollider 横向摩擦力，允许车辆侧滑
                        SetWheelSidewaysFriction(DriftSidewaysFriction);
                        ActivateDriftVFX(true);
                    }
                }

                if (IsDrifting)
                {
                    // BountyHunter: 蓄力计时（参考《QQ飞车》三段充能）
                    m_DriftChargeTime += Time.fixedDeltaTime;
                    if      (m_DriftChargeTime >= DriftChargeLevel3Time) DriftChargeLevel = 3;
                    else if (m_DriftChargeTime >= DriftChargeLevel2Time) DriftChargeLevel = 2;
                    else if (m_DriftChargeTime >= DriftChargeLevel1Time) DriftChargeLevel = 1;
                    else                                                   DriftChargeLevel = 0;

                    float turnInputAbs = Mathf.Abs(turnInput);

                    // [BountyHunter] 原版 DriftDampening=10 过强，松手立即停漂
                    // 改为 0.3 倍衰减，使漂移持续更久（偏《QQ飞车》手感）
                    // if (turnInputAbs < k_NullInput)
                    //     m_DriftTurningPower = Mathf.MoveTowards(m_DriftTurningPower, 0.0f, Mathf.Clamp01(DriftDampening * Time.fixedDeltaTime));
                    if (turnInputAbs < k_NullInput)
                        m_DriftTurningPower = Mathf.MoveTowards(m_DriftTurningPower, 0.0f, Mathf.Clamp01(DriftDampening * 0.3f * Time.fixedDeltaTime));

                    float driftMaxSteerValue = m_FinalStats.Steer + DriftAdditionalSteer;
                    // [BountyHunter] 原版转向响应：DriftControl * deltaTime
                    // 新版：乘以 DriftSteerMultiplier，反打方向盘更灵敏（提升操控爽快感）
                    // m_DriftTurningPower = Mathf.Clamp(m_DriftTurningPower + (turnInput * Mathf.Clamp01(DriftControl * Time.fixedDeltaTime)), ...);
                    m_DriftTurningPower = Mathf.Clamp(m_DriftTurningPower + (turnInput * Mathf.Clamp01(DriftControl * DriftSteerMultiplier * Time.fixedDeltaTime)), -driftMaxSteerValue, driftMaxSteerValue);

                    bool facingVelocity = Vector3.Dot(Rigidbody.velocity.normalized, transform.forward * Mathf.Sign(accelInput)) > Mathf.Cos(MinAngleToFinishDrift * Mathf.Deg2Rad);

                    bool canEndDrift = true;
                    // [BountyHunter] 新增：松开漂移键立即结束漂移（QQ飞车模型：松键=触发Boost）
                    // 原版：只有车身对齐速度方向才结束，导致松键后漂移仍持续
                    if (!m_DriftKeyHeld)
                        canEndDrift = true;
                    else if (isBraking)
                        canEndDrift = false;
                    else if (!facingVelocity)
                        canEndDrift = false;
                    else if (turnInputAbs >= k_NullInput && currentSpeed > maxSpeed * MinSpeedPercentToFinishDrift)
                        canEndDrift = false;

                    if (canEndDrift || currentSpeed < k_NullSpeed)
                    {
                        IsDrifting    = false;
                        m_CurrentGrip = m_FinalStats.Grip;
                        // BountyHunter: 恢复横向摩擦力
                        SetWheelSidewaysFriction(m_NormalSidewaysFriction);

                        // BountyHunter: 漂移结束触发"涡轮加速"
                        // 原版：无任何奖励
                        // 新版：瞬间冲量（即时手感）+ Powerup 持续加速（涡轮持续感）
                        // 对比《QQ飞车》：等级越高，冲量越大、持续时间越长
                        if (DriftChargeLevel > 0)
                        {
                            float t = DriftChargeLevel / 3f;

                            // BountyHunter: 涡轮冲量分两段
                            // ① 瞬间大冲量（1.5 倍 TurboSpeedBonus）——制造"往前猛冲"的即时感
                            //    原版 0.6 倍冲量太弱，新版参考《赛博朋克2077》Dash 的瞬间加速感
                            Rigidbody.velocity += transform.forward * (TurboSpeedBonus * t * 1.5f);

                            // ② 阻力豁免：防止松油门时 CoastingDrag 立即把冲量抵消
                            m_BoostBypassTimer = BoostBypassDuration;

                            // ③ 持续 Powerup：同时提升 TopSpeed + Acceleration
                            //    TopSpeed 拉高速度上限，Acceleration 让车快速冲向新上限
                            //    两者结合才能产生"往前猛冲"而不是"慢慢加速"的感觉
                            AddPowerup(new StatPowerup
                            {
                                PowerUpID = "DriftTurbo",
                                MaxTime   = TurboBoostDuration * t,
                                modifiers = new Stats
                                {
                                    TopSpeed     = TurboSpeedBonus * t,
                                    Acceleration = TurboAccelBonus * t
                                }
                            });

                            TurboTimer = TurboBoostDuration * t;
                            TurboLevel = DriftChargeLevel;
                        }
                        m_DriftChargeTime = 0f;
                        DriftChargeLevel  = 0;
                    }
                }

                // [BountyHunter] 原版固定 m_CurrentGrip；漂移时替换为 Pacejka 动态侧向力系数
                // 原版: Rigidbody.velocity = Quaternion.AngleAxis(turningPower * Mathf.Sign(localVel.z) * velocitySteering * m_CurrentGrip * Time.fixedDeltaTime, transform.up) * Rigidbody.velocity;
                //
                // Pacejka 原理：大滑移角时侧向力自然衰减 → 漂移角越大越滑 → 漂移不会因速度降低而突然停止
                float effectiveGrip = m_CurrentGrip;
                if (IsDrifting)
                {
                    float latVel  = Vector3.Dot(Rigidbody.velocity, transform.right);
                    float fwdVel  = Mathf.Max(0.1f, Mathf.Abs(Vector3.Dot(Rigidbody.velocity, transform.forward)));
                    float slipRad = Mathf.Atan2(latVel, fwdVel);
                    effectiveGrip = Mathf.Abs(PacejkaLateral(slipRad, PacejkaB, PacejkaC, PacejkaDriftD, PacejkaE));
                }
                Rigidbody.velocity = Quaternion.AngleAxis(turningPower * Mathf.Sign(localVel.z) * velocitySteering * effectiveGrip * Time.fixedDeltaTime, transform.up) * Rigidbody.velocity;
            }
            else
            {
                m_InAir = true;
            }

            bool validPosition = false;
            if (Physics.Raycast(transform.position + (transform.up * 0.1f), -transform.up, out RaycastHit hit, 3.0f, 1 << 9 | 1 << 10 | 1 << 11)) // Layer: ground (9) / Environment(10) / Track (11)
            {
                Vector3 lerpVector = (m_HasCollision && m_LastCollisionNormal.y > hit.normal.y) ? m_LastCollisionNormal : hit.normal;
                m_VerticalReference = Vector3.Slerp(m_VerticalReference, lerpVector, Mathf.Clamp01(AirborneReorientationCoefficient * Time.fixedDeltaTime * (GroundPercent > 0.0f ? 10.0f : 1.0f)));    // Blend faster if on ground
            }
            else
            {
                Vector3 lerpVector = (m_HasCollision && m_LastCollisionNormal.y > 0.0f) ? m_LastCollisionNormal : Vector3.up;
                m_VerticalReference = Vector3.Slerp(m_VerticalReference, lerpVector, Mathf.Clamp01(AirborneReorientationCoefficient * Time.fixedDeltaTime));
            }

            validPosition = GroundPercent > 0.7f && !m_HasCollision && Vector3.Dot(m_VerticalReference, Vector3.up) > 0.9f;

            // Airborne / Half on ground management
            if (GroundPercent < 0.7f)
            {
                Rigidbody.angularVelocity = new Vector3(0.0f, Rigidbody.angularVelocity.y * 0.98f, 0.0f);
                Vector3 finalOrientationDirection = Vector3.ProjectOnPlane(transform.forward, m_VerticalReference);
                finalOrientationDirection.Normalize();
                if (finalOrientationDirection.sqrMagnitude > 0.0f)
                {
                    Rigidbody.MoveRotation(Quaternion.Lerp(Rigidbody.rotation, Quaternion.LookRotation(finalOrientationDirection, m_VerticalReference), Mathf.Clamp01(AirborneReorientationCoefficient * Time.fixedDeltaTime)));
                }
            }
            else if (validPosition)
            {
                m_LastValidPosition = transform.position;
                m_LastValidRotation.eulerAngles = new Vector3(0.0f, transform.rotation.y, 0.0f);
            }

            ActivateDriftVFX(IsDrifting && GroundPercent > 0.0f);
        }
    }
}
