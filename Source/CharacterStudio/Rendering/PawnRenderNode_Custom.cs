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
        /// <summary>关联的图层配置</summary>
        public PawnLayerConfig? config;

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

        /// <summary>当前动画产生的位移偏移</summary>
        public Vector3 currentAnimOffset = Vector3.zero;

        /// <summary>当前动画产生的缩放系数</summary>
        public float currentAnimScale = 1f;

        /// <summary>动画基础相位（用于错开不同图层）</summary>
        public float basePhase = 0f;

        public PawnRenderNode_Custom(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }
    }
}
