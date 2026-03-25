using UnityEngine;
using BountyHunter.Physics;
using BountyHunter.Shared;

namespace BountyHunter.Network
{
    /// <summary>
    /// 客户端预测 + 回滚重推演
    ///
    /// 原理：
    /// 1. 本地输入立即执行，存入环形缓冲区
    /// 2. 收到服务端权威状态后，比对同Tick的本地预测状态
    /// 3. 若误差过大，回滚到权威状态，重新推演至当前帧（Replay）
    ///
    /// 参考：GDC《Replicating Chaos: Vehicle Replication in Watch Dogs 2》
    /// </summary>
    public class ClientPrediction : MonoBehaviour
    {
        [Tooltip("环形缓冲区大小，需 >= 网络RTT对应帧数")]
        public int BufferSize = 64;

        private KartInput[]    _inputBuffer;
        private KartNetState[] _stateBuffer;

        private void Awake()
        {
            _inputBuffer = new KartInput[BufferSize];
            _stateBuffer = new KartNetState[BufferSize];
        }

        public void StoreInput(KartInput input)
        {
            _inputBuffer[input.Tick % BufferSize] = input;
        }

        public void StoreState(uint tick, KartNetState state)
        {
            _stateBuffer[tick % BufferSize] = state;
        }

        public KartNetState GetStoredState(uint tick)
        {
            return _stateBuffer[tick % BufferSize];
        }

        /// <summary>
        /// 回滚：将物理状态恢复到权威快照
        /// </summary>
        public void Rollback(KartNetState authoritative)
        {
            var rb = GetComponent<Rigidbody>();
            if (rb == null) return;

            transform.position  = authoritative.Position;
            transform.rotation  = authoritative.Rotation;
            rb.velocity   = authoritative.Velocity;
            rb.angularVelocity  = authoritative.AngularVel;
        }

        /// <summary>
        /// 重推演：从权威帧开始，用缓冲区内的输入重新模拟到 currentTick
        /// </summary>
        public void Replay(KartController kart, uint currentTick)
        {
            // 权威状态对应的 tick 已在 Rollback 中恢复
            // 注意：Replay 需要在 Physics 模拟环境下逐帧运行
            // Unity 中可通过 Physics.Simulate() 实现逐帧推演
            for (uint t = GetOldestStoredTick(currentTick); t < currentTick; t++)
            {
                var input = _inputBuffer[t % BufferSize];
                kart.ApplyInput(input);
                UnityEngine.Physics.Simulate(Time.fixedDeltaTime);

                // 更新预测缓冲
                var rb = GetComponent<Rigidbody>();
                StoreState(t, new KartNetState
                {
                    Tick       = t,
                    Position   = transform.position,
                    Rotation   = transform.rotation,
                    Velocity   = rb != null ? rb.velocity : Vector3.zero,
                    AngularVel = rb != null ? rb.angularVelocity : Vector3.zero
                });
            }
        }

        private uint GetOldestStoredTick(uint currentTick)
        {
            return currentTick >= (uint)BufferSize
                ? currentTick - (uint)BufferSize + 1
                : 0;
        }
    }
}
