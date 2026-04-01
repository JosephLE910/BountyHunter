using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;
using BountyHunter.Network;

namespace BountyHunter.Editor
{
    /// <summary>
    /// BountyHunter 场景自动搭建工具
    /// 菜单：BountyHunter / Setup Network Manager
    ///       BountyHunter / Setup Lobby UI
    ///       BountyHunter / Setup Kart Network Components
    ///       BountyHunter / Setup Drift Boost UI
    /// </summary>
    public static class BountyHunterSetup
    {
        // ─── 1. NetworkManager ────────────────────────────────────────────────

        [MenuItem("BountyHunter/Setup Network Manager")]
        public static void SetupNetworkManager()
        {
            if (Object.FindObjectOfType<NetworkManager>() != null)
            {
                EditorUtility.DisplayDialog("已存在", "场景中已有 NetworkManager，无需重复创建。", "OK");
                return;
            }

            var go = new GameObject("NetworkManager");
            var nm = go.AddComponent<NetworkManager>();
            var utp = go.AddComponent<UnityTransport>();

            // 绑定 Transport
            nm.NetworkConfig = new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = utp;

            // 默认端口 7777
            utp.SetConnectionData("127.0.0.1", 7777);

            Undo.RegisterCreatedObjectUndo(go, "Create NetworkManager");
            Selection.activeGameObject = go;

            Debug.Log("[BountyHunter] NetworkManager 创建完成。请将 KartPrefab 拖入 NetworkManager → Player Prefab 槽。");
        }

        // ─── 2. Lobby UI ──────────────────────────────────────────────────────

        [MenuItem("BountyHunter/Setup Lobby UI")]
        public static void SetupLobbyUI()
        {
            // 找或创建 Canvas
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                var cgo = new GameObject("Canvas");
                canvas = cgo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                cgo.AddComponent<CanvasScaler>();
                cgo.AddComponent<GraphicRaycaster>();
                Undo.RegisterCreatedObjectUndo(cgo, "Create Canvas");
            }

            // 根面板（半透明黑底，左上角）
            var panel = CreateUIObject("LobbyPanel", canvas.transform);
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0f, 0f, 0f, 0.75f);
            SetRect(panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -10), new Vector2(280, 180));

            // Host 按钮
            var hostBtn = CreateButton("HostButton", panel.transform, "Host（主机）",
                new Vector2(10, -10), new Vector2(260, 45));

            // Join 按钮
            var joinBtn = CreateButton("JoinButton", panel.transform, "Join（加入）",
                new Vector2(10, -65), new Vector2(260, 45));

            // IP 输入框
            var ipField = CreateInputField("IpField", panel.transform, "127.0.0.1",
                new Vector2(10, -120), new Vector2(260, 35));

            // 状态文字
            var statusGo = CreateUIObject("StatusText", panel.transform);
            var statusTmp = statusGo.AddComponent<TextMeshProUGUI>();
            statusTmp.text = "未连接";
            statusTmp.fontSize = 14;
            statusTmp.color = Color.white;
            statusTmp.alignment = TextAlignmentOptions.Center;
            SetRect(statusGo, new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -160), new Vector2(-10, 20));

            // LobbyManager 脚本
            var lobbyMgr = panel.AddComponent<LobbyManager>();
            lobbyMgr.HostButton = hostBtn.GetComponent<Button>();
            lobbyMgr.JoinButton = joinBtn.GetComponent<Button>();
            lobbyMgr.IpField    = ipField.GetComponent<TMP_InputField>();
            lobbyMgr.StatusText = statusTmp;

            Undo.RegisterCreatedObjectUndo(panel, "Create Lobby UI");
            Selection.activeGameObject = panel;

            Debug.Log("[BountyHunter] Lobby UI 创建完成，LobbyManager 槽位已自动连接。");
        }

        // ─── 3. 给已有 Kart 添加网络组件 ─────────────────────────────────────

        [MenuItem("BountyHunter/Setup Kart Network Components")]
        public static void SetupKartNetworkComponents()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("请先选择", "请在 Hierarchy 中选中 Kart 对象后再执行此操作。", "OK");
                return;
            }

            bool changed = false;

            if (selected.GetComponent<NetworkObject>() == null)
            { selected.AddComponent<NetworkObject>(); changed = true; }

            if (selected.GetComponent<BountyHunter.Shared.InputHandler>() == null)
            { selected.AddComponent<BountyHunter.Shared.InputHandler>(); changed = true; }

            if (selected.GetComponent<ClientPrediction>() == null)
            { selected.AddComponent<ClientPrediction>(); changed = true; }

            var interp = selected.GetComponent<InterpolationSystem>();
            if (interp == null)
            { interp = selected.AddComponent<InterpolationSystem>(); changed = true; }
            interp.enabled = false; // 默认关闭，非 Owner 时自动开启

            if (selected.GetComponent<NetworkKartController>() == null)
            { selected.AddComponent<NetworkKartController>(); changed = true; }

            if (selected.GetComponent<LagCompensator>() == null)
            { selected.AddComponent<LagCompensator>(); changed = true; }

            if (changed)
            {
                Undo.RegisterFullObjectHierarchyUndo(selected, "Add Kart Network Components");
                Debug.Log($"[BountyHunter] 已为 {selected.name} 添加网络组件。\n请将此对象拖到 Project 面板生成 Prefab，再拖入 NetworkManager → Player Prefab 槽。");
            }
            else
            {
                Debug.Log($"[BountyHunter] {selected.name} 的网络组件已全部存在，无需重复添加。");
            }
        }

        // ─── 4. Drift Boost UI 仪表盘 ────────────────────────────────────────

        [MenuItem("BountyHunter/Setup Drift Boost UI")]
        public static void SetupDriftBoostUI()
        {
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("找不到 Canvas", "场景中没有 Canvas，请先创建或运行 Setup Lobby UI。", "OK");
                return;
            }

            // 底部仪表盘面板
            var dashboard = CreateUIObject("DriftBoostUI", canvas.transform);
            dashboard.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            // 锚定底部拉伸
            var dashRect = dashboard.GetComponent<RectTransform>();
            dashRect.anchorMin = new Vector2(0, 0);
            dashRect.anchorMax = new Vector2(1, 0);
            dashRect.pivot     = new Vector2(0.5f, 0);
            dashRect.offsetMin = new Vector2(0, 0);
            dashRect.offsetMax = new Vector2(0, 80);

            // DriftLevelText（居中）
            var levelGo  = CreateUIObject("DriftLevelText", dashboard.transform);
            var levelTmp = levelGo.AddComponent<TextMeshProUGUI>();
            levelTmp.text      = "";
            levelTmp.fontSize  = 28;
            levelTmp.alignment = TextAlignmentOptions.Center;
            levelTmp.color     = Color.white;
            SetRect(levelGo, new Vector2(0.3f, 0.5f), new Vector2(0.7f, 1f),
                Vector2.zero, Vector2.zero);

            // DriftChargeBar（中下）
            var barGo  = CreateUIObject("DriftChargeBar", dashboard.transform);
            var barImg = barGo.AddComponent<Image>();
            barImg.color     = new Color(0.4f, 0.7f, 1f);
            barImg.type      = Image.Type.Filled;
            barImg.fillMethod = Image.FillMethod.Horizontal;
            barImg.fillAmount = 0f;
            SetRect(barGo, new Vector2(0.2f, 0f), new Vector2(0.8f, 0.45f),
                new Vector2(0, 8), new Vector2(0, -8));

            // TurboText（右侧）
            var turboGo  = CreateUIObject("TurboText", dashboard.transform);
            var turboTmp = turboGo.AddComponent<TextMeshProUGUI>();
            turboTmp.text      = "TURBO!";
            turboTmp.fontSize  = 32;
            turboTmp.fontStyle = FontStyles.Bold;
            turboTmp.alignment = TextAlignmentOptions.Center;
            turboTmp.color     = new Color(1f, 0.4f, 0.8f);
            turboTmp.enabled   = false;
            SetRect(turboGo, new Vector2(0.7f, 0f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero);

            // 挂 DriftBoostUI 脚本并连接子物体槽
            var driftUI = dashboard.AddComponent<BountyHunter.Physics.DriftBoostUI>();
            driftUI.DriftLevelText = levelTmp;
            driftUI.DriftChargeBar = barImg;
            driftUI.TurboText      = turboTmp;
            // SpeedText 和 Kart 需手动连接（依赖场景中具体对象）

            Undo.RegisterCreatedObjectUndo(dashboard, "Create DriftBoostUI");
            Selection.activeGameObject = dashboard;

            Debug.Log("[BountyHunter] DriftBoostUI 仪表盘创建完成。\n请手动将 Kart 和 SpeedText 拖入 Inspector 对应槽。");
        }

        // ─── 辅助方法 ─────────────────────────────────────────────────────────

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        private static GameObject CreateButton(string name, Transform parent, string label,
            Vector2 anchoredPos, Vector2 size)
        {
            var go  = CreateUIObject(name, parent);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.35f, 0.35f, 0.35f);
            btn.colors = colors;

            SetRectAbsolute(go, anchoredPos, size);

            // 文字子物体
            var textGo  = CreateUIObject("Text", go.transform);
            var tmp     = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text      = label;
            tmp.fontSize  = 18;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = Color.white;
            var tr = textGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;

            return go;
        }

        private static GameObject CreateInputField(string name, Transform parent, string placeholder,
            Vector2 anchoredPos, Vector2 size)
        {
            var go  = CreateUIObject(name, parent);
            go.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 1f);
            var field = go.AddComponent<TMP_InputField>();
            SetRectAbsolute(go, anchoredPos, size);

            // Text Area
            var areaGo = CreateUIObject("Text Area", go.transform);
            var areaRect = areaGo.GetComponent<RectTransform>();
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = new Vector2(8, 4);
            areaRect.offsetMax = new Vector2(-8, -4);

            // Placeholder
            var phGo  = CreateUIObject("Placeholder", areaGo.transform);
            var phTmp = phGo.AddComponent<TextMeshProUGUI>();
            phTmp.text    = placeholder;
            phTmp.color   = new Color(0.5f, 0.5f, 0.5f);
            phTmp.fontSize = 16;
            FillParent(phGo);

            // Input text
            var txtGo  = CreateUIObject("Text", areaGo.transform);
            var txtTmp = txtGo.AddComponent<TextMeshProUGUI>();
            txtTmp.color    = Color.white;
            txtTmp.fontSize = 16;
            FillParent(txtGo);

            field.textViewport   = areaRect;
            field.textComponent  = txtTmp;
            field.placeholder    = phTmp;
            field.text           = placeholder;

            return go;
        }

        private static void SetRect(GameObject go, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        private static void SetRectAbsolute(GameObject go, Vector2 anchoredPos, Vector2 size)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin       = new Vector2(0, 1);
            rt.anchorMax       = new Vector2(0, 1);
            rt.pivot           = new Vector2(0, 1);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta       = size;
        }

        private static void FillParent(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
