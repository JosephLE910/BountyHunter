using UnityEngine;
using BountyHunter.AI.States;
using BountyHunter.Physics;
using BountyHunter.Shared;

namespace BountyHunter.AI
{
    /// <summary>
    /// 赛车 AI 主控制器
    ///
    /// FSM 状态：
    ///   Race  ─→ Overtake  (前方 DetectRange 内有障碍物且在同向车道)
    ///   Race  ─→ Block     (后方 BlockRange 内有追赶者且 AI 排名靠前)
    ///   Overtake / Block ─→ Race  (条件消失或超时)
    /// </summary>
    [RequireComponent(typeof(WaypointNavigator))]
    [RequireComponent(typeof(KartController))]
    public class KartAIController : MonoBehaviour
    {
        [Header("Detection")]
        [Tooltip("前方障碍物检测距离")]
        public float DetectRange       = 15f;
        [Tooltip("后方追赶者检测距离")]
        public float BlockRange        = 12f;
        [Tooltip("超车时的侧向偏移距离")]
        public float OvertakeSideOffset = 3f;

        [Header("Difficulty")]
        [Tooltip("0=新手(慢反应)  1=高手(即时反应)")]
        [Range(0f, 1f)]
        public float Difficulty        = 0.7f;

        // 公开给 State 访问
        public WaypointNavigator Navigator    { get; private set; }
        public AIStateMachine    StateMachine { get; private set; }
        public Transform         TargetObstacle { get; private set; }

        private KartController   _kart;
        private float            _reactionTimer;

        private void Awake()
        {
            Navigator    = GetComponent<WaypointNavigator>();
            _kart        = GetComponent<KartController>();

            StateMachine = new AIStateMachine();
            StateMachine.RegisterState(new RaceState(this));
            StateMachine.RegisterState(new OvertakeState(this));
            StateMachine.RegisterState(new BlockState(this));
        }

        private void Start() => StateMachine.Start<RaceState>();

        private void Update()
        {
            // 难度越低，决策延迟越高（模拟反应时间）
            _reactionTimer += Time.deltaTime;
            float reactionInterval = Mathf.Lerp(0.3f, 0.05f, Difficulty);
            if (_reactionTimer < reactionInterval) return;
            _reactionTimer = 0f;

            EvaluateTransitions();
            StateMachine.Update(Time.deltaTime);
        }

        /// <summary>
        /// 供 State 调用，向 KartController 写入输入
        /// </summary>
        public void SetInput(float steer, float throttle, float brake, bool driftHeld)
        {
            _kart.ApplyInput(new KartInput
            {
                Steering  = steer,
                Throttle  = throttle * Mathf.Lerp(0.7f, 1f, Difficulty),  // 难度影响油门上限
                Brake     = brake,
                DriftHeld = driftHeld
            });
        }

        // ─── 状态转换评估 ─────────────────────────────────────────────────────

        private void EvaluateTransitions()
        {
            bool inRace     = StateMachine.CurrentState is RaceState;
            bool inOvertake = StateMachine.CurrentState is OvertakeState;
            bool inBlock    = StateMachine.CurrentState is BlockState;

            // 前方障碍检测
            Transform frontObstacle = DetectFront();
            // 后方追赶者检测
            Transform rearPursuer   = DetectRear();

            TargetObstacle = frontObstacle ?? rearPursuer;

            if (inRace)
            {
                if (frontObstacle != null)
                    StateMachine.TransitionTo<OvertakeState>();
                else if (rearPursuer != null)
                    StateMachine.TransitionTo<BlockState>();
            }
        }

        private Transform DetectFront()
        {
            // 扇形射线检测前方车辆（3条射线：中、左15°、右15°）
            Vector3[] dirs = {
                transform.forward,
                Quaternion.Euler(0, 15f, 0) * transform.forward,
                Quaternion.Euler(0,-15f, 0) * transform.forward
            };

            foreach (var dir in dirs)
            {
                if (UnityEngine.Physics.Raycast(transform.position + Vector3.up * 0.5f,
                        dir, out RaycastHit hit, DetectRange))
                {
                    if (hit.collider.CompareTag("Kart") && hit.transform != transform)
                        return hit.transform;
                }
            }
            return null;
        }

        private Transform DetectRear()
        {
            if (UnityEngine.Physics.Raycast(transform.position + Vector3.up * 0.5f,
                    -transform.forward, out RaycastHit hit, BlockRange))
            {
                if (hit.collider.CompareTag("Kart") && hit.transform != transform)
                    return hit.transform;
            }
            return null;
        }

        private void OnDrawGizmosSelected()
        {
            // 可视化检测范围
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, transform.forward * DetectRange);
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, -transform.forward * BlockRange);
        }
    }
}
