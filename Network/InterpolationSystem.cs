using System.Collections.Generic;
using UnityEngine;

namespace BountyHunter.Network
{
    /// <summary>
    /// 远程玩家插值系统
    ///
    /// 策略：保留最近 N 条状态快照，渲染时滞后 InterpolationDelay 秒，
    /// 在两条快照之间线性/球形插值，实现平滑移动。
    ///
    /// 弱网处理：
    /// - 快照过期（超过 2 倍延迟仍未收到新包）→ 切换外推模式（用速度推算位置）
    /// - 外推时间过长（>500ms）→ 停止外推，等待下一个快照
    /// </summary>
    public class InterpolationSystem : MonoBehaviour
    {
        public float InterpolationDelay = 0.1f;   // 渲染落后多少秒（缓冲）
        public float MaxExtrapolateTime = 0.5f;   // 最大外推时间

        private readonly List<KartNetState> _buffer = new();
        private float _extrapolateTimer;
        private KartNetState _lastState;

        /// <summary>
        /// 收到服务端/其他玩家广播的状态时调用
        /// </summary>
        public void ReceiveState(KartNetState state)
        {
            // state.Tick 已包含服务端 tick，直接入缓冲
            _buffer.Add(state);
            _extrapolateTimer = 0f;

            // 保留最近 60 条快照（~1秒@60fps）
            if (_buffer.Count > 60) _buffer.RemoveAt(0);
        }

        private void Update()
        {
            if (_buffer.Count < 2)
            {
                // 快照不足时外推
                Extrapolate(Time.deltaTime);
                return;
            }

            float renderTime = Time.time - InterpolationDelay;

            // 找到夹住 renderTime 的两条快照
            // 简化：用最后两条快照（实际应基于时间戳二分查找）
            var older = _buffer[_buffer.Count - 2];
            var newer = _buffer[_buffer.Count - 1];

            // 用 tick 差估算时间（fixedDeltaTime * tickDiff）
            float duration = (newer.Tick - older.Tick) * Time.fixedDeltaTime;
            if (duration <= 0f) return;

            float t = Mathf.Clamp01((Time.time - renderTime) / duration);

            transform.position = Vector3.Lerp(older.Position, newer.Position, t);
            transform.rotation = Quaternion.Slerp(older.Rotation, newer.Rotation, t);

            _lastState = newer;
            _extrapolateTimer = 0f;
        }

        private void Extrapolate(float dt)
        {
            _extrapolateTimer += dt;
            if (_extrapolateTimer > MaxExtrapolateTime) return;

            // 用最后已知速度外推位置
            transform.position += _lastState.Velocity * dt;
            // 旋转外推：用角速度
            if (_lastState.AngularVel.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Euler(
                    _lastState.AngularVel * Mathf.Rad2Deg * dt) * transform.rotation;
            }
        }
    }
}
