using System.Collections.Generic;
using UnityEngine;

namespace BountyHunter.Network
{
    /// <summary>
    /// 延迟补偿（用于道具/碰撞判定）
    ///
    /// 原理：服务端保存各车辆历史位置快照，
    /// 当玩家触发道具/碰撞时，将所有车辆回滚到玩家发出操作时的时间点进行判定，
    /// 再恢复到当前状态。
    ///
    /// 竞速游戏中的碰撞问题：
    /// - 弱网下两台车的本地表现可能不一致（A看到撞上了，B看到没撞）
    /// - 解决方案：服务端延迟补偿 + 视觉层面的"碰撞特效对齐"
    /// </summary>
    public class LagCompensator : MonoBehaviour
    {
        [Tooltip("历史快照保留时间（秒）")]
        public float HistoryDuration = 1.0f;

        private readonly Dictionary<int, Queue<(float time, Vector3 pos, Quaternion rot)>> _history = new();

        /// <summary>
        /// 每帧记录车辆位置快照（由服务端 NetworkManager 调用）
        /// </summary>
        public void RecordSnapshot(int vehicleId, Vector3 pos, Quaternion rot)
        {
            if (!_history.ContainsKey(vehicleId))
                _history[vehicleId] = new Queue<(float, Vector3, Quaternion)>();

            var q = _history[vehicleId];
            q.Enqueue((Time.time, pos, rot));

            // 清理过期快照
            while (q.Count > 0 && Time.time - q.Peek().time > HistoryDuration)
                q.Dequeue();
        }

        /// <summary>
        /// 在 targetTime 时刻获取车辆的历史变换（用于碰撞判定）
        /// </summary>
        public bool GetHistoricalTransform(int vehicleId, float targetTime,
            out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;

            if (!_history.TryGetValue(vehicleId, out var q) || q.Count < 2)
                return false;

            // 在快照队列中找到夹住 targetTime 的两帧
            var arr = q.ToArray();
            for (int i = 0; i < arr.Length - 1; i++)
            {
                if (arr[i].time <= targetTime && arr[i + 1].time >= targetTime)
                {
                    float t = (targetTime - arr[i].time) / (arr[i + 1].time - arr[i].time);
                    pos = Vector3.Lerp(arr[i].pos, arr[i + 1].pos, t);
                    rot = Quaternion.Slerp(arr[i].rot, arr[i + 1].rot, t);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 在历史位置执行碰撞/道具判定，返回是否命中
        /// </summary>
        public bool PerformHistoricalCheck(int attackerId, int targetId,
            float clientTimestamp, float checkRadius)
        {
            if (!GetHistoricalTransform(attackerId, clientTimestamp, out var aPos, out _))
                return false;
            if (!GetHistoricalTransform(targetId, clientTimestamp, out var tPos, out _))
                return false;

            return Vector3.Distance(aPos, tPos) < checkRadius;
        }
    }
}
