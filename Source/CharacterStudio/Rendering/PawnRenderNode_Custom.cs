using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.Rendering
{
    /// <summary>
    /// 自定义渲染节点
    /// 包含动画状态和图层配置
    /// </summary>
    public class PawnRenderNode_Custom : PawnRenderNode
    {
        // P1: 缓存 CompPawnSkin 引用（避免每帧每节点 50+ 次 TryGetComp O(N) 查找）
        private CompPawnSkin? _cachedSkinComp;
        private int _cachedSkinCompTick = -1;

        /// <summary>
        /// P1: 获取缓存的 CompPawnSkin 引用。每 tick 最多解析一次。
        /// </summary>
        public CompPawnSkin? GetCachedSkinComp()
        {
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            if (_cachedSkinCompTick == currentTick)
                return _cachedSkinComp;

            _cachedSkinComp = tree?.pawn?.TryGetComp<CompPawnSkin>();
            _cachedSkinCompTick = currentTick;
            return _cachedSkinComp;
        }

        /// <summary>P1: 使缓存失效（皮肤切换时调用）</summary>
        public void InvalidateSkinCompCache()
        {
            _cachedSkinCompTick = -1;
            _cachedSkinComp = null;
            _cachedCanDrawTick = -1;
            _hasVanillaAncestorState = -1;
        }

        // P-PERF: CanDrawNow 结果缓存（避免 ReplacementMouth/Mouth 每帧重复解析纹理路径）
        internal int _cachedCanDrawTick = -1;
        internal int _cachedCanDrawFacing = -1;
        internal bool _cachedCanDrawResult = true;

        /// <summary>P-PERF: 使 CanDrawNow 缓存失效</summary>
        public void InvalidateCanDrawCache()
        {
            _cachedCanDrawTick = -1;
        }

        /// <summary>关联的图层配置</summary>
        public PawnLayerConfig? config;

        /// <summary>
        /// 当该自定义节点用于 LayeredDynamic 面部系统时，标记当前节点所代表的面部部件类型。
        /// null 表示普通自定义图层节点。
        /// </summary>
        public LayeredFacePartType? layeredFacePartType;

        /// <summary>
        /// 当该节点代表 LayeredDynamic 左右独立部件时，记录当前节点对应的 side。
        /// None 表示旧版单节点部件或非左右分离部件。
        /// </summary>
        public LayeredFacePartSide layeredFacePartSide = LayeredFacePartSide.None;

        /// <summary>
        /// 当该节点代表 Overlay 分组时，记录对应的 Overlay 标识。
        /// 空字符串表示默认 Overlay 组或非 Overlay 节点。
        /// </summary>
        public string layeredOverlayId = string.Empty;

        /// <summary>
        /// Overlay 组的编辑器排序值。数值越小越先绘制。
        /// </summary>
        public int layeredOverlayOrder = 0;

        // ─────────────────────────────────────────────
        // 动画状态
        // ─────────────────────────────────────────────
        
        /// <summary>下次抽动开始的游戏Tick</summary>
        public int nextTwitchTick = -1;
        
        /// <summary>是否正在播放抽动动画</summary>
        public bool isTwitching = false;
        
        /// <summary>抽动开始时的游戏Tick</summary>
        public int twitchStartTick = 0;
        
        /// <summary>当前动画产生的额外旋转角度</summary>
        public float currentAnimAngle = 0f;

        /// <summary>Spin 模式下的当前累积旋转角度（度，持续增加）</summary>
        public float currentSpinAngle = 0f;

        /// <summary>Spin 模式下上一次更新的游戏 Tick</summary>
        public int lastSpinTick = -1;

        /// <summary>Brownian 模式下上次更新 Tick</summary>
        public int lastBrownianTick = -1;

        /// <summary>Brownian 模式下当前速度</summary>
        public Vector3 currentBrownianVelocity = Vector3.zero;

        /// <summary>Brownian 启停混合因子（0=完全停用，1=完全启用），用于平滑插值</summary>
        public float brownianBlendFactor = 0f;

        /// <summary>Brownian 原始偏移（不含 blend 衰减），由物理模拟直接产出</summary>
        public Vector3 brownianRawOffset = Vector3.zero;

        /// <summary>Brownian 经过 blend 插值后的输出偏移，供渲染使用</summary>
        public Vector3 brownianSmoothedOffset = Vector3.zero;

        /// <summary>当前动画产生的位移偏移</summary>
        public Vector3 currentAnimOffset = Vector3.zero;

        /// <summary>当前动画产生的缩放系数</summary>
        public float currentAnimScale = 1f;

        /// <summary>动画基础相位（用于错开不同图层）</summary>
        public float basePhase = 0f;

        /// <summary>面部分层程序化变形上次更新 Tick</summary>
        public int lastProgrammaticFaceTick = int.MinValue;
        public int lastAnimCalcTick = int.MinValue;

        /// <summary>程序化表情附加旋转角</summary>
        public float currentProgrammaticAngle = 0f;

        /// <summary>程序化表情附加位移</summary>
        public Vector3 currentProgrammaticOffset = Vector3.zero;

        /// <summary>程序化表情附加缩放</summary>
        public Vector3 currentProgrammaticScale = Vector3.one;

        /// <summary>程序化表情当前透明度</summary>
        public float currentProgrammaticAlpha = 1f;

        /// <summary>程序化表情目标透明度</summary>
        public float targetProgrammaticAlpha = 1f;

        /// <summary>程序化表情透明度是否已完成首次同步</summary>
        public bool hasProgrammaticAlphaInitialized = false;

        /// <summary>外部图层动画上次更新 Tick</summary>
        public int lastExternalLayerAnimationTick = int.MinValue;

        /// <summary>外部图层动画附加旋转</summary>
        public float currentExternalLayerAngle = 0f;

        /// <summary>外部图层动画附加位移</summary>
        public Vector3 currentExternalLayerOffset = Vector3.zero;

        /// <summary>外部图层动画附加缩放乘数</summary>
        public Vector3 currentExternalLayerScale = Vector3.one;

        // ─────────────────────────────────────────────
        // P-PERF: HasAnyVanillaAncestor 结果缓存
        // 该方法每次渲染调用都会走 parent 链，多节点场景下开销显著。
        // 树结构仅在 Init / SetDirty 时重建，之后 parent 链不变，
        // 因此在节点首次访问时缓存结果即可。
        // ─────────────────────────────────────────────
        internal int _hasVanillaAncestorState = -1; // -1=未计算, 0=false, 1=true

        /// <summary>P-PERF: 使祖先缓存失效（树结构重建时调用）</summary>
        public void InvalidateAncestorCache()
        {
            _hasVanillaAncestorState = -1;
        }

        // P8: 节点级图形缓存（避免 GetGraphic 每帧完整重算）
        // 当 tick 和 facing 未变时直接返回缓存的 Graphic，跳过
        // 完整的路径解析、TextureExists 查找和 GraphicDatabase.Get。
        // ─────────────────────────────────────────────
        internal Graphic? _cachedGraphicResult;
        internal bool _cachedGraphicHasResult;       // true = 有缓存结果（包括 null）
        internal int _cachedGraphicTick = -1;
        internal int _cachedGraphicFacing = -1;

        /// <summary>P8: 使节点级图形缓存失效（皮肤切换/表情变更时调用）</summary>
        public void InvalidateGraphicCache()
        {
            _cachedGraphicHasResult = false;
            _cachedGraphicResult = null;
            _cachedGraphicTick = -1;
            _hasVanillaAncestorState = -1;
        }

        public PawnRenderNode_Custom(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        public override GraphicMeshSet MeshSetFor(Pawn pawn)
        {
            if (this.Props.overrideMeshSize.HasValue)
            {
                Vector2 meshSize = this.Props.overrideMeshSize.Value;
                return MeshPool.GetMeshSetForSize(meshSize.x, meshSize.y);
            }

            if (pawn?.RaceProps?.Humanlike ?? false)
                return base.MeshSetFor(pawn);

            Vector2? bodyGraphicSize = pawn?.Drawer?.renderer?.BodyGraphic != null
                ? pawn.Drawer.renderer.BodyGraphic.drawSize
                : (Vector2?)null;

            if (bodyGraphicSize.HasValue && bodyGraphicSize.Value.x > 0f && bodyGraphicSize.Value.y > 0f)
                return MeshPool.GetMeshSetForSize(bodyGraphicSize.Value.x, bodyGraphicSize.Value.y);

            Graphic? graphic = this.GraphicFor(pawn);
            if (graphic != null && graphic.drawSize.x > 0f && graphic.drawSize.y > 0f)
                return MeshPool.GetMeshSetForSize(graphic.drawSize.x, graphic.drawSize.y);

            return base.MeshSetFor(pawn);
        }
    }
}