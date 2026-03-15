using RimWorld;
using UnityEngine;
using Verse;
using CharacterStudio.Core;

namespace CharacterStudio.Rendering
{
    /// <summary>
    /// 面部渲染工人 - 驱动 Head 节点整张贴图的表情切换
    /// 当 PawnFaceConfig 启用时，根据当前表情状态切换头部贴图
    /// 挂载到 Head 节点或其自定义子图层上
    /// </summary>
    public class PawnRenderNodeWorker_FaceComponent : PawnRenderNodeWorker
    {
        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Vector2 originalDrawSize = node.Props.drawSize;
            node.Props.drawSize = Vector2.one;
            Vector3 baseScale;
            try
            {
                baseScale = base.ScaleFor(node, parms);
            }
            finally
            {
                node.Props.drawSize = originalDrawSize;
            }

            if (node is PawnRenderNode_Custom customNode && customNode.config != null)
            {
                Vector2 cfg = customNode.config.scale;
                float sx = cfg.x <= 0f ? 1f : cfg.x;
                float sy = cfg.y <= 0f ? 1f : cfg.y;
                baseScale = new Vector3(sx * baseScale.x, 1f, sy * baseScale.z);
            }

            if (node.debugScale != 1f)
                baseScale *= node.debugScale;

            return baseScale;
        }

        public override Quaternion RotationFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Quaternion baseRot = base.RotationFor(node, parms);

            if (Mathf.Abs(node.debugAngleOffset) > 0.01f)
                baseRot *= Quaternion.Euler(0f, node.debugAngleOffset, 0f);

            if (node is PawnRenderNode_Custom customNode && customNode.config != null)
            {
                float rot = customNode.config.rotation;
                if (Mathf.Abs(rot) > 0.01f)
                    baseRot *= Quaternion.Euler(0f, rot, 0f);
            }

            return baseRot;
        }

        /// <summary>
        /// 根据当前表情获取头部贴图
        /// 若 faceConfig 未启用或无对应表情，回退到原版渲染
        /// </summary>
        protected override Graphic? GetGraphic(PawnRenderNode node, PawnDrawParms parms)
        {
            var comp = parms.pawn.GetComp<CompPawnSkin>();
            var skin = comp?.ActiveSkin;

            if (skin?.faceConfig?.enabled == true)
            {
                ExpressionType exp = comp?.GetEffectiveExpression() ?? ExpressionType.Neutral;
                string path = skin.faceConfig.GetTexPath(exp);

                if (!string.IsNullOrEmpty(path))
                {
                    Shader shader = node.ShaderFor(parms.pawn) ?? ShaderDatabase.Cutout;
                    var props = node.Props;

                    bool isExternal = System.IO.Path.IsPathRooted(path);

                    if (isExternal)
                    {
                        var req = new GraphicRequest(
                            typeof(Graphic_Runtime), path, shader,
                            Vector2.one, props?.color ?? Color.white,
                            Color.white, null, 0, null, null);
                        var gr = new Graphic_Runtime();
                        gr.Init(req);
                        return gr;
                    }
                    else
                    {
                        // 游戏内路径：自动探测 Multi/Single
                        if (ContentFinder<Texture2D>.Get(path + "_north", false) != null)
                            return GraphicDatabase.Get<Graphic_Multi>(path, shader, Vector2.one, props?.color ?? Color.white);
                        else
                            return GraphicDatabase.Get<Graphic_Single>(path, shader, Vector2.one, props?.color ?? Color.white);
                    }
                }
            }

            return base.GetGraphic(node, parms);
        }

        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
            => base.CanDrawNow(node, parms);
    }
}
