using System.Collections.Generic;
using Verse;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 眼睛注视方向枚举
    /// 对应 RimWorld 等距视角下可感知到的 5 个方向
    /// </summary>
    public enum EyeDirection
    {
        Center, // 正视前方（默认回退）
        Left,   // 向左注视
        Right,  // 向右注视
        Up,     // 向上注视（背向摄像机时）
        Down,   // 向下注视
    }

    /// <summary>
    /// 眼睛方向贴图配置
    /// 为每个方向指定一张眼睛覆盖贴图，随 Pawn 朝向自动切换。
    ///
    /// 渲染方式（方案 A）：
    ///   以额外覆盖层叠加在头部节点上，贴图替换实现方向感。
    ///   依赖现有 ExpressionType 驱动框架，不引入程序偏移。
    /// </summary>
    public class PawnEyeDirectionConfig
    {
        /// <summary>是否启用眼睛方向功能</summary>
        public bool enabled = false;

        /// <summary>正视前方贴图路径（Center，默认回退）</summary>
        public string texCenter = "";

        /// <summary>向左注视贴图路径</summary>
        public string texLeft = "";

        /// <summary>向右注视贴图路径</summary>
        public string texRight = "";

        /// <summary>向上注视贴图路径（Pawn 面朝北时）</summary>
        public string texUp = "";

        /// <summary>向下注视贴图路径</summary>
        public string texDown = "";

        /// <summary>
        /// UV 偏移幅度（用于代码驱动瞳孔偏移，0 = 关闭，使用贴图替换方案）
        /// 取值范围建议 0.02 ~ 0.10（超出1会采样到纹理外部）。
        /// 正值 = 开启程序驱动：无需 texLeft/Right/Up/Down 贴图，
        /// 在同一张眼睛贴图上通过偏移 UV 采样坐标模拟瞳孔移动。
        /// 偏移方向映射：
        ///   Left  → UV.x += range（采样左侧，瞳孔偏左）
        ///   Right → UV.x -= range（采样右侧，瞳孔偏右）
        ///   Up    → UV.y -= range（采样上侧，瞳孔偏上）
        ///   Down  → UV.y += range（采样下侧，瞳孔偏下）
        /// </summary>
        public float pupilMoveRange = 0f;

        /// <summary>
        /// 分层模式下，上眼睑在触发替换式眼部表情时的下移量。
        /// 0 = 不额外移动；建议值 0.002 ~ 0.008。
        /// 仅影响 LayeredDynamic 中的 UpperLid 程序位移。
        /// </summary>
        public float upperLidMoveDown = 0.0044f;

        // ─────────────────────────────────────────────
        // 查询 API
        // ─────────────────────────────────────────────

        /// <summary>
        /// 获取指定方向对应的贴图路径。
        /// 若目标方向未配置贴图，自动回退到 Center；
        /// Center 也未配置则返回空字符串。
        /// </summary>
        public string GetTexPath(EyeDirection direction)
        {
            string? path = direction switch
            {
                EyeDirection.Left  => texLeft,
                EyeDirection.Right => texRight,
                EyeDirection.Up    => texUp,
                EyeDirection.Down  => texDown,
                _                  => texCenter
            };

            // 未配置时回退到 Center
            if (string.IsNullOrEmpty(path) && direction != EyeDirection.Center)
                path = texCenter;

            return path ?? string.Empty;
        }

        /// <summary>是否配置了任意一张眼睛贴图</summary>
        public bool HasAnyTex()
            => !string.IsNullOrEmpty(texCenter)
            || !string.IsNullOrEmpty(texLeft)
            || !string.IsNullOrEmpty(texRight)
            || !string.IsNullOrEmpty(texUp)
            || !string.IsNullOrEmpty(texDown);

        // ─────────────────────────────────────────────
        // 克隆
        // ─────────────────────────────────────────────

        public PawnEyeDirectionConfig Clone() => new PawnEyeDirectionConfig
        {
            enabled          = this.enabled,
            texCenter        = this.texCenter,
            texLeft          = this.texLeft,
            texRight         = this.texRight,
            texUp            = this.texUp,
            texDown          = this.texDown,
            pupilMoveRange   = this.pupilMoveRange,
            upperLidMoveDown = this.upperLidMoveDown,
        };
    }
}
