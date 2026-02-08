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
        /// 获取图形 - 根据表情状态动态切换贴图
        /// </summary>
        protected override Graphic? GetGraphic(PawnRenderNode node, PawnDrawParms parms)
        {
            var comp = parms.pawn.GetComp<CompPawnSkin>();
            var skin = comp?.ActiveSkin;
            
            if (skin?.faceConfig?.enabled == true)
            {
                // 尝试从节点名称判断组件类型
                FaceComponentType type = FaceComponentType.Eyes;
                string label = node.Props?.debugLabel?.ToLower() ?? "";
                
                if (label.Contains("mouth") || label.Contains("嘴")) type = FaceComponentType.Mouth;
                else if (label.Contains("brow") || label.Contains("眉")) type = FaceComponentType.Brows;

                ExpressionType exp = comp.GetEffectiveExpression();
                string path = skin.faceConfig.GetTexPath(type, exp);
                
                if (!string.IsNullOrEmpty(path))
                {
                    Shader shader = node.ShaderFor(parms.pawn);
                    if (shader == null)
                    {
                        shader = ShaderDatabase.Cutout;
                    }

                    var props = node.Props;
                    
                    // 检查是否是外部路径
                    bool isExternal = path.Contains(":") ||
                                      path.StartsWith("/") ||
                                      System.IO.File.Exists(path);

                    if (isExternal)
                    {
                        if (!System.IO.File.Exists(path))
                        {
                            Log.Error($"[CharacterStudio] 外部表情纹理文件不存在: {path}");
                        }

                        // 不使用 GraphicDatabase 缓存 Graphic_Runtime，以支持实时热加载
                        var req = new GraphicRequest(
                            typeof(Graphic_Runtime),
                            path,
                            shader,
                            props?.drawSize ?? Vector2.one,
                            props?.color ?? Color.white,
                            Color.white, null, 0, null, null
                        );
                        
                        var graphic = new Graphic_Runtime();
                        graphic.Init(req);
                        return graphic;
                    }
                    else
                    {
                        // 检查游戏内资源是否存在
                        if (ContentFinder<Texture2D>.Get(path, false) == null)
                        {
                            // 尝试检查 _north 变体
                            if (ContentFinder<Texture2D>.Get(path + "_north", false) == null)
                            {
                                Log.Error($"[CharacterStudio] 无法找到游戏内表情纹理资源: {path}");
                            }
                            else
                            {
                                // 如果存在 _north，可能是 Graphic_Multi
                                return GraphicDatabase.Get<Graphic_Multi>(
                                    path,
                                    shader,
                                    props?.drawSize ?? Vector2.one,
                                    props?.color ?? Color.white
                                );
                            }
                        }

                        return GraphicDatabase.Get<Graphic_Single>(
                            path,
                            shader,
                            props?.drawSize ?? Vector2.one,
                            props?.color ?? Color.white
                        );
                    }
                }
            }

            return base.GetGraphic(node, parms);
        }

        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            // 基础检查
            return base.CanDrawNow(node, parms);
        }
    }
}
