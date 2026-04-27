using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities
{
    public sealed class AbilityTimeStopRuntimeController : GameComponent
    {
        private List<AbilityTimeStopState> activeStates = new List<AbilityTimeStopState>();
        private float originalCameraSaturation = 1f;
        private bool originalCameraColorEnabled;
        private bool cameraSaturationCaptured;
        private Texture2D? casterHighlightTexture;
        private bool createdCameraColorForTimeStop;

        private static readonly System.Reflection.PropertyInfo? FindCameraColorProperty =
            AccessTools.Property(typeof(Find), "CameraColor");
        private static readonly System.Reflection.PropertyInfo? FindCameraProperty =
            AccessTools.Property(typeof(Find), "Camera");
        private static readonly Dictionary<Type, System.Reflection.FieldInfo?> CameraSaturationFieldCache =
            new Dictionary<Type, System.Reflection.FieldInfo?>();

        public AbilityTimeStopRuntimeController(Game game)
        {
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref activeStates, "csAbilityTimeStops", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                activeStates ??= new List<AbilityTimeStopState>();
                CleanupExpiredStates();
            }
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            CleanupExpiredStates();
            UpdateCameraGrayscale();

            // 诊断：每 600 tick (10秒) 输出一次当前时停状态
            int tick = Find.TickManager?.TicksGame ?? 0;
            if (tick > 0 && tick % 600 == 0 && activeStates.Count > 0)
            {
                Log.Message($"[CharacterStudio] TimeStop 状态报告 tick={tick}: {activeStates.Count} 个活跃时停状态");
                foreach (var state in activeStates)
                {
                    if (state != null)
                    {
                        Log.Message($"[CharacterStudio]   map={state.mapId}, caster={state.casterThingId}, [{state.startTick}..{state.endTick}], frozenVisualTick={state.frozenVisualTick}, freezeVisuals={state.freezeVisualsDuringTimeStop}, active={state.IsActive(tick)}");
                    }
                }
            }
        }

        public override void GameComponentOnGUI()
        {
            base.GameComponentOnGUI();
            DrawCasterHighlightOverlay();
        }

        public static void ActivateForCaster(Pawn? caster, AbilityRuntimeComponentConfig? component, int nowTick)
        {
            if (caster == null || component == null)
            {
                return;
            }

            Current.Game?.GetComponent<AbilityTimeStopRuntimeController>()?.Activate(caster, component, nowTick);
        }

        public static bool CanPawnAct(Pawn? pawn)
        {
            return pawn != null && !ShouldSkipThingTick(pawn);
        }

        public static bool ShouldSkipThingTick(Thing? thing)
        {
            if (thing == null)
            {
                return false;
            }

            AbilityTimeStopState? state = TryGetActiveStateForMap(thing.MapHeld);
            if (state == null)
            {
                return false;
            }

            return !IsThingExemptFromTimeStop(thing, state);
        }

        public static bool ShouldSkipMapTick(Map? map)
        {
            return TryGetActiveStateForMap(map) != null;
        }

        public static bool ShouldFreezeMapVisuals(Map? map)
        {
            AbilityTimeStopState? state = TryGetActiveStateForMap(map);
            return state != null && state.freezeVisualsDuringTimeStop;
        }

        public static bool HasActiveTimeStopOnCurrentMap()
        {
            return TryGetActiveStateForMap(Find.CurrentMap) != null;
        }

        public static int ResolveGlobalVisualTickForCurrentMap(int liveTick)
        {
            return ResolveGlobalVisualTickForMap(Find.CurrentMap, liveTick);
        }

        public static int ResolveGlobalVisualTickForMap(Map? map, int liveTick)
        {
            AbilityTimeStopState? state = TryGetActiveStateForMap(map);
            if (state == null || !state.freezeVisualsDuringTimeStop)
            {
                return liveTick;
            }

            return state.frozenVisualTick >= 0 ? state.frozenVisualTick : liveTick;
        }

        public static Pawn? GetActiveCasterOnCurrentMap()
        {
            AbilityTimeStopState? state = TryGetActiveStateForMap(Find.CurrentMap);
            if (state == null)
            {
                return null;
            }

            Map? currentMap = Find.CurrentMap;
            if (currentMap?.mapPawns?.AllPawnsSpawned == null)
            {
                return null;
            }

            for (int i = 0; i < currentMap.mapPawns.AllPawnsSpawned.Count; i++)
            {
                Pawn pawn = currentMap.mapPawns.AllPawnsSpawned[i];
                if (pawn != null && pawn.thingIDNumber == state.casterThingId)
                {
                    return pawn;
                }
            }

            return null;
        }

        public static int ResolveVisualTickForPawn(Pawn? pawn, int liveTick)
        {
            return ResolveVisualTickForThing(pawn, liveTick);
        }

        public static int ResolveVisualTickForThing(Thing? thing, int liveTick)
        {
            if (thing == null)
            {
                return liveTick;
            }

            AbilityTimeStopState? state = TryGetActiveStateForMap(thing.MapHeld);
            if (state == null || !state.freezeVisualsDuringTimeStop || IsThingExemptFromTimeStop(thing, state))
            {
                return liveTick;
            }

            return state.frozenVisualTick >= 0 ? state.frozenVisualTick : liveTick;
        }

        private void Activate(Pawn caster, AbilityRuntimeComponentConfig component, int nowTick)
        {
            Map? map = caster.MapHeld;
            if (map == null)
            {
                Log.Warning("[CharacterStudio] TimeStop Activate: caster.MapHeld 为 null，无法启动时停。");
                return;
            }

            int durationTicks = Math.Max(1, component.timeStopDurationTicks);
            int mapId = map.uniqueID;
            AbilityTimeStopState? existing = activeStates.FirstOrDefault(state => state != null && state.mapId == mapId);

            if (existing != null && existing.casterThingId == caster.thingIDNumber && existing.IsActive(nowTick))
            {
                existing.endTick = Math.Max(existing.endTick, nowTick + durationTicks);
                existing.freezeVisualsDuringTimeStop = existing.freezeVisualsDuringTimeStop || component.freezeVisualsDuringTimeStop;
                ReleaseCasterForTimeStop(caster);
                Log.Message($"[CharacterStudio] TimeStop 已续期: map={mapId}, endTick={existing.endTick}, duration={durationTicks}");
                return;
            }

            activeStates.RemoveAll(state => state == null || state.mapId == mapId);
            activeStates.Add(new AbilityTimeStopState
            {
                mapId = mapId,
                casterThingId = caster.thingIDNumber,
                startTick = nowTick,
                endTick = nowTick + durationTicks,
                frozenVisualTick = nowTick,
                freezeVisualsDuringTimeStop = component.freezeVisualsDuringTimeStop
            });

            ReleaseCasterForTimeStop(caster);
            UpdateCameraGrayscale();
            Log.Message($"[CharacterStudio] TimeStop 已激活: map={mapId}, caster={caster.LabelShort}, tick={nowTick}, endTick={nowTick + durationTicks}, duration={durationTicks}, freezeVisuals={component.freezeVisualsDuringTimeStop}");
        }

        private void CleanupExpiredStates()
        {
            int nowTick = Find.TickManager?.TicksGame ?? 0;
            activeStates.RemoveAll(state => state == null || !state.IsActive(nowTick) || !MapStillExists(state.mapId));
        }

        private void UpdateCameraGrayscale()
        {
            if (!TryGetOrCreateCameraColor(out object? cameraColorObject, out Behaviour? cameraColor))
            {
                return;
            }

            object cameraColorInstance = cameraColorObject!;

            System.Reflection.FieldInfo? saturationField = GetCameraSaturationField(cameraColorInstance.GetType());
            if (saturationField == null)
            {
                return;
            }

            bool shouldGrayscale = HasActiveTimeStopOnCurrentMap();
            if (shouldGrayscale)
            {
                if (cameraColor == null)
                {
                    return;
                }

                if (!cameraSaturationCaptured)
                {
                    originalCameraSaturation = (float)(saturationField.GetValue(cameraColorInstance) ?? 1f);
                    originalCameraColorEnabled = cameraColor.enabled;
                    cameraSaturationCaptured = true;
                }

                float saturation = (float)(saturationField.GetValue(cameraColorInstance) ?? 1f);
                if (Math.Abs(saturation) > 0.001f)
                {
                    saturationField.SetValue(cameraColorInstance, 0f);
                }

                InvokeCameraMethodIfExists(cameraColorInstance, "UpdateParameters");

                if (!cameraColor.enabled)
                {
                    cameraColor.enabled = true;
                }

                return;
            }

            if (!cameraSaturationCaptured)
            {
                return;
            }

            if (cameraColor == null)
            {
                cameraSaturationCaptured = false;
                return;
            }

            saturationField.SetValue(cameraColorInstance, originalCameraSaturation);
            cameraColor.enabled = originalCameraColorEnabled;
            InvokeCameraMethodIfExists(cameraColorInstance, "UpdateParameters");
            if (createdCameraColorForTimeStop && !originalCameraColorEnabled)
            {
                UnityEngine.Object.Destroy(cameraColor);
                createdCameraColorForTimeStop = false;
            }

            cameraSaturationCaptured = false;
        }

        private void DrawCasterHighlightOverlay()
        {
            if (!HasActiveTimeStopOnCurrentMap())
            {
                return;
            }

            Pawn? caster = GetActiveCasterOnCurrentMap();
            if (caster == null || !caster.Spawned || caster.Map != Find.CurrentMap)
            {
                return;
            }

            if (Event.current == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            Vector2 center = GenMapUI.LabelDrawPosFor(caster, 0f);
            if (float.IsNaN(center.x) || float.IsNaN(center.y))
            {
                return;
            }

            Texture2D texture = GetOrCreateCasterHighlightTexture();
            const float outerSize = 116f;
            const float innerSize = 76f;

            Rect outerRect = new Rect(center.x - outerSize * 0.5f, center.y - outerSize * 0.5f, outerSize, outerSize);
            Rect innerRect = new Rect(center.x - innerSize * 0.5f, center.y - innerSize * 0.5f, innerSize, innerSize);

            Color prevColor = GUI.color;
            GUI.color = new Color(0.48f, 0.92f, 1f, 0.16f);
            GUI.DrawTexture(outerRect, texture);
            GUI.color = new Color(0.75f, 0.97f, 1f, 0.10f);
            GUI.DrawTexture(innerRect, texture);
            GUI.color = prevColor;
        }

        private Texture2D GetOrCreateCasterHighlightTexture()
        {
            if (casterHighlightTexture != null)
            {
                return casterHighlightTexture;
            }

            const int size = 64;
            casterHighlightTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "CS_TimeStopCasterHighlight"
            };

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxDist = center.x;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist01 = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                    float alpha = Mathf.Clamp01(1f - dist01);
                    alpha = alpha * alpha * 0.92f;
                    casterHighlightTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            casterHighlightTexture.Apply(false, true);
            return casterHighlightTexture;
        }

        private static System.Reflection.FieldInfo? GetCameraSaturationField(Type cameraColorType)
        {
            if (CameraSaturationFieldCache.TryGetValue(cameraColorType, out System.Reflection.FieldInfo? cachedField))
            {
                return cachedField;
            }

            System.Reflection.FieldInfo? field = AccessTools.Field(cameraColorType, "saturation");
            CameraSaturationFieldCache[cameraColorType] = field;
            return field;
        }

        private static void InvokeCameraMethodIfExists(object cameraColorObject, string methodName)
        {
            System.Reflection.MethodInfo? method = AccessTools.Method(cameraColorObject.GetType(), methodName);
            method?.Invoke(cameraColorObject, null);
        }

        private bool TryGetOrCreateCameraColor(out object? cameraColorObject, out Behaviour? cameraColor)
        {
            cameraColorObject = FindCameraColorProperty?.GetValue(null, null);
            cameraColor = cameraColorObject as Behaviour;
            if (cameraColorObject != null && cameraColor != null)
            {
                return true;
            }

            Camera? cameraComponent = FindCameraProperty?.GetValue(null, null) as Camera;
            Type? colorType = FindCameraColorProperty?.PropertyType;
            if (cameraComponent == null || colorType == null)
            {
                return false;
            }

            cameraColor = cameraComponent.GetComponent(colorType) as Behaviour;
            if (cameraColor == null)
            {
                cameraColor = cameraComponent.gameObject.AddComponent(colorType) as Behaviour;
                createdCameraColorForTimeStop = cameraColor != null;
            }

            cameraColorObject = cameraColor;
            if (cameraColorObject == null)
            {
                return false;
            }

            InvokeCameraMethodIfExists(cameraColorObject, "CheckResources");
            InvokeCameraMethodIfExists(cameraColorObject, "UpdateTextures");
            InvokeCameraMethodIfExists(cameraColorObject, "UpdateParameters");
            return true;
        }

        private static AbilityTimeStopState? TryGetActiveStateForMap(Map? map)
        {
            if (map == null || Current.Game == null)
            {
                return null;
            }

            AbilityTimeStopRuntimeController? controller = Current.Game.GetComponent<AbilityTimeStopRuntimeController>();
            if (controller == null || controller.activeStates == null || controller.activeStates.Count == 0)
            {
                return null;
            }

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            int mapId = map.uniqueID;
            AbilityTimeStopState? best = null;
            for (int i = 0; i < controller.activeStates.Count; i++)
            {
                AbilityTimeStopState? state = controller.activeStates[i];
                if (state == null || state.mapId != mapId || !state.IsActive(nowTick))
                {
                    continue;
                }

                if (best == null || state.endTick > best.endTick)
                {
                    best = state;
                }
            }

            return best;
        }

        private static bool IsThingExemptFromTimeStop(Thing thing, AbilityTimeStopState state)
        {
            if (thing.thingIDNumber == state.casterThingId)
            {
                return true;
            }

            if (thing is Projectile projectile && projectile.Launcher != null && projectile.Launcher.thingIDNumber == state.casterThingId)
            {
                return true;
            }

            if (thing is PawnFlyer flyer && flyer.FlyingPawn != null && flyer.FlyingPawn.thingIDNumber == state.casterThingId)
            {
                return true;
            }

            IThingHolder? holder = thing.ParentHolder;
            while (holder != null)
          {
                if (holder is Pawn p && p.thingIDNumber == state.casterThingId)
                {
                    return true;
                }
                holder = holder.ParentHolder;
            }

            return false;
        }

        private static bool MapStillExists(int mapId)
        {
            return Current.Game?.Maps?.Any(map => map != null && map.uniqueID == mapId) == true;
        }

        private static void ReleaseCasterForTimeStop(Pawn caster)
        {
            caster.stances?.CancelBusyStanceHard();
        }
    }

    public sealed class AbilityTimeStopState : IExposable
    {
        public int mapId = -1;
        public int casterThingId = -1;
        public int startTick = -1;
        public int endTick = -1;
        public int frozenVisualTick = -1;
        public bool freezeVisualsDuringTimeStop = true;

        public bool IsActive(int nowTick)
        {
            return mapId >= 0 && casterThingId >= 0 && endTick >= nowTick;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Values.Look(ref casterThingId, "casterThingId", -1);
            Scribe_Values.Look(ref startTick, "startTick", -1);
            Scribe_Values.Look(ref endTick, "endTick", -1);
            Scribe_Values.Look(ref frozenVisualTick, "frozenVisualTick", -1);
            Scribe_Values.Look(ref freezeVisualsDuringTimeStop, "freezeVisualsDuringTimeStop", true);
        }
    }

    public static class Patch_AbilityTimeStop
    {
        // RimWorld WindManager 通过此 shader 属性驱动树木/植物摇动
        private static readonly int WindSpeedShaderId = Shader.PropertyToID("_WindSpeed");

        private static readonly System.Reflection.FieldInfo? FleckManagerParentField =
            AccessTools.Field(typeof(FleckManager), "parent");
        private static readonly System.Reflection.FieldInfo? PostTickVisualsMapField =
            AccessTools.Field(typeof(PostTickVisuals), "map");
        private static readonly System.Reflection.FieldInfo? WeatherManagerMapField =
            AccessTools.Field(typeof(WeatherManager), "map");
        private static readonly System.Reflection.FieldInfo? WindManagerMapField =
            AccessTools.Field(typeof(WindManager), "map");

        public static void Apply(Harmony harmony)
        {
            PatchPrefix(harmony, typeof(Thing), nameof(Thing.DoTick), nameof(Thing_DoTick_Prefix));
            PatchPrefix(harmony, typeof(ThingWithComps), "TickLong", nameof(ThingWithComps_TickLong_Prefix));
            PatchPrefix(harmony, typeof(ThingWithComps), "TickRare", nameof(ThingWithComps_TickRare_Prefix));
            PatchPrefix(harmony, typeof(Pawn), "TickRare", nameof(Pawn_TickRare_Prefix));
            // Do NOT skip MapPreTick/MapPostTick entirely — doing so prevents
            // ALL pawn ticks from running (including the caster).  Per-entity
            // patches (Thing_DoTick, Pawn_TickRare, etc.) already freeze
            // non-exempt pawns while letting the caster act freely.
            PatchPrefix(harmony,
                AccessTools.PropertyGetter(typeof(Pawn_StanceTracker), nameof(Pawn_StanceTracker.FullBodyBusy)),
                nameof(Pawn_StanceTracker_FullBodyBusy_Prefix));
            PatchPrefix(harmony, typeof(FleckManager), nameof(FleckManager.FleckManagerUpdate), nameof(FleckManager_Update_Prefix));
            PatchPrefix(harmony, typeof(Mote), nameof(Mote.RealtimeUpdate), nameof(Mote_RealtimeUpdate_Prefix));
            PatchPrefix(harmony, typeof(PostTickVisuals), nameof(PostTickVisuals.ProcessPostTickVisuals), nameof(PostTickVisuals_ProcessPostTickVisuals_Prefix));
            PatchPrefix(harmony, typeof(WeatherManager), nameof(WeatherManager.WeatherManagerUpdate), nameof(WeatherManager_Update_Prefix));
            PatchPrefix(harmony, typeof(WindManager), "WindManagerUpdate", nameof(WindManager_Update_Prefix));
            PatchPrefix(harmony, typeof(MapComponentUtility), nameof(MapComponentUtility.MapComponentUpdate), nameof(MapComponentUtility_MapComponentUpdate_Prefix));

            PatchPostfix(harmony,
                AccessTools.Method(typeof(RimWorld.Planet.GlobalRendererUtility), nameof(RimWorld.Planet.GlobalRendererUtility.UpdateGlobalShadersParams)),
                nameof(GlobalRendererUtility_UpdateGlobalShadersParams_Postfix));

            Log.Message("[CharacterStudio] Patch_AbilityTimeStop 时停补丁已应用");
        }

        private static void PatchPrefix(Harmony harmony, Type type, string methodName, string prefixName)
        {
            var target = AccessTools.Method(type, methodName);
            var prefix = AccessTools.Method(typeof(Patch_AbilityTimeStop), prefixName);
            if (target != null && prefix != null)
            {
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
            }
            else
            {
                Log.Warning($"[CharacterStudio] Patch_AbilityTimeStop: 无法绑定 {type.Name}.{methodName} → {prefixName} (target={target != null}, prefix={prefix != null})");
            }
        }

        private static void PatchPrefix(Harmony harmony, System.Reflection.MethodBase? target, string prefixName)
        {
            var prefix = AccessTools.Method(typeof(Patch_AbilityTimeStop), prefixName);
            if (target != null && prefix != null)
            {
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
            }
            else
            {
                Log.Warning($"[CharacterStudio] Patch_AbilityTimeStop: 无法绑定 {(target?.DeclaringType?.Name ?? "null")}.{(target?.Name ?? "null")} → {prefixName} (target={target != null}, prefix={prefix != null})");
            }
        }

        private static void PatchPostfix(Harmony harmony, System.Reflection.MethodBase? target, string postfixName)
        {
            var postfix = AccessTools.Method(typeof(Patch_AbilityTimeStop), postfixName);
            if (target != null && postfix != null)
            {
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            }
        }

        public static bool Thing_DoTick_Prefix(Thing __instance)
        {
            return !AbilityTimeStopRuntimeController.ShouldSkipThingTick(__instance);
        }

        public static bool ThingWithComps_TickLong_Prefix(ThingWithComps __instance)
        {
            return !AbilityTimeStopRuntimeController.ShouldSkipThingTick(__instance);
        }

        public static bool ThingWithComps_TickRare_Prefix(ThingWithComps __instance)
        {
            return !AbilityTimeStopRuntimeController.ShouldSkipThingTick(__instance);
        }

        public static bool Pawn_TickRare_Prefix(Pawn __instance)
        {
            return !AbilityTimeStopRuntimeController.ShouldSkipThingTick(__instance);
        }


        public static bool Pawn_StanceTracker_FullBodyBusy_Prefix(Pawn_StanceTracker __instance, ref bool __result)
        {
            Pawn? pawn = __instance?.pawn;
            if (__instance == null)
            {
                return true;
            }

            // 时停期间非豁免角色强制 FullBodyBusy
            if (!AbilityTimeStopRuntimeController.CanPawnAct(pawn))
            {
                __result = true;
                return false;
            }

            // 时停施法者（CanPawnAct 返回 true）拥有完全行动自由，
            // 跳过下方的 IsForcedMoveBusy 检查，避免冲刺等强制位移状态残留导致无法转向
            if (AbilityTimeStopRuntimeController.HasActiveTimeStopOnCurrentMap())
            {
                return true;
            }

            // 正常冲刺等强制位移状态下阻止其他动作
            CompCharacterAbilityRuntime? abilityComp = pawn?.GetComp<CompCharacterAbilityRuntime>();
            if (abilityComp != null && abilityComp.IsForcedMoveBusy())
            {
                __result = true;
                return false;
            }

            return true;
        }

        public static bool FleckManager_Update_Prefix(FleckManager __instance)
        {
            Map? map = FleckManagerParentField?.GetValue(__instance) as Map;
            return !AbilityTimeStopRuntimeController.ShouldFreezeMapVisuals(map);
        }

        public static bool Mote_RealtimeUpdate_Prefix(Mote __instance)
        {
            return !AbilityTimeStopRuntimeController.ShouldFreezeMapVisuals(__instance.MapHeld);
        }

        public static bool PostTickVisuals_ProcessPostTickVisuals_Prefix(PostTickVisuals __instance)
        {
            Map? map = PostTickVisualsMapField?.GetValue(__instance) as Map;
            return !AbilityTimeStopRuntimeController.ShouldFreezeMapVisuals(map);
        }

        public static bool WeatherManager_Update_Prefix(WeatherManager __instance)
        {
            Map? map = WeatherManagerMapField?.GetValue(__instance) as Map;
            return !AbilityTimeStopRuntimeController.ShouldFreezeMapVisuals(map);
        }

        public static bool WindManager_Update_Prefix(WindManager __instance)
        {
            Map? map = WindManagerMapField?.GetValue(__instance) as Map;
            if (AbilityTimeStopRuntimeController.ShouldFreezeMapVisuals(map))
            {
                // 跳过 WindManager 更新后必须手动清零全局 shader 参数，
                // 否则 _WindSpeed 会保留上一帧的非零值导致树木/植物继续摇动。
                // 同时冻结 _GameSeconds 防止环境 shader 动画（水流等）继续推进。
                int currentTick = Find.TickManager?.TicksGame ?? 0;
                int frozenTick = AbilityTimeStopRuntimeController.ResolveGlobalVisualTickForCurrentMap(currentTick);
                Shader.SetGlobalFloat(WindSpeedShaderId, 0f);
                Shader.SetGlobalFloat(ShaderPropertyIDs.GameSeconds, frozenTick.TicksToSeconds());
                return false;
            }
            return true;
        }

        public static bool MapComponentUtility_MapComponentUpdate_Prefix(Map map)
        {
            return !AbilityTimeStopRuntimeController.ShouldFreezeMapVisuals(map);
        }

        /// <summary>
        /// 在 UpdateGlobalShadersParams 之后执行，覆盖原方法的 _GameSeconds 和 _WindSpeed 值。
        /// 这样施法者渲染逻辑查询 TicksGame 时能获取到真实的 live tick，
        /// 而全局 shader 动画（树木/水）和风力渲染仍会冻结。
        /// </summary>
        public static void GlobalRendererUtility_UpdateGlobalShadersParams_Postfix()
        {
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            int frozenTick = AbilityTimeStopRuntimeController.ResolveGlobalVisualTickForCurrentMap(currentTick);

            if (frozenTick != currentTick)
            {
                Shader.SetGlobalFloat(ShaderPropertyIDs.GameSeconds, frozenTick.TicksToSeconds());
                Shader.SetGlobalFloat(WindSpeedShaderId, 0f);
            }
        }
    }
}
