// ─────────────────────────────────────────────
// BezierCurveWall 游戏生命周期组件
//
// 负责：
//   1. 每 Tick 清理过期的贝塞尔曲线墙实例
//   2. 在 GameComponentUpdate 中绘制曲线的可视化效果
// ─────────────────────────────────────────────

using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 驱动贝塞尔曲线墙的清理和绘制。
    /// 通过 Patch_GameComponentBootstrap 自动注入到 Game.components。
    /// </summary>
    [StaticConstructorOnStartup]
    public class BezierCurveWallGameComponent : GameComponent
    {
        private static readonly System.Collections.Generic.Dictionary<string, Material> CachedMaterials = new System.Collections.Generic.Dictionary<string, Material>();

        private static Material GetMaterial(string texturePath)
        {
            if (string.IsNullOrWhiteSpace(texturePath))
                texturePath = GenDraw.LineTexPath;

            if (CachedMaterials.TryGetValue(texturePath, out Material mat))
                return mat;

            mat = MaterialPool.MatFrom(texturePath, ShaderDatabase.Transparent, new Color(0.4f, 0.8f, 1f, 0.6f));
            CachedMaterials[texturePath] = mat;
            return mat;
        }

        public BezierCurveWallGameComponent(Game game) : base() { }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            BezierCurveWallManager.CleanupExpired(nowTick);
        }

        public override void GameComponentUpdate()
        {
            base.GameComponentUpdate();

            Map? map = Find.CurrentMap;
            if (map == null) return;

            var walls = BezierCurveWallManager.ActiveWalls;
            for (int wi = 0; wi < walls.Count; wi++)
            {
                var wall = walls[wi];
                if (wall.Map != map) continue;
                if (wall.SegmentPoints == null || wall.SegmentPoints.Length < 2) continue;

                DrawBezierWall(wall);
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            BezierCurveWallManager.ClearAll();
        }

        private static void DrawBezierWall(BezierWallInstance wall)
        {
            float altitude = AltitudeLayer.MoteOverhead.AltitudeFor();
            Material mat = GetMaterial(wall.CustomTexturePath);

            for (int i = 0; i < wall.SegmentPoints.Length - 1; i++)
            {
                Vector2 a = wall.SegmentPoints[i];
                Vector2 b = wall.SegmentPoints[i + 1];

                Vector3 from = new Vector3(a.x, altitude, a.y);
                Vector3 to = new Vector3(b.x, altitude, b.y);

                GenDraw.DrawLineBetween(from, to, mat, wall.Thickness * 2f);
            }
        }
    }
}
