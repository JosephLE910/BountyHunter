using UnityEngine;

namespace BountyHunter.AI
{
    /// <summary>
    /// 航点路径追踪器
    /// 提供 AI 所需的路径跟随基础数据
    /// </summary>
    public class WaypointNavigator : MonoBehaviour
    {
        [Header("Waypoints")]
        [Tooltip("赛道航点列表，按行进方向排列")]
        public Transform[] Waypoints;
        [Tooltip("到达航点的切换距离")]
        public float WaypointRadius = 5f;
        [Tooltip("是否循环（比赛用途一般为 true）")]
        public bool Loop = true;

        public int   CurrentIndex   { get; private set; }
        public Transform CurrentWaypoint => Waypoints.Length > 0 ? Waypoints[CurrentIndex] : null;

        /// <summary>
        /// 到当前目标航点的方向（已归一化）
        /// </summary>
        public Vector3 DirectionToWaypoint
        {
            get
            {
                if (CurrentWaypoint == null) return transform.forward;
                return (CurrentWaypoint.position - transform.position).normalized;
            }
        }

        /// <summary>
        /// 所需转向输入 (-1 ~ 1)，基于车辆朝向与目标方向的夹角
        /// </summary>
        public float RequiredSteer
        {
            get
            {
                Vector3 local = transform.InverseTransformDirection(DirectionToWaypoint);
                return Mathf.Clamp(local.x, -1f, 1f);
            }
        }

        public float DistanceToWaypoint =>
            CurrentWaypoint != null
                ? Vector3.Distance(transform.position, CurrentWaypoint.position)
                : float.MaxValue;

        private void Update()
        {
            if (Waypoints.Length == 0) return;
            if (DistanceToWaypoint < WaypointRadius)
                AdvanceWaypoint();
        }

        private void AdvanceWaypoint()
        {
            CurrentIndex++;
            if (CurrentIndex >= Waypoints.Length)
                CurrentIndex = Loop ? 0 : Waypoints.Length - 1;
        }

        /// <summary>
        /// 向前预看 lookahead 个航点，用于 AI 预判弯道
        /// </summary>
        public Transform PeekWaypoint(int lookahead)
        {
            int idx = (CurrentIndex + lookahead) % Waypoints.Length;
            return Waypoints[idx];
        }
    }
}
