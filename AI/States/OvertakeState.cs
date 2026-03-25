using UnityEngine;

namespace BountyHunter.AI.States
{
    /// <summary>
    /// 超车状态：检测到前方有车时，计算绕行路线
    /// </summary>
    public class OvertakeState : AIState
    {
        private float   _overtakeTimer;
        private Vector3 _sideOffset;
        private const float MaxOvertakeTime = 3f;

        public OvertakeState(KartAIController ai) : base(ai) { }

        public override void OnEnter()
        {
            Debug.Log($"[AI:{AI.name}] → Overtake");
            _overtakeTimer = 0f;

            // 判断从左侧还是右侧超车（取对手车辆与赛道切线的关系）
            if (AI.TargetObstacle != null)
            {
                Vector3 toObstacle = AI.TargetObstacle.position - AI.transform.position;
                float dot = Vector3.Dot(AI.transform.right, toObstacle.normalized);
                // 对方偏右 → 我向左超车
                _sideOffset = dot > 0
                    ? -AI.transform.right * AI.OvertakeSideOffset
                    :  AI.transform.right * AI.OvertakeSideOffset;
            }
        }

        public override void OnUpdate(float deltaTime)
        {
            _overtakeTimer += deltaTime;

            // 超车目标点 = 当前航点 + 侧偏
            Vector3 target = AI.Navigator.CurrentWaypoint != null
                ? AI.Navigator.CurrentWaypoint.position + _sideOffset
                : AI.transform.position + AI.transform.forward * 10f;

            Vector3 localDir = AI.transform.InverseTransformDirection(
                (target - AI.transform.position).normalized);

            float steer    = Mathf.Clamp(localDir.x * 2f, -1f, 1f);
            float throttle = 1f;  // 超车全油门

            AI.SetInput(steer, throttle, 0f, driftHeld: false);

            // 超时或已超过对手 → 回到 Race
            bool pastObstacle = AI.TargetObstacle != null &&
                Vector3.Dot(AI.transform.forward,
                    AI.TargetObstacle.position - AI.transform.position) < 0;

            if (_overtakeTimer > MaxOvertakeTime || pastObstacle)
                AI.StateMachine.TransitionTo<RaceState>();
        }

        public override void OnExit() { }
    }
}
