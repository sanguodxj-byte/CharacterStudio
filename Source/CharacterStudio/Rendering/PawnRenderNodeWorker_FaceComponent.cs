using RimWorld;
using UnityEngine;
using Verse;
using CharacterStudio.Core;

namespace CharacterStudio.Rendering
{
    /// <summary>
    /// 面部组件渲染工人 - 实现动态表情贴图切换
    /// </summary>
    public class PawnRenderNodeWorker_FaceComponent : PawnRenderNodeWorker
    {
        /// <summary>
        /// 编辑器中统一由 ScaleFor 处理缩放，避免 GetGraphic(drawSize) 与矩阵缩放叠加造成二次缩放。
        /// </summary>
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
            {
                baseScale *= node.debugScale;
            }

            return baseScale;
        }

        /// <summary>
        /// 旋转处理：支持图层配置中的固定旋转角
        /// </summary>
        public override Quaternion RotationFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Quaternion baseRot = base.RotationFor(node, parms);

            if (Mathf.Abs(node.debugAngleOffset) > 0.01f)
            {
                baseRot *= Quaternion.Euler(0f, node.debugAngleOffset, 0f);
            }

            if (node is PawnRenderNode_Custom customNode && customNode.config != null)
            {
                float rot = customNode.config.rotation;
                if (Mathf.Abs(rot) > 0.01f)
                {
                    baseRot *= Quaternion.Euler(0f, rot, 0f);
                }
            }

            return baseRot;
        }

        /// <summary>
        /// 获取图形 - 根据表情状态动态切换贴图
        /// </summary>
        protected override Graphic? GetGraphic(PawnRenderNode node, PawnDrawParms parms)
        {
            var comp = parms.pawn.GetComp<CompPawnSkin>();
            var skin = comp?.ActiveSkin;
            
            if (skin?.faceConfig?.enabled == true)
            {
                FaceComponentType type = ResolveFaceComponentType(node);
                ExpressionType exp = comp?.GetEffectiveExpression() ?? ExpressionType.Neutral;
                string path = skin.faceConfig.GetTexPath(type, exp);

                if (!string.IsNullOrEmpty(path))
                {
                    Shader shader = node.ShaderFor(parms.pawn);
                    if (shader == null)
                    {
                        shader = ShaderDatabase.Cutout;
                    }

                    var props = node.Props;

                    bool isExternal = path.Contains(":") ||
                                      path.StartsWith("/") ||
                                      System.IO.File.Exists(path);

                    if (isExternal)
                    {
                        if (!System.IO.File.Exists(path))
                        {
                            Log.Error($"[CharacterStudio] 外部表情纹理文件不存在: {path}");
                        }

                        var req = new GraphicRequest(
                            typeof(Graphic_Runtime),
                            path,
                            shader,
                            Vector2.one,
                            props?.color ?? Color.white,
                            Color.white, null, 0, null, null
                        );

                        var graphic = new Graphic_Runtime();
                        graphic.Init(req);
                        return graphic;
                    }
                    else
                    {
                        if (ContentFinder<Texture2D>.Get(path, false) == null)
                        {
                            if (ContentFinder<Texture2D>.Get(path + "_north", false) == null)
                            {
                                Log.Error($"[CharacterStudio] 无法找到游戏内表情纹理资源: {path}");
                            }
                            else
                            {
                                return GraphicDatabase.Get<Graphic_Multi>(
                                    path,
                                    shader,
                                    Vector2.one,
                                    props?.color ?? Color.white
                                );
                            }
                        }

                        return GraphicDatabase.Get<Graphic_Single>(
                            path,
                            shader,
                            Vector2.one,
                            props?.color ?? Color.white
                        );
                    }
                }
            }

            return base.GetGraphic(node, parms);
        }

        private static FaceComponentType ResolveFaceComponentType(PawnRenderNode node)
        {
            if (node is PawnRenderNode_Custom customNode && customNode.config != null)
            {
                return customNode.config.faceComponent;
            }

            string label = node.Props?.debugLabel?.ToLowerInvariant() ?? string.Empty;
            if (label.Contains("mouth") || label.Contains("嘴"))
            {
                return FaceComponentType.Mouth;
            }
            if (label.Contains("brow") || label.Contains("眉"))
            {
                return FaceComponentType.Brows;
            }

            return FaceComponentType.Eyes;
        }

        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            return base.CanDrawNow(node, parms);
        }
    }
}
