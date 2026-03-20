using System;
using UnityEngine;
using Verse;

namespace CharacterStudio.Rendering
{
    /// <summary>
    /// 运行时图形
    /// 用于加载外部纹理文件并作为 Graphic 使用
    /// 绕过 ContentFinder，直接使用 RuntimeAssetLoader
    /// </summary>
    public class Graphic_Runtime : Graphic_Single
    {
        public override void Init(GraphicRequest req)
        {
            this.data = req.graphicData;
            this.path = req.path;
            this.color = req.color;
            this.colorTwo = req.colorTwo;
            this.drawSize = req.drawSize;

            // 尝试加载纹理
            // 这里的 req.path 应该是完整文件路径
            Texture2D? tex = RuntimeAssetLoader.LoadTextureRaw(req.path);

            if (tex == null)
            {
                bool looksLikeExternalPath = !string.IsNullOrEmpty(req.path)
                    && (req.path.Contains(":") || req.path.StartsWith("/") || System.IO.Path.IsPathRooted(req.path));

                bool externalFileExists = looksLikeExternalPath && System.IO.File.Exists(req.path);

                // 外部纹理在非主线程首次触发渲染时，RuntimeAssetLoader 会主动拒绝即时创建 Texture2D，
                // 以避免 Unity 崩溃。这种情况下不要把它当成真正错误刷红日志。
                if (externalFileExists && !RuntimeAssetLoader.IsMainThread())
                {
                    return;
                }

                Log.Error($"[CharacterStudio] 无法加载纹理: {req.path}");
                return;
            }

            // 获取材质
            Material? mat = RuntimeAssetLoader.GetMaterialForTexture(tex, req.shader);
            
            // 应用颜色
            if (mat != null)
            {
                mat.color = this.color;
                this.mat = mat; // Graphic_Single 直接使用 mat 字段
            }
            else
            {
                 Log.Error($"[CharacterStudio] 无法为纹理创建材质: {req.path}");
            }
        }

        public override Graphic GetColoredVersion(Shader newShader, Color newColor, Color newColorTwo)
        {
            return GraphicDatabase.Get<Graphic_Runtime>(this.path, newShader, this.drawSize, newColor, newColorTwo, this.data);
        }
        
        public override string ToString()
        {
            return $"Graphic_Runtime(path={this.path})";
        }
    }
}