using CharacterStudio.Rendering;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 支持帧动画的自定义 Mote。
    /// 按帧率切换材质贴图，实现精灵帧动画效果。
    /// 
    /// 帧文件命名约定（以 basePath = "Effects/fire" 为例）：
    ///   Effects/fire_1.png  → 第 1 帧
    ///   Effects/fire_2.png  → 第 2 帧
    ///   ...
    ///   Effects/fire_N.png  → 第 N 帧
    /// 
    /// 当 frameCount >= 10 时，同时尝试零填充格式（_01, _02, ...）
    /// </summary>
    public class Mote_VfxFrameAnimation : MoteThrown
    {
        private Graphic_FrameAnimation? frameGraphic;
        private Texture2D?[] frameTextures = System.Array.Empty<Texture2D?>();
        private int frameCount;
        private int frameIntervalTicks;
        private bool frameLoop;
        private new int spawnTick;
        private int currentFrame = -1;
        private bool initialized;

        public override Graphic Graphic
        {
            get
            {
                if (initialized && frameGraphic != null)
                {
                    return frameGraphic;
                }
                return base.Graphic;
            }
        }

        /// <summary>
        /// 初始化帧动画。在 Spawn 之后调用。
        /// </summary>
        /// <param name="basePath">贴图基础路径（不含帧号后缀和扩展名）</param>
        /// <param name="count">总帧数</param>
        /// <param name="intervalTicks">每帧持续 tick 数</param>
        /// <param name="loop">是否循环播放</param>
        public void InitFrames(string basePath, int count, int intervalTicks, bool loop)
        {
            frameCount = Mathf.Max(1, count);
            frameIntervalTicks = Mathf.Max(1, intervalTicks);
            frameLoop = loop;
            spawnTick = Find.TickManager.TicksGame;

            // 预加载所有帧贴图
            frameTextures = new Texture2D?[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                string framePath = $"{basePath}_{i + 1}";
                Texture2D? tex = RuntimeAssetLoader.LoadTextureRaw(framePath);
                if (tex == null)
                {
                    // 尝试零填充格式 _01, _02, ...
                    framePath = $"{basePath}_{(i + 1):D2}";
                    tex = RuntimeAssetLoader.LoadTextureRaw(framePath);
                }
                frameTextures[i] = tex;
            }

            // 使用第一帧作为初始贴图，创建独立的 Graphic + Material
            Texture2D? firstTex = frameTextures.Length > 0 && frameTextures[0] != null
                ? frameTextures[0]
                : RuntimeAssetLoader.LoadTextureRaw(basePath);

            if (firstTex != null && def?.graphicData != null)
            {
                frameGraphic = new Graphic_FrameAnimation();
                string firstFramePath = frameCount >= 1 ? $"{basePath}_1" : basePath;
                var req = new GraphicRequest(
                    typeof(Graphic_FrameAnimation),
                    firstFramePath,
                    ShaderDatabase.Transparent,
                    def.graphicData.drawSize,
                    Color.white,
                    Color.white,
                    def.graphicData,
                    0,
                    null,
                    null);
                frameGraphic.Init(req);
            }

            initialized = true;
            currentFrame = -1;
            ApplyCurrentFrame();
        }

        protected override void Tick()
        {
            base.Tick();
            if (initialized)
            {
                ApplyCurrentFrame();
            }
        }

        private void ApplyCurrentFrame()
        {
            if (frameTextures == null || frameGraphic == null) return;

            int elapsed = Find.TickManager.TicksGame - spawnTick;
            int frameIndex;

            if (frameLoop)
            {
                frameIndex = (elapsed / frameIntervalTicks) % frameCount;
            }
            else
            {
                frameIndex = Mathf.Min(elapsed / frameIntervalTicks, frameCount - 1);
            }

            if (frameIndex == currentFrame) return;
            currentFrame = frameIndex;

            if (currentFrame >= 0 && currentFrame < frameTextures.Length && frameTextures[currentFrame] != null)
            {
                frameGraphic.SetFrameTexture(frameTextures[currentFrame]!);
            }
        }
    }

    /// <summary>
    /// 支持帧贴图切换的 Graphic。
    /// 在 Init 时克隆材质，确保每个 Mote 实例拥有独立的材质，
    /// 避免帧切换影响共享同一 GraphicDatabase 缓存的其他 Mote。
    /// </summary>
    public class Graphic_FrameAnimation : Graphic_Runtime
    {
        public override void Init(GraphicRequest req)
        {
            base.Init(req);
            // 克隆材质，使此实例拥有独立材质
            if (this.mat != null)
            {
                this.mat = new Material(this.mat);
            }
        }

        /// <summary>
        /// 切换当前帧贴图
        /// </summary>
        public void SetFrameTexture(Texture2D tex)
        {
            if (this.mat != null && tex != null)
            {
                this.mat.mainTexture = tex;
            }
        }
    }
}
