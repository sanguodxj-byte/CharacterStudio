// ─────────────────────────────────────────────
// 空间线状/墙状贴图专用 Mote
//
// 原版 Mote 使用 Graphic_Mote.DrawMoteInternal 渲染，
// 该路径会消费 exactRotation + ExactScale(linearScale) 构建 TRS 矩阵。
// 但外部纹理（Graphic_Runtime → Graphic_Single）不继承 Graphic_Mote，
// 导致 linearScale 和 exactRotation 被忽略。
//
// 本类仿照 CombatExtended 的 MoteThrownCE 模式，
// 直接覆写 DrawAt，自行构建 Matrix4x4.TRS + Graphics.DrawMesh，
// 确保 exactRotation / linearScale 对所有 Graphic 类型生效。
// ─────────────────────────────────────────────

using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 支持精确旋转和非均匀缩放的线状/墙状贴图 Mote。
    /// 绕过原版 Graphic 渲染路径，直接用 Graphics.DrawMesh 绘制。
    /// </summary>
    [StaticConstructorOnStartup]
    public class Mote_SpatialLine : MoteThrown
    {
        /// <summary>
        /// 本段开始可见的游戏 Tick。用于按段推进显示。
        /// </summary>
        public int visibleFromTick;

        /// <summary>
        /// 每个实例独立的 MaterialPropertyBlock，用于设置颜色而不污染共享材质。
        /// </summary>
        private static readonly MaterialPropertyBlock SharedPropertyBlock = new MaterialPropertyBlock();

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            int nowTick = Find.TickManager?.TicksGame ?? 0;
            if (visibleFromTick > 0 && nowTick < visibleFromTick)
            {
                return;
            }

            float alpha = Alpha;
            if (alpha <= 0f)
            {
                return;
            }

            Color drawColor = DrawColor;
            drawColor.a *= alpha;

            // 从 def.graphicData 获取基础 drawSize，乘以 linearScale 得到最终缩放
            Vector2 baseDrawSize = def.graphicData?.drawSize ?? Vector2.one;
            Vector3 finalScale = new Vector3(
                baseDrawSize.x * linearScale.x,
                1f,
                baseDrawSize.y * linearScale.z);

            Matrix4x4 matrix = default;
            matrix.SetTRS(
                drawLoc,
                Quaternion.AngleAxis(exactRotation, Vector3.up),
                finalScale);

            Material mat = Graphic.MatSingle;

            if (mat != null)
            {
                SharedPropertyBlock.Clear();
                SharedPropertyBlock.SetColor(ShaderPropertyIDs.Color, drawColor);
                Graphics.DrawMesh(MeshPool.plane10, matrix, mat, 0, null, 0, SharedPropertyBlock);
            }
            else
            {
                Graphics.DrawMesh(MeshPool.plane10, matrix, mat, 0);
            }
        }
    }
}
