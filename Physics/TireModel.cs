using UnityEngine;

namespace BountyHunter.Physics
{
    /// <summary>
    /// Pacejka "Magic Formula" 轮胎模型简化版
    ///
    /// 参考：《极品飞车：集结》采用写实物理，轮胎力基于滑移角驱动；
    /// 对比《QQ飞车》则大幅夸张横向抓地，降低漂移门槛以提升爽快感。
    ///
    /// Magic Formula: F = D * sin(C * arctan(B*slip - E*(B*slip - arctan(B*slip))))
    ///   B = Stiffness Factor
    ///   C = Shape Factor
    ///   D = Peak Value
    ///   E = Curvature Factor
    /// </summary>
    [System.Serializable]
    public class TireModel
    {
        [Header("Pacejka Coefficients (Lateral)")]
        public float B = 10f;   // 刚度系数：越大在小滑移角时力越强
        public float C = 1.9f;  // 形状系数：控制曲线形状
        public float D = 1.0f;  // 峰值系数：最大侧向力系数（乘以法向力得到力）
        public float E = 0.97f; // 曲率系数：影响峰值后的下降

        [Header("Longitudinal (Drive/Brake)")]
        public float LongitudinalB = 12f;
        public float LongitudinalC = 2.3f;
        public float LongitudinalD = 1.1f;
        public float LongitudinalE = 0.8f;

        /// <summary>
        /// 计算侧向力系数，输入滑移角（弧度）
        /// </summary>
        public float LateralForceCoeff(float slipAngleRad)
        {
            float slip = slipAngleRad * Mathf.Rad2Deg; // 转换为度（经验公式通常用度）
            return MagicFormula(slip, B, C, D, E);
        }

        /// <summary>
        /// 计算纵向力系数，输入纵向滑移率 (-1~1)
        /// </summary>
        public float LongitudinalForceCoeff(float slipRatio)
        {
            return MagicFormula(slipRatio * 100f, LongitudinalB, LongitudinalC, LongitudinalD, LongitudinalE);
        }

        private static float MagicFormula(float slip, float b, float c, float d, float e)
        {
            float bSlip = b * slip;
            return d * Mathf.Sin(c * Mathf.Atan(bSlip - e * (bSlip - Mathf.Atan(bSlip))));
        }

        /// <summary>
        /// 漂移模式：降低侧向峰值 + 展宽曲线，使大滑移角仍有持续推力
        /// 模拟《QQ飞车》漂移手感：摩擦力不会随滑移角增大而快速衰减
        /// </summary>
        public TireModel GetDriftVariant(float grip = 0.4f)
        {
            return new TireModel
            {
                B = B * 0.3f,
                C = C * 0.7f,
                D = D * grip,
                E = E,
                LongitudinalB = LongitudinalB,
                LongitudinalC = LongitudinalC,
                LongitudinalD = LongitudinalD,
                LongitudinalE = LongitudinalE
            };
        }
    }
}
