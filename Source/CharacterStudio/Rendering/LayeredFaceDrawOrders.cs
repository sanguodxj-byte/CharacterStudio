using UnityEngine;

namespace CharacterStudio.Rendering
{
    /// <summary>
    /// 分层面部渲染层级的 DrawOrder 常量。
    /// 集中管理所有面部部件的渲染顺序值，避免散落在各处的硬编码魔数。
    /// 
    /// 层级规则：
    /// - 基础面部层 (0.05 ~ 0.20)：用于正常渲染队列
    /// - 高优先级面部层 (50.05+)：用于需要在普通服装之上渲染的场景
    /// - 同类部件之间间隔 0.002~0.01，确保稳定排序
    /// - overlayOrder 偏移量为 0.0002，支持同一类型多个实例的微调
    /// </summary>
    public static class LayeredFaceDrawOrders
    {
        // ─── 基础面部层（正常渲染队列） ───

        /// <summary>头体基础层</summary>
        public const float HeadBase = 0.05f;

        /// <summary>头发层（后部）</summary>
        public const float HairBack = 0.201f;

        /// <summary>头发层（默认/前部）</summary>
        public const float HairDefault = 0.155f;

        /// <summary>眼睛基础层</summary>
        public const float Eye = 0.118f;

        /// <summary>瞳孔层</summary>
        public const float Pupil = 0.128f;

        /// <summary>上眼睑层</summary>
        public const float UpperLid = 0.136f;

        /// <summary>下眼睑层</summary>
        public const float LowerLid = 0.138f;

        /// <summary>替换眼层（闭眼/特殊眼）</summary>
        public const float ReplacementEye = 0.139f;

        /// <summary>眉毛层</summary>
        public const float Brow = 0.16f;

        /// <summary>嘴部层</summary>
        public const float Mouth = 0.18f;

        // ─── 情绪覆盖层（正常渲染队列） ───

        /// <summary>脸红层</summary>
        public const float Blush = 0.142f;

        /// <summary>泪水层</summary>
        public const float Tear = 0.144f;

        /// <summary>汗珠层</summary>
        public const float Sweat = 0.146f;

        /// <summary>睡眠覆盖层</summary>
        public const float SleepOverlay = 0.148f;

        /// <summary>通用覆盖层默认值</summary>
        public const float OverlayDefault = 0.149f;

        /// <summary>覆盖层 overlayOrder 步进值</summary>
        public const float OverlayOrderStep = 0.0002f;

        /// <summary>覆盖层顶部基准值</summary>
        public const float OverlayTop = 0.250f;

        /// <summary>覆盖层顶部 overlayOrder 步进值</summary>
        public const float OverlayTopOrderStep = 0.0002f;

        // ─── 可编辑覆盖层（编辑器用，与基础层略有偏移以区分迁移） ───

        /// <summary>[可编辑] 脸红层</summary>
        public const float Editable_Blush = 0.146f;

        /// <summary>[可编辑] 泪水层</summary>
        public const float Editable_Tear = 0.148f;

        /// <summary>[可编辑] 汗珠层</summary>
        public const float Editable_Sweat = 0.150f;

        /// <summary>[可编辑] 睡眠覆盖层</summary>
        public const float Editable_SleepOverlay = 0.152f;

        /// <summary>[可编辑] 通用覆盖层默认值</summary>
        public const float Editable_OverlayDefault = 0.154f;

        // ─── 高优先级面部层（50+ 渲染队列，在服装之上） ───

        /// <summary>[高优先级] 脸红层</summary>
        public const float HighPriority_Blush = 50.142f;

        /// <summary>[高优先级] 泪水层</summary>
        public const float HighPriority_Tear = 50.144f;

        /// <summary>[高优先级] 汗珠层</summary>
        public const float HighPriority_Sweat = 50.146f;

        /// <summary>[高优先级] 睡眠覆盖层</summary>
        public const float HighPriority_SleepOverlay = 50.148f;

        /// <summary>[高优先级] 通用覆盖层默认值</summary>
        public const float HighPriority_OverlayDefault = 50.149f;

        // ─── 高优先级覆盖层顶部（编辑器特殊渲染） ───

        /// <summary>[高优先级顶部] 脸红层</summary>
        public const float HighPriorityTop_Blush = 50.22f;

        /// <summary>[高优先级顶部] 泪水层</summary>
        public const float HighPriorityTop_Tear = 50.24f;

        /// <summary>[高优先级顶部] 汗珠层</summary>
        public const float HighPriorityTop_Sweat = 50.26f;

        /// <summary>[高优先级顶部] 睡眠覆盖层</summary>
        public const float HighPriorityTop_SleepOverlay = 50.28f;

        /// <summary>[高优先级顶部] 通用覆盖层默认值</summary>
        public const float HighPriorityTop_OverlayDefault = 50.30f;

        /// <summary>[高优先级顶部] overlayOrder 步进值</summary>
        public const float HighPriorityTopOrderStep = 0.002f;

        // ─── 其他渲染常量 ───

        /// <summary>默认面部层（未匹配时的回退值）</summary>
        public const float Default = 0.20f;

        /// <summary>眼睛方向节点基础层</summary>
        public const float EyeDirectionBaseLayer = 0.001f;

        /// <summary>DrawOrder 比较容差</summary>
        public const float DrawOrderEpsilon = 0.0001f;
    }
}