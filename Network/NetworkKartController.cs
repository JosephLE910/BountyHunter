using Unity.Netcode;
using UnityEngine;
using BountyHunter.Physics;
using BountyHunter.Shared;

namespace BountyHunter.Network
{
    /// <summary>
    /// 网络化车辆控制器（基于 Netcode for GameObjects）
    ///
    /// 架构：权威服务器 + 客户端预测
    ///
    /// 技术对比：
    /// - 《极品飞车：集结》：状态同步为主，服务端权威，移动端带宽敏感
    /// - 《火箭联盟》      ：帧同步 + 物理回滚，高精度但对网络要求更高
    /// - 本实现            ：状态同步 + 客户端预测 + 插值，适合手游网络环境
    ///
    /// 同步策略说明：
    /// 赛车游戏选择状态同步 vs 帧同步：
    ///   帧同步优势：所有客户端结果完全一致，碰撞精确
    ///   帧同步劣势：一个客户端卡顿会拖慢所有人，移动端不稳定网络难以保证
    ///   状态同步优势：容错性强，每个客户端独立运行，适合手游
    ///   状态同步劣势：碰撞不完全一致，需额外的一致性修正
    ///   → 《极品飞车：集结》选择状态同步，本实现遵循同样设计
    /// </summary>
    public class NetworkKartController : NetworkBehaviour
    {
        [Header("Prediction")]
        [Tooltip("客户端预测缓冲帧数")]
        public int   InputBufferSize     = 64;
        [Tooltip("位置纠错插值速度（服务端权威纠正时）")]
        public float CorrectionLerpSpeed = 15f;

        [Header("Remote Interpolation")]
        [Tooltip("远程玩家渲染延迟（秒），用于插值平滑")]
        public float InterpolationDelay  = 0.1f;

        // NetworkVariable：服务端写，所有客户端读
        private NetworkVariable<KartNetState> _netState = new(
            writePerm: NetworkVariableWritePermission.Server);

        private KartController  _kart;
        private InputHandler    _input;
        private ClientPrediction _prediction;
        private InterpolationSystem _interpolation;

        private uint _localTick;

        public override void OnNetworkSpawn()
        {
            _kart          = GetComponent<KartController>();
            _input         = GetComponent<InputHandler>();
            _prediction    = GetComponent<ClientPrediction>();
            _interpolation = GetComponent<InterpolationSystem>();

            if (!IsOwner)
            {
                // 非本地玩家：禁用物理，由插值系统驱动
                var rb = GetComponent<Rigidbody>();
                if (rb) rb.isKinematic = true;
                _interpolation.enabled = true;
                enabled = false; // 关闭本脚本 Update
            }
        }

        private void FixedUpdate()
        {
            if (!IsSpawned) return;

            if (IsOwner)
                OwnerFixedUpdate();
            // 非Owner已通过 InterpolationSystem 处理
        }

        // ─── 本地玩家（Owner）逻辑 ────────────────────────────────────────────

        private void OwnerFixedUpdate()
        {
            var input = _input.GetInput(_localTick);

            // 1. 本地立即执行（客户端预测）
            _kart.ApplyInput(input);

            // 2. 存入预测缓冲
            _prediction.StoreInput(input);
            _prediction.StoreState(_localTick, GetCurrentState());

            // 3. 发送输入到服务端
            SubmitInputServerRpc(input);

            _localTick++;
        }

        // ─── 服务端 RPC ───────────────────────────────────────────────────────

        [ServerRpc]
        private void SubmitInputServerRpc(KartInput input)
        {
            // 服务端执行物理
            _kart.ApplyInput(input);

            // 广播权威状态
            _netState.Value = GetCurrentState();
        }

        // ─── 权威状态接收（Owner 客户端纠错）─────────────────────────────────

        private void OnEnable()
        {
            _netState.OnValueChanged += OnAuthorativeStateReceived;
        }

        private void OnDisable()
        {
            _netState.OnValueChanged -= OnAuthorativeStateReceived;
        }

        private void OnAuthorativeStateReceived(KartNetState prev, KartNetState next)
        {
            if (!IsOwner) return;

            // 对比服务端状态与本地预测状态
            KartNetState predicted = _prediction.GetStoredState(next.Tick);
            float posError = Vector3.Distance(predicted.Position, next.Position);

            // 误差超过阈值才纠正（避免抖动）
            if (posError > 0.2f)
            {
                _prediction.Rollback(next);
                // 从权威状态重新推演到当前帧
                _prediction.Replay(_kart, _localTick);
            }
        }

        private KartNetState GetCurrentState()
        {
            var rb = GetComponent<Rigidbody>();
            return new KartNetState
            {
                Tick        = _localTick,
                Position    = transform.position,
                Rotation    = transform.rotation,
                Velocity    = rb != null ? rb.velocity : Vector3.zero,
                AngularVel  = rb != null ? rb.angularVelocity : Vector3.zero
            };
        }
    }

    /// <summary>
    /// 网络同步的车辆状态快照
    /// </summary>
    [System.Serializable]
    public struct KartNetState : INetworkSerializable
    {
        public uint       Tick;
        public Vector3    Position;
        public Quaternion Rotation;
        public Vector3    Velocity;
        public Vector3    AngularVel;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Tick);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
            serializer.SerializeValue(ref Velocity);
            serializer.SerializeValue(ref AngularVel);
        }
    }
}
