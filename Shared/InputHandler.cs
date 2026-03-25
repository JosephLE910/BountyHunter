using UnityEngine;
using Unity.Netcode;

namespace BountyHunter.Shared
{
    /// <summary>
    /// 统一输入处理，供物理控制器和网络层共用
    /// INetworkSerializeByMemcpy：结构体全为基础类型，直接内存拷贝序列化
    /// </summary>
    public struct KartInput : INetworkSerializeByMemcpy
    {
        public float Steering;   // -1 ~ 1
        public float Throttle;   // 0 ~ 1
        public float Brake;      // 0 ~ 1
        public bool  DriftHeld;
        public uint  Tick;       // 网络帧序号（用于客户端预测）
    }

    public class InputHandler : MonoBehaviour
    {
        public KartInput GetInput(uint tick = 0)
        {
            return new KartInput
            {
                Steering  = Input.GetAxis("Horizontal"),
                Throttle  = Mathf.Max(0f, Input.GetAxis("Vertical")),
                Brake     = Mathf.Max(0f, -Input.GetAxis("Vertical")),
                DriftHeld = Input.GetKey(KeyCode.Space),
                Tick      = tick
            };
        }
    }
}
