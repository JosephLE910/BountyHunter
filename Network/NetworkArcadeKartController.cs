using Unity.Netcode;
using UnityEngine;
using KartGame.KartSystems;

namespace BountyHunter.Network
{
    /// <summary>
    /// ArcadeKart 网络控制器（适配原版 Karting Microgame）
    ///
    /// 与 NetworkKartController 的区别：
    ///   NetworkKartController  → 基于 BountyHunter.Physics.KartController（自定义物理）
    ///   NetworkArcadeKartController → 基于 KartGame.KartSystems.ArcadeKart（原版模板）
    ///
    /// 同步模型：客户端权威（Owner 直接写 NetworkVariable）
    ///   优点：实现简单，本地输入无延迟，适合演示
    ///   缺点：无服务端验证，生产环境应改为服务端权威（见 NetworkKartController）
    ///
    /// 非 Owner 玩家处理：
    ///   1. 禁用 KeyboardInput，阻止本地输入驱动远端车
    ///   2. 将 Rigidbody 设为 kinematic，ArcadeKart 物理失效
    ///   3. InterpolationSystem 通过 transform.position/rotation 驱动平滑移动
    ///
    /// 弱网纠错（Owner 自身）：
    ///   收到其他玩家广播时不处理自己的状态；
    ///   若未来改为服务端权威，可在此加入位置纠错逻辑。
    /// </summary>
    [RequireComponent(typeof(ArcadeKart))]
    [RequireComponent(typeof(InterpolationSystem))]
    public class NetworkArcadeKartController : NetworkBehaviour
    {
        [Header("同步设置")]
        [Tooltip("状态发送频率（秒/次），建议 0.05 = 20次/秒")]
        public float SendRate = 0.05f;

        [Tooltip("位置纠错阈值（米），超过此值对 Owner 做瞬移纠正")]
        public float CorrectionThreshold = 1.0f;

        // Owner 写权限：本地玩家直接更新，无需经过服务端
        private readonly NetworkVariable<KartNetState> _netState = new(
            writePerm: NetworkVariableWritePermission.Owner);

        private ArcadeKart        _kart;
        private InterpolationSystem _interpolation;
        private float             _sendTimer;

        // ─── NGO 生命周期 ─────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            _kart          = GetComponent<ArcadeKart>();
            _interpolation = GetComponent<InterpolationSystem>();

            _netState.OnValueChanged += OnRemoteStateReceived;

            if (!IsOwner)
            {
                // 非本地玩家：禁用键盘输入，由插值系统驱动
                var keyInput = GetComponent<BaseInput>();
                if (keyInput != null) keyInput.enabled = false;

                var rb = _kart != null ? _kart.Rigidbody : GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;

                if (_interpolation != null) _interpolation.enabled = true;
            }
            else
            {
                // 本地玩家：关闭插值（自己不需要插值）
                if (_interpolation != null) _interpolation.enabled = false;
            }
        }

        public override void OnNetworkDespawn()
        {
            _netState.OnValueChanged -= OnRemoteStateReceived;
        }

        // ─── Owner 每帧发送状态 ───────────────────────────────────────────────

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsOwner) return;

            _sendTimer += Time.fixedDeltaTime;
            if (_sendTimer >= SendRate)
            {
                _sendTimer = 0f;
                _netState.Value = CaptureState();
            }
        }

        // ─── 接收远端状态 ─────────────────────────────────────────────────────

        private void OnRemoteStateReceived(KartNetState prev, KartNetState next)
        {
            // Owner 不处理自己写的状态
            if (IsOwner) return;

            _interpolation?.ReceiveState(next);
        }

        // ─── 快照采集 ─────────────────────────────────────────────────────────

        private KartNetState CaptureState()
        {
            var rb = _kart != null ? _kart.Rigidbody : GetComponent<Rigidbody>();
            return new KartNetState
            {
                // Tick 用物理帧序号估算，便于插值排序
                Tick       = (uint)(Time.fixedTime / Time.fixedDeltaTime),
                Position   = transform.position,
                Rotation   = transform.rotation,
                Velocity   = rb != null ? rb.velocity   : Vector3.zero,
                AngularVel = rb != null ? rb.angularVelocity : Vector3.zero
            };
        }
    }
}
