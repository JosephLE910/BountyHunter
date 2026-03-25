using UnityEngine;

namespace BountyHunter.AI.States
{
    /// <summary>
    /// 阻挡状态：当 AI 排名靠前、后方有玩家逼近时，横向移动进行干扰
    /// </summary>
    public class BlockState : AIState
    {
        private float _blockTimer;
        private const float MaxBlockTime = 2f;

        public BlockState(KartAIController ai) : base(ai) { }

        public override void OnEnter() =>
            Debug.Log($"[AI:{AI.name}] → Block");

        public override void OnUpdate(float deltaTime)
        {
            _blockTimer += deltaTime;

            float steer = AI.Navigator.RequiredSteer;

            // 检测后方来车方向，向其移动方向靠拢以阻挡
            if (AI.TargetObstacle != null)
            {
                Vector3 pursuerLocal = AI.transform.InverseTransformPoint(AI.TargetObstacle.position);
                // 追赶者偏左 → 我也向左移，阻挡其超车线路
                float blockBias = Mathf.Clamp(pursuerLocal.x * 0.5f, -0.4f, 0.4f);
                steer += blockBias;
            }

            AI.SetInput(Mathf.Clamp(steer, -1f, 1f), 0.9f, 0f, driftHeld: false);

            if (_blockTimer > MaxBlockTime)
                AI.StateMachine.TransitionTo<RaceState>();
        }

        public override void OnExit() { }
    }
}
