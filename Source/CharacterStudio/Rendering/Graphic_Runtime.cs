using System;
using System.Collections.Generic;
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
        private static readonly object initWarningLock = new object();
        private static readonly HashSet<string> initWarnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object pendingInitLock = new object();
        private static readonly HashSet<string> pendingMainThreadInitializations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public bool IsInitializedSuccessfully { get; private set; }

        public override void Init(GraphicRequest req)
        {
            IsInitializedSuccessfully = false;
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
                bool looksLikeExternalPath = RuntimeAssetLoader.LooksLikeExternalTexturePath(req.path);

                string resolvedExternalPath = req.path;
                bool externalFileExists = looksLikeExternalPath
                    && RuntimeAssetLoader.ExternalTextureExists(req.path, out resolvedExternalPath);
                bool pendingMainThreadInit = looksLikeExternalPath
                    && IsPendingMainThreadInitialization(resolvedExternalPath);

                // 外部纹理在非主线程首次触发渲染时，RuntimeAssetLoader 会主动拒绝即时创建 Texture2D，
                // 以避免 Unity 崩溃。这种情况下不要把它当成真正错误，也不要缓存透明占位材质，
                // 让后续主线程渲染仍有机会重新初始化为真实纹理。
                if (pendingMainThreadInit || (externalFileExists && !RuntimeAssetLoader.IsMainThread()))
                {
                    MarkPendingMainThreadInitialization(resolvedExternalPath);
                    return;
                }

                if (!looksLikeExternalPath)
                {
                    LogInitWarningOnce(req.path, $"[CharacterStudio] Graphic_Runtime 无法加载纹理，已回退为透明占位材质: {req.path}");
                }

                ApplyFallbackMaterial(req.shader);
                return;
            }

            // 获取材质
            Material? mat = RuntimeAssetLoader.GetMaterialForTexture(tex, req.shader);

            if (mat == null && !RuntimeAssetLoader.IsMainThread())
            {
                MarkPendingMainThreadInitialization(req.path);
                return;
            }
            
            // 应用颜色
            if (mat != null)
            {
                mat.color = this.color;
                this.mat = mat; // Graphic_Single 直接使用 mat 字段
                IsInitializedSuccessfully = true;
                ClearPendingMainThreadInitialization(req.path);
            }
            else
            {
                LogInitWarningOnce(req.path, $"[CharacterStudio] 无法为纹理创建材质，已回退为透明占位材质: {req.path}");
                ApplyFallbackMaterial(req.shader);
            }
        }

        private void ApplyFallbackMaterial(Shader? requestedShader)
        {
            if (!RuntimeAssetLoader.IsMainThread())
            {
                return;
            }

            Material? fallbackMat = RuntimeAssetLoader.GetMaterialForTexture(
                BaseContent.ClearTex,
                requestedShader ?? ShaderDatabase.Transparent);

            if (fallbackMat != null)
            {
                fallbackMat.color = new Color(this.color.r, this.color.g, this.color.b, 0f);
                this.mat = fallbackMat;
                IsInitializedSuccessfully = true;
                ClearPendingMainThreadInitialization(this.path);
            }
        }

        private static void MarkPendingMainThreadInitialization(string? path)
        {
            string pendingKey = path?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(pendingKey))
            {
                return;
            }

            lock (pendingInitLock)
            {
                pendingMainThreadInitializations.Add(pendingKey);
            }
        }

        private static void ClearPendingMainThreadInitialization(string? path)
        {
            string pendingKey = path?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(pendingKey))
            {
                return;
            }

            lock (pendingInitLock)
            {
                pendingMainThreadInitializations.Remove(pendingKey);
            }
        }

        public static bool IsPendingMainThreadInitialization(string? path)
        {
            string pendingKey = path?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(pendingKey))
            {
                return false;
            }

            lock (pendingInitLock)
            {
                return pendingMainThreadInitializations.Contains(pendingKey);
            }
        }

        private static void LogInitWarningOnce(string? key, string message)
        {
            string warningKey = key?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(warningKey))
            {
                return;
            }

            lock (initWarningLock)
            {
                if (!initWarnings.Add(warningKey))
                {
                    return;
                }
            }

            Log.Warning(message);
        }

        public override Graphic GetColoredVersion(Shader newShader, Color newColor, Color newColorTwo)
        {
            // 彻底绕过 GraphicDatabase.Get，因为该方法内部会强制调用 ContentFinder 校验路径，
            // 导致绝对路径直接触发原版报错。
            var newGraphic = Activator.CreateInstance<Graphic_Runtime>();
            newGraphic.Init(new GraphicRequest(typeof(Graphic_Runtime), this.path, newShader, this.drawSize, newColor, newColorTwo, this.data, 0, null, null));
            return newGraphic;
        }
        
        public override string ToString()
        {
            return $"Graphic_Runtime(path={this.path})";
        }
    }
}
