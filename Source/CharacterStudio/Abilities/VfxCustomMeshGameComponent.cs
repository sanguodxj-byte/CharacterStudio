// ─────────────────────────────────────────────
// CustomMesh VFX 游戏生命周期组件
//
// 作为 GameComponent 注册，每 Tick 驱动 VfxCustomMeshManager，
// 并在合适的渲染时机调用 Draw()。
// ─────────────────────────────────────────────

using Verse;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 驱动 VfxCustomMeshManager 的 GameComponent。
    /// 通过 Patch_GameComponentBootstrap 自动注入到 Game.components。
    /// </summary>
    public class VfxCustomMeshGameComponent : GameComponent
    {
        public VfxCustomMeshGameComponent(Game game) : base() { }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            VfxCustomMeshManager.Tick();
        }

        public override void GameComponentOnGUI()
        {
            base.GameComponentOnGUI();
            // 在 OnGUI 阶段渲染，确保在 Camera.Render 之前提交所有 DrawMesh 调用
            VfxCustomMeshManager.Draw();
        }
    }
}
