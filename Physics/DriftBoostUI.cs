using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KartGame.KartSystems;

namespace BountyHunter.Physics
{
    /// <summary>
    /// 漂移蓄力 + 涡轮加速 UI（TextMeshPro 版本）
    ///
    /// 使用方式：
    /// 1. 在已有 Canvas 下新建空物体，挂此脚本
    /// 2. SpeedText  → 复用场景中已有的 Speed TextMeshPro 组件（可选）
    /// 3. DriftLevelText / TurboText → 在 Canvas 下新建 TextMeshPro - Text(UI)
    /// 4. DriftChargeBar → 新建 Image，Image Type 设为 Filled / Horizontal
    /// </summary>
    public class DriftBoostUI : MonoBehaviour
    {
        [Header("目标车辆")]
        public ArcadeKart Kart;

        [Header("UI 元素（均为 TextMeshPro）")]
        public TextMeshProUGUI SpeedText;       // 速度数字（可复用已有 Speed Text）
        public TextMeshProUGUI DriftLevelText;  // 蓄力等级：Lv.1 / Lv.2 / Lv.3 PERFECT!
        public Image           DriftChargeBar;  // 进度条：Image Type = Filled, Horizontal
        public TextMeshProUGUI TurboText;       // "TURBO!" 闪烁提示

        // 各蓄力等级颜色
        static readonly Color[] LevelColors =
        {
            Color.white,                        // 0 = 无漂移
            new Color(0.4f, 0.7f, 1f),          // 1 = 蓝焰
            new Color(1f, 0.55f, 0.1f),         // 2 = 橙焰
            new Color(1f, 0.4f, 0.8f),          // 3 = 粉焰（完美）
        };

        static readonly string[] LevelNames = { "", "Lv.1", "Lv.2", "Lv.3  PERFECT!" };

        float _turboFlashTimer;
        bool  _turboVisible;

        void Update()
        {
            if (Kart == null) return;

            float speedMs  = Kart.Rigidbody != null ? Kart.Rigidbody.velocity.magnitude : 0f;
            float speedKph = speedMs * 3.6f;
            int   level    = Kart.DriftChargeLevel;

            // ── 速度 ──────────────────────────────────────────────────────
            if (SpeedText != null)
                SpeedText.text = $"{speedKph:F0} <size=60%>km/h</size>";

            // ── 蓄力等级文字 ───────────────────────────────────────────────
            if (DriftLevelText != null)
            {
                DriftLevelText.text    = Kart.IsDrifting ? LevelNames[level] : "";
                DriftLevelText.color   = LevelColors[level];
                // 等级提升时放大闪一下
                float targetScale = (Kart.IsDrifting && level > 0) ? 1f : 0.8f;
                DriftLevelText.transform.localScale = Vector3.Lerp(
                    DriftLevelText.transform.localScale,
                    Vector3.one * targetScale,
                    Time.deltaTime * 12f);
            }

            // ── 蓄力进度条 ─────────────────────────────────────────────────
            if (DriftChargeBar != null)
            {
                float fillTarget = Kart.IsDrifting
                    ? Mathf.Clamp01(level / 3f + (level < 3 ? 0.2f : 0f))
                    : 0f;
                DriftChargeBar.fillAmount = Mathf.Lerp(
                    DriftChargeBar.fillAmount, fillTarget, Time.deltaTime * 8f);
                DriftChargeBar.color = LevelColors[Mathf.Max(1, level)];
            }

            // ── 涡轮提示（直接读 ArcadeKart.TurboTimer，不再猜测）────────
            if (Kart.TurboTimer > 0f)
            {
                _turboFlashTimer -= Time.deltaTime;
                if (_turboFlashTimer <= 0f)
                {
                    _turboFlashTimer = 0.12f;
                    _turboVisible    = !_turboVisible;
                }
                if (TurboText != null)
                {
                    TurboText.enabled = _turboVisible;
                    TurboText.color   = LevelColors[Mathf.Clamp(Kart.TurboLevel, 1, 3)];
                }
            }
            else
            {
                _turboVisible = false;
                if (TurboText != null) TurboText.enabled = false;
            }
        }
    }
}
