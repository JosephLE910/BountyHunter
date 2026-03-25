using UnityEngine;

namespace BountyHunter.AI.States
{
    /// <summary>
    /// 正常行驶状态：沿航点路线行驶，自动调节油门
    /// </summary>
    public class RaceState : AIState
    {
        public RaceState(KartAIController ai) : base(ai) { }

        public override void OnEnter() =>
            Debug.Log($"[AI:{AI.name}] → Race");

        public override void OnUpdate(float deltaTime)
        {
            float steer    = AI.Navigator.RequiredSteer;
            float throttle = CalcThrottle();
            float brake    = 0f;

            // 急弯减速
            float cornerAngle = Vector3.Angle(AI.transform.forward, AI.Navigator.DirectionToWaypoint);
            if (cornerAngle > 30f)
            {
                throttle = Mathf.Lerp(throttle, 0.3f, (cornerAngle - 30f) / 60f);
                if (cornerAngle > 60f) brake = 0.3f;
            }

            AI.SetInput(steer, throttle, brake, driftHeld: false);
        }

        public override void OnExit() { }

        private float CalcThrottle()
        {
            // 预看下两个航点，评估弯道曲率
            var next1 = AI.Navigator.PeekWaypoint(1);
            var next2 = AI.Navigator.PeekWaypoint(2);
            if (next1 == null || next2 == null) return 1f;

            Vector3 dir1 = (next1.position - AI.transform.position).normalized;
            Vector3 dir2 = (next2.position - next1.position).normalized;
            float angle  = Vector3.Angle(dir1, dir2);

            // 弯道越急，油门越小
            return Mathf.Clamp01(1f - angle / 120f);
        }
    }
}
