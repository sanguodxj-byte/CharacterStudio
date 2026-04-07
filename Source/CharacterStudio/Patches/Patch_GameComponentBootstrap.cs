using System;
using System.Collections.Generic;
using HarmonyLib;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using Verse;

namespace CharacterStudio.Patches
{
    /// <summary>
    /// RimWorld 1.6 不再提供 GameComponentDef。
    /// 这里在 Game.FinalizeInit 前确保 Character Studio 需要的 GameComponent 已被加入 Game.components。
    /// </summary>
    public static class Patch_GameComponentBootstrap
    {
        private static readonly System.Reflection.FieldInfo? ComponentsField =
            AccessTools.Field(typeof(Game), "components");

        private static readonly Type[] RequiredComponentTypes =
        {
            typeof(PawnSkinBootstrapComponent),
            typeof(AbilityHotkeyRuntimeComponent),
            typeof(AbilityTimeStopRuntimeController),
            typeof(CharacterRuntimeTriggerComponent)
        };

        public static void Apply(Harmony harmony)
        {
            var target = AccessTools.Method(typeof(Game), nameof(Game.FinalizeInit));
            if (target == null)
            {
                Log.Warning("[CharacterStudio] Patch_GameComponentBootstrap: 未找到 Game.FinalizeInit，跳过注册。");
                return;
            }

            var prefix = new HarmonyMethod(typeof(Patch_GameComponentBootstrap), nameof(Game_FinalizeInit_Prefix));
            harmony.Patch(target, prefix: prefix);
            Log.Message("[CharacterStudio] Patch_GameComponentBootstrap 已注册。");
        }

        private static void Game_FinalizeInit_Prefix(Game __instance)
        {
            EnsureRequiredComponents(__instance);
        }

        private static void EnsureRequiredComponents(Game? game)
        {
            if (game == null || ComponentsField == null)
                return;

            if (ComponentsField.GetValue(game) is not List<GameComponent> components)
            {
                Log.Warning("[CharacterStudio] Patch_GameComponentBootstrap: 无法访问 Game.components，组件注册已跳过。");
                return;
            }

            foreach (Type componentType in RequiredComponentTypes)
            {
                bool exists = false;
                for (int i = 0; i < components.Count; i++)
                {
                    GameComponent? component = components[i];
                    if (component != null && componentType.IsInstanceOfType(component))
                    {
                        exists = true;
                        break;
                    }
                }

                if (exists)
                    continue;

                try
                {
                    if (Activator.CreateInstance(componentType, game) is GameComponent component)
                    {
                        components.Add(component);
                        Log.Message($"[CharacterStudio] 已注册 GameComponent: {componentType.Name}");
                    }
                    else
                    {
                        Log.Warning($"[CharacterStudio] Patch_GameComponentBootstrap: 未能创建组件实例 {componentType.FullName}");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[CharacterStudio] Patch_GameComponentBootstrap: 注册组件失败 [{componentType.FullName}]: {ex}");
                }
            }
        }
    }
}
