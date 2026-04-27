// ─────────────────────────────────────────────
// CustomMesh VFX Worker
//
// 将 AbilityVisualEffectConfig.CustomMesh 类型的特效
// 通过 VfxMeshGenerator 生成程序化 Mesh，
// 并提交到 VfxCustomMeshManager 进行生命周期渲染。
// ─────────────────────────────────────────────

using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 自定义 Mesh 特效 Worker。
    /// 从配置中读取 Mesh 形状参数，程序化生成 Mesh，注入到 VfxCustomMeshManager 渲染。
    /// </summary>
    public class VisualEffectWorker_CustomMesh : VisualEffectWorker
    {
        public override void Play(AbilityVisualEffectConfig config, LocalTargetInfo target, Pawn caster)
        {
            if (caster?.Map == null) return;

            foreach (Vector3 pos in ResolvePositions(config, target, caster))
            {
                SpawnMeshEffect(config, pos, caster);
            }
        }

        private void SpawnMeshEffect(AbilityVisualEffectConfig config, Vector3 position, Pawn caster)
        {
            // 1. 根据 meshShape 生成 Mesh
            Mesh mesh = GenerateMesh(config);
            if (mesh == null) return;

            // 2. 构建材质
            Material baseMat = ResolveMaterial(config);

            // 3. 计算旋转
            Quaternion rotation = ResolveRotation(config, caster);

            // 4. 计算缩放
            Vector3 scale = Vector3.one * Mathf.Max(0.01f, config.scale);

            // 5. 应用高度偏移
            Vector3 drawPos = position;
            drawPos.y += config.heightOffset;

            // 6. 提交到 Manager
            VfxCustomMeshManager.Spawn(
                mesh: mesh,
                baseMaterial: baseMat,
                position: drawPos,
                rotation: rotation,
                scale: scale,
                durationTicks: Mathf.Max(1, config.displayDurationTicks),
                fadeInTicks: Mathf.Max(0, config.meshFadeInTicks),
                altitude: AltitudeLayer.Weather);
        }

        private Mesh GenerateMesh(AbilityVisualEffectConfig config)
        {
            switch (config.meshShape)
            {
                case VfxMeshShape.LightningBolt:
                    return VfxMeshGenerator.GenerateLightningBolt(
                        height: config.meshSize,
                        width: config.meshSecondarySize,
                        segments: Mathf.Max(2, config.meshSegments),
                        jitter: config.meshParam1);

                case VfxMeshShape.Ring:
                    return VfxMeshGenerator.GenerateRing(
                        innerRadius: config.meshSecondarySize,
                        outerRadius: config.meshSize,
                        segments: Mathf.Max(6, config.meshSegments));

                case VfxMeshShape.Spiral:
                    return VfxMeshGenerator.GenerateSpiral(
                        height: config.meshSize,
                        radius: config.meshSecondarySize,
                        turns: Mathf.Max(0.1f, config.meshParam2),
                        ribbonWidth: Mathf.Max(0.01f, config.meshParam1),
                        segments: Mathf.Max(6, config.meshSegments));

                case VfxMeshShape.Beam:
                    return VfxMeshGenerator.GenerateBeam(
                        length: config.meshSize,
                        startWidth: config.meshSecondarySize,
                        endWidth: Mathf.Max(0.01f, config.meshParam1),
                        segments: Mathf.Max(1, config.meshSegments));

                default:
                    Log.Warning($"[CharacterStudio] 未知的 VfxMeshShape: {config.meshShape}，回退到 LightningBolt");
                    return VfxMeshGenerator.GenerateLightningBolt(
                        config.meshSize, config.meshSecondarySize,
                        Mathf.Max(2, config.meshSegments), config.meshParam1);
            }
        }

        private static Material ResolveMaterial(AbilityVisualEffectConfig config)
        {
            // 如果配置了贴图路径，创建带贴图的材质
            if (!string.IsNullOrWhiteSpace(config.meshTexturePath))
            {
                Texture2D? tex = ContentFinder<Texture2D>.Get(config.meshTexturePath, false);
                if (tex == null)
                {
                    tex = Rendering.RuntimeAssetLoader.LoadTextureRaw(config.meshTexturePath);
                }

                if (tex != null)
                {
                    Shader shader = ResolveShader(config);
                    var mat = new Material(shader);
                    mat.mainTexture = tex;
                    mat.color = Color.white;
                    return mat;
                }
            }

            // 如果配置了 shader 名称，使用指定 Shader
            if (!string.IsNullOrWhiteSpace(config.meshShaderName))
            {
                Shader? shader = Shader.Find(config.meshShaderName);
                if (shader != null)
                {
                    return new Material(shader) { color = Color.white };
                }
            }

            // 默认使用 MoteGlow（发光效果，与 RimWorld 原版闪电一致）
            return MaterialPool.MatFrom("Things/Mote/HeatGlow", ShaderDatabase.MoteGlow, Color.white);
        }

        private static Shader ResolveShader(AbilityVisualEffectConfig config)
        {
            if (!string.IsNullOrWhiteSpace(config.meshShaderName))
            {
                Shader? shader = Shader.Find(config.meshShaderName);
                if (shader != null) return shader;
            }
            return ShaderDatabase.MoteGlow;
        }

        private static Quaternion ResolveRotation(AbilityVisualEffectConfig config, Pawn caster)
        {
            float yRot = config.rotation;

            if (config.facingMode == AbilityVisualFacingMode.CasterFacing && caster != null)
            {
                yRot += caster.Rotation.AsAngle;
            }

            return Quaternion.Euler(0f, yRot, 0f);
        }
    }
}
