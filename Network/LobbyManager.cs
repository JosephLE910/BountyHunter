using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BountyHunter.Network
{
    /// <summary>
    /// 简单多人房间管理器
    ///
    /// 功能：Host / Client 两种模式，IP 直连（Relay 可后续接入）
    ///
    /// 使用方式：
    /// 1. 在网络测试场景的 Canvas 下新建空物体，挂此脚本
    /// 2. 配置 HostButton / JoinButton / IpField / StatusText
    /// 3. 将场景中的 KartPrefab（挂有 NetworkObject + NetworkKartController）
    ///    拖入 NetworkManager 的 Player Prefab 槽
    ///
    /// 测试方法（本机双开）：
    /// - 在 Unity Editor 中点 Host 启动服务端+本地玩家
    /// - 用 Build 出的 exe 或第二个 Editor（ParrelSync）点 Join
    /// - 默认连接 127.0.0.1:7777
    /// </summary>
    public class LobbyManager : MonoBehaviour
    {
        [Header("UI")]
        public Button          HostButton;
        public Button          JoinButton;
        public TMP_InputField  IpField;
        public TextMeshProUGUI StatusText;

        [Header("网络设置")]
        [Tooltip("默认连接 IP，本机测试填 127.0.0.1")]
        public string DefaultIP = "127.0.0.1";
        [Tooltip("端口号，与 NetworkManager UTP Transport 保持一致")]
        public ushort Port      = 7777;

        private void Start()
        {
            if (IpField != null)
                IpField.text = DefaultIP;

            HostButton?.onClick.AddListener(StartHost);
            JoinButton?.onClick.AddListener(StartClient);

            NetworkManager.Singleton.OnClientConnectedCallback  += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton == null) return;
            NetworkManager.Singleton.OnClientConnectedCallback  -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        // ─── Host（服务端 + 本地玩家）────────────────────────────────────────

        private void StartHost()
        {
            SetTransportAddress(DefaultIP);
            NetworkManager.Singleton.StartHost();
            SetStatus("已启动 Host，等待玩家加入...");
            SetButtonsInteractable(false);
        }

        // ─── Client（纯客户端）───────────────────────────────────────────────

        private void StartClient()
        {
            string ip = IpField != null && !string.IsNullOrEmpty(IpField.text)
                ? IpField.text
                : DefaultIP;

            SetTransportAddress(ip);
            NetworkManager.Singleton.StartClient();
            SetStatus($"正在连接 {ip}:{Port}...");
            SetButtonsInteractable(false);
        }

        // ─── 回调 ────────────────────────────────────────────────────────────

        private void OnClientConnected(ulong clientId)
        {
            if (NetworkManager.Singleton.IsServer)
                SetStatus($"玩家加入 (id={clientId})，当前人数: {NetworkManager.Singleton.ConnectedClients.Count}");
            else
                SetStatus("连接成功！");
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (NetworkManager.Singleton.IsServer)
                SetStatus($"玩家离开 (id={clientId})，当前人数: {NetworkManager.Singleton.ConnectedClients.Count}");
            else
            {
                SetStatus("已断开连接");
                SetButtonsInteractable(true);
            }
        }

        // ─── 工具 ────────────────────────────────────────────────────────────

        private void SetTransportAddress(string ip)
        {
            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (utp != null)
                utp.SetConnectionData(ip, Port);
        }

        private void SetStatus(string msg)
        {
            if (StatusText != null)
                StatusText.text = msg;
            Debug.Log($"[LobbyManager] {msg}");
        }

        private void SetButtonsInteractable(bool value)
        {
            if (HostButton != null) HostButton.interactable = value;
            if (JoinButton != null) JoinButton.interactable = value;
        }
    }
}
