using System.Collections.Generic;
using UnityEngine;

namespace BountyHunter.Shared
{
    /// <summary>
    /// 管理比赛状态：排名、圈数、计时
    /// </summary>
    public class RaceManager : MonoBehaviour
    {
        public static RaceManager Instance { get; private set; }

        [Header("Race Settings")]
        public int TotalLaps = 3;

        private readonly Dictionary<int, RacerState> _racers = new();

        public struct RacerState
        {
            public int   PlayerId;
            public int   CurrentLap;
            public int   WaypointIndex;   // 当前到达的航点索引（用于计算排名）
            public float LapStartTime;
            public float BestLapTime;
        }

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void RegisterRacer(int playerId)
        {
            _racers[playerId] = new RacerState
            {
                PlayerId     = playerId,
                CurrentLap   = 1,
                WaypointIndex = 0,
                LapStartTime = Time.time,
                BestLapTime  = float.MaxValue
            };
        }

        /// <summary>
        /// 车辆经过航点时调用，更新排名数据
        /// </summary>
        public void OnWaypointPassed(int playerId, int waypointIndex, int totalWaypoints)
        {
            if (!_racers.TryGetValue(playerId, out var state)) return;

            state.WaypointIndex = waypointIndex;

            // 过终点线
            if (waypointIndex == 0 && state.WaypointIndex == totalWaypoints - 1)
            {
                float lapTime = Time.time - state.LapStartTime;
                if (lapTime < state.BestLapTime) state.BestLapTime = lapTime;

                state.CurrentLap++;
                state.LapStartTime = Time.time;

                if (state.CurrentLap > TotalLaps)
                    OnRacerFinished(playerId);
            }

            _racers[playerId] = state;
        }

        /// <summary>
        /// 返回当前排名列表（按圈数+航点降序）
        /// </summary>
        public List<(int playerId, int rank)> GetRankings()
        {
            var list = new List<(int, int)>();
            int rank = 1;
            // 简单排序：圈数多 > 航点靠前
            var sorted = new List<RacerState>(_racers.Values);
            sorted.Sort((a, b) =>
            {
                if (a.CurrentLap != b.CurrentLap) return b.CurrentLap - a.CurrentLap;
                return b.WaypointIndex - a.WaypointIndex;
            });
            foreach (var s in sorted)
                list.Add((s.PlayerId, rank++));
            return list;
        }

        private void OnRacerFinished(int playerId)
        {
            Debug.Log($"[RaceManager] Player {playerId} finished the race!");
        }
    }
}
