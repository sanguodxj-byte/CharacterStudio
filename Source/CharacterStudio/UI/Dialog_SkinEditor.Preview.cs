using System;
using System.Collections.Generic;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        private void ApplyPreviewExpressionOverride(bool enabled, ExpressionType expression)
        {
            previewExpressionOverrideEnabled = enabled;
            previewExpression = expression;

            if (enabled)
            {
                previewAutoPlayEnabled = false;
            }

            var previewPawn = mannequin?.CurrentPawn;
            var skinComp = previewPawn?.GetComp<CompPawnSkin>();
            skinComp?.SetPreviewExpressionOverride(enabled ? expression : null);
            RefreshPreview();
        }

        private void ApplyPreviewEyeDirectionOverride(bool enabled, EyeDirection direction)
        {
            previewEyeDirectionOverrideEnabled = enabled;
            previewEyeDirection = direction;

            if (enabled)
            {
                previewAutoPlayEnabled = false;
            }

            var previewPawn = mannequin?.CurrentPawn;
            var skinComp = previewPawn?.GetComp<CompPawnSkin>();
            skinComp?.SetPreviewEyeDirection(enabled ? direction : null);
            RefreshPreview();
        }

        private void OpenPreviewEyeDirectionMenu()
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_Face_PreviewAuto".Translate(), () => ApplyPreviewEyeDirectionOverride(false, previewEyeDirection))
            };

            foreach (EyeDirection direction in Enum.GetValues(typeof(EyeDirection)))
            {
                EyeDirection localDirection = direction;
                options.Add(new FloatMenuOption(localDirection.ToString(), () => ApplyPreviewEyeDirectionOverride(true, localDirection)));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ResetPreviewAutoPlayState(bool keepEnabled = false)
        {
            previewAutoPlayEnabled = keepEnabled;
            previewAutoPlayStepIndex = 0;
            previewAutoPlayNextStepTime = Time.realtimeSinceStartup + previewAutoPlayIntervalSeconds;
        }

        private void ApplyPreviewAutoPlayStep()
        {
            if (!previewAutoPlayEnabled)
            {
                return;
            }

            previewExpressionOverrideEnabled = true;
            previewEyeDirectionOverrideEnabled = true;
            previewMouthStateOverrideEnabled = false;
            previewLidStateOverrideEnabled = false;
            previewBrowStateOverrideEnabled = false;
            previewEmotionStateOverrideEnabled = false;

            previewExpression = PreviewAutoPlayExpressions[previewAutoPlayStepIndex % PreviewAutoPlayExpressions.Length];
            previewEyeDirection = PreviewAutoPlayEyeDirections[previewAutoPlayStepIndex % PreviewAutoPlayEyeDirections.Length];
            previewAutoPlayStepIndex++;

            var previewPawn = mannequin?.CurrentPawn;
            var skinComp = previewPawn?.GetComp<CompPawnSkin>();
            if (skinComp == null)
            {
                return;
            }

            skinComp.SetPreviewExpressionOverride(previewExpression);
            skinComp.SetPreviewEyeDirection(previewEyeDirection);
            skinComp.SetPreviewMouthState(null);
            skinComp.SetPreviewLidState(null);
            skinComp.SetPreviewBrowState(null);
            skinComp.SetPreviewEmotionOverlayState(null);
            skinComp.RequestRenderRefresh();
        }

        private void UpdatePreviewAutoPlay()
        {
            if (!previewAutoPlayEnabled)
            {
                return;
            }

            if (PreviewAutoPlayExpressions.Length == 0 || PreviewAutoPlayEyeDirections.Length == 0)
            {
                previewAutoPlayEnabled = false;
                return;
            }

            if (!EnsureMannequinReady())
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (previewAutoPlayNextStepTime <= 0f)
            {
                ResetPreviewAutoPlayState(keepEnabled: true);
                ApplyPreviewAutoPlayStep();
                return;
            }

            if (now < previewAutoPlayNextStepTime)
            {
                return;
            }

            previewAutoPlayNextStepTime = now + previewAutoPlayIntervalSeconds;
            ApplyPreviewAutoPlayStep();
        }
        

        private void OpenPreviewExpressionMenu()
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_Face_PreviewAuto".Translate(), () => ApplyPreviewExpressionOverride(false, previewExpression))
            };

            foreach (ExpressionType expression in Enum.GetValues(typeof(ExpressionType)))
            {
                ExpressionType localExpression = expression;
                options.Add(new FloatMenuOption(GetExpressionTypeLabel(localExpression), () => ApplyPreviewExpressionOverride(true, localExpression)));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ApplyPreviewMouthStateOverride(bool enabled, MouthState state)
        {
            previewMouthStateOverrideEnabled = enabled;
            previewMouthState = state;

            var previewPawn = mannequin?.CurrentPawn;
            var skinComp = previewPawn?.GetComp<CompPawnSkin>();
            skinComp?.SetPreviewMouthState(enabled ? state : null);
            RefreshPreview();
        }

        private void OpenPreviewMouthStateMenu()
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_Face_PreviewAuto".Translate(), () => ApplyPreviewMouthStateOverride(false, previewMouthState))
            };

            foreach (MouthState state in Enum.GetValues(typeof(MouthState)))
            {
                MouthState localState = state;
                options.Add(new FloatMenuOption(localState.ToString(), () => ApplyPreviewMouthStateOverride(true, localState)));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ApplyPreviewLidStateOverride(bool enabled, LidState state)
        {
            previewLidStateOverrideEnabled = enabled;
            previewLidState = state;

            var previewPawn = mannequin?.CurrentPawn;
            var skinComp = previewPawn?.GetComp<CompPawnSkin>();
            skinComp?.SetPreviewLidState(enabled ? state : null);
            RefreshPreview();
        }

        private void OpenPreviewLidStateMenu()
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_Face_PreviewAuto".Translate(), () => ApplyPreviewLidStateOverride(false, previewLidState))
            };

            foreach (LidState state in Enum.GetValues(typeof(LidState)))
            {
                LidState localState = state;
                options.Add(new FloatMenuOption(localState.ToString(), () => ApplyPreviewLidStateOverride(true, localState)));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ApplyPreviewBrowStateOverride(bool enabled, BrowState state)
        {
            previewBrowStateOverrideEnabled = enabled;
            previewBrowState = state;

            var previewPawn = mannequin?.CurrentPawn;
            var skinComp = previewPawn?.GetComp<CompPawnSkin>();
            skinComp?.SetPreviewBrowState(enabled ? state : null);
            RefreshPreview();
        }

        private void OpenPreviewBrowStateMenu()
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_Face_PreviewAuto".Translate(), () => ApplyPreviewBrowStateOverride(false, previewBrowState))
            };

            foreach (BrowState state in Enum.GetValues(typeof(BrowState)))
            {
                BrowState localState = state;
                options.Add(new FloatMenuOption(localState.ToString(), () => ApplyPreviewBrowStateOverride(true, localState)));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ApplyPreviewEmotionStateOverride(bool enabled, EmotionOverlayState state)
        {
            previewEmotionStateOverrideEnabled = enabled;
            previewEmotionState = state;

            var previewPawn = mannequin?.CurrentPawn;
            var skinComp = previewPawn?.GetComp<CompPawnSkin>();
            skinComp?.SetPreviewEmotionOverlayState(enabled ? state : null);
            RefreshPreview();
        }

        private void OpenPreviewEmotionStateMenu()
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_Face_PreviewAuto".Translate(), () => ApplyPreviewEmotionStateOverride(false, previewEmotionState))
            };

            foreach (EmotionOverlayState state in Enum.GetValues(typeof(EmotionOverlayState)))
            {
                EmotionOverlayState localState = state;
                options.Add(new FloatMenuOption(localState.ToString(), () => ApplyPreviewEmotionStateOverride(true, localState)));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private string GetPreviewOverrideLabel(bool enabled, string value)
        {
            return enabled ? value : "CS_Studio_Face_PreviewAuto".Translate();
        }

        private void DrawPreviewOverrideButton(ref float x, float y, string label, string valueLabel, float labelWidth, float buttonWidth, Action onClick)
        {
            Widgets.Label(new Rect(x, y + 4f, labelWidth, 24f), label);

            Rect buttonRect = new Rect(x + labelWidth, y, buttonWidth, 24f);
            Widgets.DrawBoxSolid(buttonRect, UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(buttonRect.x, buttonRect.yMax - 2f, buttonRect.width, 2f), new Color(1f, 1f, 1f, 0.05f));
            GUI.color = Mouse.IsOver(buttonRect) ? UIHelper.HoverOutlineColor : UIHelper.BorderColor;
            Widgets.DrawBox(buttonRect, 1);
            GUI.color = Color.white;

            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.HeaderColor;
            Widgets.Label(buttonRect, valueLabel);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            if (Widgets.ButtonInvisible(buttonRect))
            {
                onClick();
            }

            x += labelWidth + buttonWidth + Margin;
        }

        // ─────────────────────────────────────────────
        // 预览面板
        // ─────────────────────────────────────────────

        private void DrawPreviewPanel(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, UIHelper.PanelFillColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;

            Rect titleRect = new Rect(rect.x + Margin, rect.y + Margin, rect.width - Margin * 2, 26f);
            Widgets.DrawBoxSolid(titleRect, UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(titleRect.x, titleRect.yMax - 2f, titleRect.width, 2f), UIHelper.AccentSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(titleRect, 1);
            GUI.color = Color.white;

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = UIHelper.HeaderColor;
            Widgets.Label(new Rect(titleRect.x + 8f, titleRect.y, titleRect.width - 16f, titleRect.height), "CS_Studio_Panel_Preview".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;

            bool DrawPreviewToolbarButton(Rect buttonRect, string label, Action action, bool accent = false)
            {
                Widgets.DrawBoxSolid(buttonRect, accent ? UIHelper.ActiveTabColor : UIHelper.PanelFillSoftColor);
                Widgets.DrawBoxSolid(new Rect(buttonRect.x, buttonRect.yMax - 2f, buttonRect.width, 2f), accent ? UIHelper.AccentColor : new Color(1f, 1f, 1f, 0.05f));
                GUI.color = Mouse.IsOver(buttonRect) ? UIHelper.HoverOutlineColor : UIHelper.BorderColor;
                Widgets.DrawBox(buttonRect, 1);
                GUI.color = Color.white;

                GameFont prevFont = Text.Font;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = accent ? Color.white : UIHelper.HeaderColor;
                Widgets.Label(buttonRect, label);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = prevFont;

                if (Widgets.ButtonInvisible(buttonRect))
                {
                    action();
                    return true;
                }

                return false;
            }

            float btnY = titleRect.yMax + 6f;
            float btnWidth = 40f;
            float btnHeight = Mathf.Max(ButtonHeight - 2f, 22f);
            float btnX = rect.x + Margin;

            DrawPreviewToolbarButton(new Rect(btnX, btnY, btnWidth, btnHeight), "◀", () =>
            {
                previewRotation.Rotate(RotationDirection.Counterclockwise);
                RefreshPreview();
            });
            btnX += btnWidth + Margin;

            DrawPreviewToolbarButton(new Rect(btnX, btnY, btnWidth, btnHeight), "▶", () =>
            {
                previewRotation.Rotate(RotationDirection.Clockwise);
                RefreshPreview();
            });
            btnX += btnWidth + Margin * 2f;

            DrawPreviewToolbarButton(new Rect(btnX, btnY, btnWidth, btnHeight), "-", () =>
            {
                previewZoom = Mathf.Max(0.5f, previewZoom - 0.1f);
            });
            btnX += btnWidth + Margin;

            DrawPreviewToolbarButton(new Rect(btnX, btnY, btnWidth, btnHeight), "+", () =>
            {
                previewZoom = Mathf.Min(2f, previewZoom + 0.1f);
            });
            btnX += btnWidth + Margin;

            DrawPreviewToolbarButton(new Rect(btnX, btnY, btnWidth + 20f, btnHeight), "↺", () =>
            {
                previewRotation = Rot4.South;
                previewZoom = 1f;
                RefreshPreview();
            });

            float autoPlayWidth = 100f;
            Rect autoPlayRect = new Rect(rect.xMax - Margin - autoPlayWidth, btnY, autoPlayWidth, btnHeight);
            bool nextAutoPlayEnabled = DrawPreviewToolbarButton(autoPlayRect, previewAutoPlayEnabled ? "▶ Auto" : "Auto", () =>
            {
                previewAutoPlayEnabled = !previewAutoPlayEnabled;
                if (previewAutoPlayEnabled)
                {
                    ResetPreviewAutoPlayState(keepEnabled: true);
                    ApplyPreviewAutoPlayStep();
                }
                else
                {
                    previewEyeDirectionOverrideEnabled = false;
                    RefreshPreview();
                }
            }, previewAutoPlayEnabled);
            TooltipHandler.TipRegion(autoPlayRect, "自动播放预览表情与视线方向（纯预览，不模拟Pawn行为）");

            Rect speedRect = new Rect(autoPlayRect.x - 66f, autoPlayRect.y, 60f, btnHeight);
            if (DrawPreviewToolbarButton(speedRect, $"{previewAutoPlayIntervalSeconds:0.00}s", () =>
            {
                previewAutoPlayIntervalSeconds = Mathf.Approximately(previewAutoPlayIntervalSeconds, 0.5f)
                    ? 0.75f
                    : Mathf.Approximately(previewAutoPlayIntervalSeconds, 0.75f)
                        ? 1.0f
                        : 0.5f;
                if (previewAutoPlayEnabled)
                {
                    previewAutoPlayNextStepTime = Time.realtimeSinceStartup + previewAutoPlayIntervalSeconds;
                }
            }))
            {
            }
            TooltipHandler.TipRegion(speedRect, "切换自动播放步进间隔：0.50 / 0.75 / 1.00 秒");

            // 表情 / 通道控制
            btnY += ButtonHeight + Margin;
            if (mannequin != null)
            {
                float controlX = rect.x + Margin;
                DrawPreviewOverrideButton(
                    ref controlX,
                    btnY,
                    "CS_Studio_Preview_Exp".Translate(),
                    GetPreviewOverrideLabel(previewExpressionOverrideEnabled, GetExpressionTypeLabel(previewExpression)),
                    36f,
                    150f,
                    OpenPreviewExpressionMenu);

                DrawPreviewOverrideButton(
                    ref controlX,
                    btnY,
                    "Eye",
                    GetPreviewOverrideLabel(previewEyeDirectionOverrideEnabled, previewEyeDirection.ToString()),
                    26f,
                    88f,
                    OpenPreviewEyeDirectionMenu);

                btnY += ButtonHeight + Margin;

                controlX = rect.x + Margin;
                DrawPreviewOverrideButton(
                    ref controlX,
                    btnY,
                    "Mouth",
                    GetPreviewOverrideLabel(previewMouthStateOverrideEnabled, previewMouthState.ToString()),
                    42f,
                    76f,
                    OpenPreviewMouthStateMenu);

                DrawPreviewOverrideButton(
                    ref controlX,
                    btnY,
                    "Lid",
                    GetPreviewOverrideLabel(previewLidStateOverrideEnabled, previewLidState.ToString()),
                    28f,
                    76f,
                    OpenPreviewLidStateMenu);

                DrawPreviewOverrideButton(
                    ref controlX,
                    btnY,
                    "Brow",
                    GetPreviewOverrideLabel(previewBrowStateOverrideEnabled, previewBrowState.ToString()),
                    36f,
                    76f,
                    OpenPreviewBrowStateMenu);

                btnY += ButtonHeight + Margin;

                controlX = rect.x + Margin;
                DrawPreviewOverrideButton(
                    ref controlX,
                    btnY,
                    "Emotion",
                    GetPreviewOverrideLabel(previewEmotionStateOverrideEnabled, previewEmotionState.ToString()),
                    52f,
                    96f,
                    OpenPreviewEmotionStateMenu);

                btnY += ButtonHeight + Margin;
            }

            Rect modeWrapRect = new Rect(rect.x + Margin, btnY, 210f, Mathf.Max(ButtonHeight - 2f, 22f));
            Widgets.DrawBoxSolid(modeWrapRect, UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(modeWrapRect.x, modeWrapRect.yMax - 2f, modeWrapRect.width, 2f), UIHelper.AccentSoftColor);
            GUI.color = Mouse.IsOver(modeWrapRect) ? UIHelper.HoverOutlineColor : UIHelper.BorderColor;
            Widgets.DrawBox(modeWrapRect, 1);
            GUI.color = Color.white;

            Rect labelRect = new Rect(modeWrapRect.x + 8f, modeWrapRect.y, modeWrapRect.width - 36f, modeWrapRect.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = UIHelper.HeaderColor;
            Widgets.Label(labelRect, "CS_Studio_Preview_EditPerFacing".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            bool currentMode = editLayerOffsetPerFacing;
            Widgets.Checkbox(new Vector2(modeWrapRect.xMax - 22f, modeWrapRect.y + 2f), ref currentMode);
            if (currentMode != editLayerOffsetPerFacing)
                editLayerOffsetPerFacing = currentMode;

            TooltipHandler.TipRegion(modeWrapRect, "CS_Studio_Preview_EditPerFacingTip".Translate());

            // 预览区域
            float previewY = btnY + modeWrapRect.height + Margin;
            float previewHeight = rect.height - previewY + rect.y - Margin;
            Rect previewRect = new Rect(rect.x + Margin, previewY, rect.width - Margin * 2, previewHeight);

            Widgets.DrawBoxSolid(previewRect, new Color(0.12f, 0.12f, 0.12f));
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(previewRect, 1);
            GUI.color = Color.white;

            // 绘制 Mannequin
            if (mannequin != null)
            {
                mannequin.DrawPreview(previewRect, previewRotation, previewZoom);
                DrawWeaponPreviewOverlay(previewRect);
                DrawReferenceGhostOverlay(previewRect);
                DrawMapTileReferenceGrid(previewRect);
            }
            else
            {
                // 占位文本
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(previewRect, "CS_Studio_Status_Loading".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            if (targetPawn != null)
            {
                TooltipHandler.TipRegion(previewRect, "CS_Studio_Preview_RefHint".Translate());
            }

            DrawPreviewHintsOverlay(previewRect);

            // 处理交互输入
            HandlePreviewInput(previewRect);

            // 绘制选中图层的高亮指示
            DrawSelectedLayerHighlight(previewRect);
        }

        private void DrawReferenceGhostOverlay(Rect previewRect)
        {
            // 使用帧开始时预读的状态，避免 IMGUI TextField 拦截 Input.GetKey
            if (targetPawn == null || !isHoldingReferenceGhost)
            {
                return;
            }

            try
            {
                Vector2 portraitSize = new Vector2(256f, 256f);
                var portrait = PortraitsCache.Get(
                    targetPawn,
                    portraitSize,
                    previewRotation,
                    MannequinManager.PreviewCameraOffset,
                    previewZoom
                );

                if (portrait == null)
                {
                    return;
                }

                float drawSize = Mathf.Min(previewRect.width, previewRect.height);

                // 按住 F 时：默认居中；若鼠标位于预览区域内则让参考虚影跟随鼠标
                float x = previewRect.x + (previewRect.width - drawSize) / 2f;
                float y = previewRect.y + (previewRect.height - drawSize) / 2f;
                if (Mouse.IsOver(previewRect))
                {
                    Vector2 mousePos = Event.current.mousePosition;
                    x = mousePos.x - drawSize * 0.5f;
                    y = mousePos.y - drawSize * 0.5f;

                    // 限制在预览框内，避免完全拖出可视区域
                    x = Mathf.Clamp(x, previewRect.x, previewRect.xMax - drawSize);
                    y = Mathf.Clamp(y, previewRect.y, previewRect.yMax - drawSize);
                }

                Rect drawRect = new Rect(x, y, drawSize, drawSize);

                Color prevColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, ReferenceGhostAlpha);
                GUI.DrawTexture(drawRect, portrait, ScaleMode.ScaleToFit, true);
                GUI.color = prevColor;
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 绘制参考虚影失败: {ex.Message}");
            }
        }

        private void DrawMapTileReferenceGrid(Rect previewRect)
        {
            float pixelsPerUnit = GetPreviewPixelsPerUnit(previewRect);
            if (pixelsPerUnit <= 1f)
            {
                return;
            }

            Vector2 center = previewRect.center;
            float halfTile = pixelsPerUnit * 0.5f;

            Vector2 topLeft = new Vector2(center.x - halfTile, center.y - halfTile);
            Vector2 topRight = new Vector2(center.x + halfTile, center.y - halfTile);
            Vector2 bottomRight = new Vector2(center.x + halfTile, center.y + halfTile);
            Vector2 bottomLeft = new Vector2(center.x - halfTile, center.y + halfTile);

            Color lineColor = new Color(0.78f, 0.86f, 1f, 0.32f);
            Color pointColor = new Color(0.92f, 0.96f, 1f, 0.92f);

            DrawDashedLine(topLeft, topRight, lineColor, 2f, 8f, 5f);
            DrawDashedLine(topRight, bottomRight, lineColor, 2f, 8f, 5f);
            DrawDashedLine(bottomRight, bottomLeft, lineColor, 2f, 8f, 5f);
            DrawDashedLine(bottomLeft, topLeft, lineColor, 2f, 8f, 5f);

            DrawReferencePoint(topLeft, pointColor, 5f);
            DrawReferencePoint(topRight, pointColor, 5f);
            DrawReferencePoint(bottomRight, pointColor, 5f);
            DrawReferencePoint(bottomLeft, pointColor, 5f);
        }

        private static void DrawDashedLine(Vector2 start, Vector2 end, Color color, float thickness, float dashLength, float gapLength)
        {
            float totalLength = Vector2.Distance(start, end);
            if (totalLength <= 0.001f)
            {
                return;
            }

            Vector2 direction = (end - start) / totalLength;
            float traveled = 0f;
            while (traveled < totalLength)
            {
                float currentDashLength = Mathf.Min(dashLength, totalLength - traveled);
                Vector2 dashStart = start + direction * traveled;
                Vector2 dashEnd = dashStart + direction * currentDashLength;
                Widgets.DrawLine(dashStart, dashEnd, color, thickness);
                traveled += dashLength + gapLength;
            }
        }

        private static void DrawReferencePoint(Vector2 position, Color color, float size)
        {
            float halfSize = size * 0.5f;
            Widgets.DrawBoxSolid(new Rect(position.x - halfSize, position.y - halfSize, size, size), color);
        }

        private float GetPreviewPixelsPerUnit(Rect previewRect)
        {
            return (Mathf.Min(previewRect.width, previewRect.height) / 1.5f) * previewZoom;
        }

        private void DrawWeaponPreviewOverlay(Rect previewRect)
        {
            var cfg = workingSkin.weaponRenderConfig?.carryVisual;
            if (cfg == null || !cfg.enabled)
            {
                return;
            }

            string texPath = cfg.GetTexPath(previewWeaponCarryState);
            Texture2D? texture = LoadPreviewWeaponTexture(texPath);
            if (texture == null)
            {
                return;
            }

            float pixelsPerUnit = GetPreviewPixelsPerUnit(previewRect);
            Vector3 offset = cfg.GetOffsetForRotation(previewRotation);
            Vector2 screenCenter = previewRect.center + new Vector2(offset.x, -(offset.y + offset.z)) * pixelsPerUnit;

            float baseHeight = Mathf.Max(18f, pixelsPerUnit * 0.55f * Mathf.Max(0.1f, cfg.scale.y));
            float aspect = texture.height > 0 ? texture.width / (float)texture.height : 1f;
            float width = Mathf.Max(18f, baseHeight * Mathf.Max(0.1f, cfg.scale.x) * aspect);
            Rect drawRect = new Rect(screenCenter.x - width * 0.5f, screenCenter.y - baseHeight * 0.5f, width, baseHeight);

            Color oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.95f);
            GUI.DrawTexture(drawRect, texture, ScaleMode.ScaleToFit, true);
            GUI.color = oldColor;
        }

        private Texture2D? LoadPreviewWeaponTexture(string texPath)
        {
            if (string.IsNullOrWhiteSpace(texPath))
            {
                return null;
            }

            if (System.IO.Path.IsPathRooted(texPath) || texPath.StartsWith("/"))
            {
                return CharacterStudio.Rendering.RuntimeAssetLoader.LoadTextureRaw(texPath);
            }

            return ContentFinder<Texture2D>.Get(texPath, false);
        }

        private void DrawSelectedLayerHighlight(Rect previewRect)
        {
            if (selectedLayerIndex < 0 || selectedLayerIndex >= workingSkin.layers.Count) return;
            var layer = workingSkin.layers[selectedLayerIndex];
            if (!layer.visible) return;

            Vector2 center = previewRect.center;
            // 估算比例 (需与 TrySelectLayerAt 保持一致)
            float pixelsPerUnit = GetPreviewPixelsPerUnit(previewRect);
            Vector3 displayOffset = GetDisplayedLayerOffsetForPreview(layer);
            Vector2 layerScreenPos = center + new Vector2(displayOffset.x, -displayOffset.z) * pixelsPerUnit;

            // 绘制黄色十字准星
            float size = 10f;
            Color highlightColor = new Color(1f, 0.8f, 0f, 0.6f);

            // 绘制
            GUI.color = highlightColor;
            // 横线
            Widgets.DrawLine(new Vector2(layerScreenPos.x - size, layerScreenPos.y), new Vector2(layerScreenPos.x + size, layerScreenPos.y), highlightColor, 2f);
            // 竖线
            Widgets.DrawLine(new Vector2(layerScreenPos.x, layerScreenPos.y - size), new Vector2(layerScreenPos.x, layerScreenPos.y + size), highlightColor, 2f);
            // 中心点
            Widgets.DrawBoxSolid(new Rect(layerScreenPos.x - 2, layerScreenPos.y - 2, 4, 4), highlightColor);

            GUI.color = Color.white;
        }

        private void HandlePreviewInput(Rect rect)
        {
            if (Mouse.IsOver(rect))
            {
                // 滚轮缩放
                if (Event.current.type == EventType.ScrollWheel)
                {
                    float delta = Event.current.delta.y;
                    if (delta > 0) previewZoom /= 1.1f;
                    else previewZoom *= 1.1f;
                    previewZoom = Mathf.Clamp(previewZoom, 0.1f, 5f);
                    Event.current.Use();
                }

                // Shift + 左键点击选中 (已根据反馈移除，但为了代码完整性保留方法定义，此处注释掉调用)
                /*
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && Event.current.shift)
                {
                    TrySelectLayerAt(Event.current.mousePosition, rect);
                    Event.current.Use();
                }
                */

                // 鼠标拖拽调整偏移（支持多选同步；基础槽位与图层保持一致；Shift 加速，Ctrl 精调）
                if (Event.current.type == EventType.MouseDrag && Event.current.button == 0)
                {
                    Vector2 delta = Event.current.delta;

                    float sensitivity = 0.0025f / Mathf.Max(0.25f, previewZoom);
                    if (Event.current.shift)
                    {
                        sensitivity *= 4f;
                    }
                    if (Event.current.control)
                    {
                        sensitivity *= 0.35f;
                    }

                    float dx = delta.x * sensitivity;
                    float dz = -delta.y * sensitivity; // 屏幕Y向下，世界Z向上

                    if (TryApplyDragToWeaponPreview(dx, dz))
                    {
                        Event.current.Use();
                    }
                    else if (TryApplyDragToSelectedEquipmentPreview(dx, dz))
                    {
                        Event.current.Use();
                    }
                    else
                    {
                        var targets = GetSelectedLayerTargets();
                        if (targets.Count > 0)
                        {
                            foreach (int idx in targets)
                            {
                                ApplyPreviewDeltaToLayer(workingSkin.layers[idx], dx, dz);
                            }

                            isDirty = true;
                            RefreshPreview();
                            Event.current.Use();
                        }
                        else if (TryApplyDragToSelectedBaseSlot(dx, dz))
                        {
                            Event.current.Use();
                        }
                    }
                }
            }

            // 方向键微调（图层与基础槽位都可用；Shift 加速，Ctrl 精调）
            if (Event.current.type == EventType.KeyDown)
            {
                if (GUIUtility.keyboardControl != 0 && !string.IsNullOrEmpty(GUI.GetNameOfFocusedControl()))
                {
                    return;
                }

                float step = 0.001f;
                if (Event.current.shift)
                {
                    step *= 5f;
                }
                if (Event.current.control)
                {
                    step *= 0.25f;
                }

                float dx = 0f;
                float dz = 0f;
                bool handled = true;
                switch (Event.current.keyCode)
                {
                    case KeyCode.LeftArrow: dx = -step; break;
                    case KeyCode.RightArrow: dx = step; break;
                    case KeyCode.UpArrow: dz = step; break;
                    case KeyCode.DownArrow: dz = -step; break;
                    default: handled = false; break;
                }

                if (handled)
                {
                    if (TryApplyNudgeToWeaponPreview(dx, dz))
                    {
                        Event.current.Use();
                    }
                    else if (TryApplyNudgeToSelectedEquipmentPreview(dx, dz))
                    {
                        Event.current.Use();
                    }
                    else
                    {
                        var targets = GetSelectedLayerTargets();
                        if (targets.Count > 0)
                        {
                            foreach (int idx in targets)
                            {
                                ApplyPreviewDeltaToLayer(workingSkin.layers[idx], dx, dz);
                            }

                            isDirty = true;
                            RefreshPreview();
                            Event.current.Use();
                        }
                        else if (TryApplyNudgeToSelectedBaseSlot(dx, dz))
                        {
                            Event.current.Use();
                        }
                    }
                }
            }
        }

        private void ApplyPreviewDeltaToLayer(PawnLayerConfig layer, float dx, float dz)
        {
            if (!editLayerOffsetPerFacing || previewRotation == Rot4.South)
            {
                layer.offset.x += dx;
                layer.offset.z += dz;
                return;
            }

            if (previewRotation == Rot4.North)
            {
                layer.offsetNorth.x += dx;
                layer.offsetNorth.z += dz;
                return;
            }

            if (previewRotation == Rot4.West)
            {
                layer.offsetEast.x -= dx;
                layer.offsetEast.z += dz;
                return;
            }

            layer.offsetEast.x += dx;
            layer.offsetEast.z += dz;
        }

        private bool TryApplyDragToWeaponPreview(float dx, float dz)
        {
            if (currentTab != EditorTab.Weapon)
            {
                return false;
            }

            workingSkin.weaponRenderConfig ??= new WeaponRenderConfig();
            workingSkin.weaponRenderConfig.carryVisual ??= new WeaponCarryVisualConfig();
            ApplyPreviewDeltaToWeaponConfig(workingSkin.weaponRenderConfig.carryVisual, dx, dz);
            isDirty = true;
            RefreshPreview();
            return true;
        }

        private bool TryApplyNudgeToWeaponPreview(float dx, float dz)
        {
            if (currentTab != EditorTab.Weapon)
            {
                return false;
            }

            workingSkin.weaponRenderConfig ??= new WeaponRenderConfig();
            workingSkin.weaponRenderConfig.carryVisual ??= new WeaponCarryVisualConfig();
            ApplyPreviewDeltaToWeaponConfig(workingSkin.weaponRenderConfig.carryVisual, dx, dz);
            isDirty = true;
            RefreshPreview();
            return true;
        }

        private bool TryApplyDragToSelectedEquipmentPreview(float dx, float dz)
        {
            if (currentTab != EditorTab.Equipment)
            {
                return false;
            }

            workingSkin.equipments ??= new List<CharacterEquipmentDef>();
            SanitizeEquipmentSelection();
            if (selectedEquipmentIndex < 0 || selectedEquipmentIndex >= workingSkin.equipments.Count)
            {
                return false;
            }

            var equipment = workingSkin.equipments[selectedEquipmentIndex] ?? new CharacterEquipmentDef();
            equipment.EnsureDefaults();
            workingSkin.equipments[selectedEquipmentIndex] = equipment;

            ApplyPreviewDeltaToLayer(equipment.visual, dx, dz);
            isDirty = true;
            RefreshPreview();
            return true;
        }

        private bool TryApplyNudgeToSelectedEquipmentPreview(float dx, float dz)
        {
            if (currentTab != EditorTab.Equipment)
            {
                return false;
            }

            workingSkin.equipments ??= new List<CharacterEquipmentDef>();
            SanitizeEquipmentSelection();
            if (selectedEquipmentIndex < 0 || selectedEquipmentIndex >= workingSkin.equipments.Count)
            {
                return false;
            }

            var equipment = workingSkin.equipments[selectedEquipmentIndex] ?? new CharacterEquipmentDef();
            equipment.EnsureDefaults();
            workingSkin.equipments[selectedEquipmentIndex] = equipment;

            ApplyPreviewDeltaToLayer(equipment.visual, dx, dz);
            isDirty = true;
            RefreshPreview();
            return true;
        }

        private void ApplyPreviewDeltaToWeaponConfig(WeaponCarryVisualConfig config, float dx, float dz)
        {
            if (!editLayerOffsetPerFacing || previewRotation == Rot4.South)
            {
                config.offset.x += dx;
                config.offset.z += dz;
                return;
            }

            if (previewRotation == Rot4.North)
            {
                config.offsetNorth.x += dx;
                config.offsetNorth.z += dz;
                return;
            }

            config.offsetEast.x += dx;
            config.offsetEast.z += dz;
        }

        private Vector3 GetEditableLayerOffsetForPreview(PawnLayerConfig layer)
        {
            if (!editLayerOffsetPerFacing || previewRotation == Rot4.South)
                return layer.offset;

            if (previewRotation == Rot4.North)
                return layer.offsetNorth;

            Vector3 offset = layer.offsetEast;
            if (previewRotation == Rot4.West)
                offset.x = -offset.x;

            return offset;
        }

        private void SetEditableLayerOffsetForPreview(PawnLayerConfig layer, Vector3 value)
        {
            if (!editLayerOffsetPerFacing || previewRotation == Rot4.South)
            {
                layer.offset = value;
                return;
            }

            if (previewRotation == Rot4.North)
            {
                layer.offsetNorth = value;
                return;
            }

            if (previewRotation == Rot4.West)
                value.x = -value.x;

            layer.offsetEast = value;
        }

        private Vector2 GetEditableLayerScaleForPreview(PawnLayerConfig layer)
        {
            if (!editLayerOffsetPerFacing || previewRotation == Rot4.South)
                return layer.scale;

            if (previewRotation == Rot4.North)
                return new Vector2(layer.scale.x * layer.scaleNorthMultiplier.x, layer.scale.y * layer.scaleNorthMultiplier.y);

            return new Vector2(layer.scale.x * layer.scaleEastMultiplier.x, layer.scale.y * layer.scaleEastMultiplier.y);
        }

        private void SetEditableLayerScaleForPreview(PawnLayerConfig layer, Vector2 value)
        {
            if (!editLayerOffsetPerFacing || previewRotation == Rot4.South)
            {
                layer.scale = value;
                return;
            }

            if (previewRotation == Rot4.North)
            {
                layer.scaleNorthMultiplier = new Vector2(layer.scale.x != 0f ? value.x / layer.scale.x : 1f, layer.scale.y != 0f ? value.y / layer.scale.y : 1f);
                return;
            }

            layer.scaleEastMultiplier = new Vector2(layer.scale.x != 0f ? value.x / layer.scale.x : 1f, layer.scale.y != 0f ? value.y / layer.scale.y : 1f);
        }

        private float GetEditableLayerRotationForPreview(PawnLayerConfig layer)
        {
            if (!editLayerOffsetPerFacing || previewRotation == Rot4.South)
                return layer.rotation;

            if (previewRotation == Rot4.North)
                return layer.rotation + layer.rotationNorthOffset;

            return previewRotation == Rot4.West ? layer.rotation - layer.rotationEastOffset : layer.rotation + layer.rotationEastOffset;
        }

        private void SetEditableLayerRotationForPreview(PawnLayerConfig layer, float value)
        {
            if (!editLayerOffsetPerFacing || previewRotation == Rot4.South)
            {
                layer.rotation = value;
                return;
            }

            if (previewRotation == Rot4.North)
            {
                layer.rotationNorthOffset = value - layer.rotation;
                return;
            }

            layer.rotationEastOffset = previewRotation == Rot4.West ? layer.rotation - value : value - layer.rotation;
        }

        private Vector3 GetDisplayedLayerOffsetForPreview(PawnLayerConfig layer)
        {
            Vector3 offset = layer.offset;

            if (previewRotation == Rot4.North)
            {
                offset += layer.offsetNorth;
            }
            else if (previewRotation == Rot4.East || previewRotation == Rot4.West)
            {
                Vector3 eastOffset = layer.offsetEast;
                if (previewRotation == Rot4.West)
                {
                    eastOffset.x = -eastOffset.x;
                }

                offset += eastOffset;
            }

            return offset;
        }

        private void DrawPreviewHintsOverlay(Rect previewRect)
        {
            string[] hints =
            {
                "滚轮=缩放",
                "拖拽=移动",
                "方向键=微调",
                "Shift=加速",
                "F=参考虚影"
            };

            float lineHeight = 18f;
            float padding = 8f;
            float width = 124f;
            float height = padding * 2f + hints.Length * lineHeight;

            Rect hintRect = new Rect(
                previewRect.x + 10f,
                previewRect.yMax - height - 10f,
                width,
                height);

            Widgets.DrawBoxSolid(hintRect, new Color(0f, 0f, 0f, 0.18f));

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;

            for (int i = 0; i < hints.Length; i++)
            {
                Rect lineRect = new Rect(
                    hintRect.x + padding,
                    hintRect.y + padding + i * lineHeight,
                    hintRect.width - padding * 2f,
                    lineHeight);

                GUI.color = new Color(1f, 1f, 1f, 0.58f);
                Widgets.Label(lineRect, hints[i]);
            }

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;
        }

        private bool TryApplyDragToSelectedBaseSlot(float dx, float dz)
        {
            if (selectedBaseSlotType == null)
            {
                return false;
            }

            workingSkin.baseAppearance ??= new BaseAppearanceConfig();
            var slot = workingSkin.baseAppearance.GetSlot(selectedBaseSlotType.Value);
            Vector3 offset = GetEditableOffsetForPreview(slot);
            offset.x += dx;
            offset.z += dz;
            SetEditableOffsetForPreview(slot, offset);

            isDirty = true;
            RefreshPreview();
            return true;
        }

        private bool TryApplyNudgeToSelectedBaseSlot(float dx, float dz)
        {
            if (selectedBaseSlotType == null)
            {
                return false;
            }

            workingSkin.baseAppearance ??= new BaseAppearanceConfig();
            var slot = workingSkin.baseAppearance.GetSlot(selectedBaseSlotType.Value);
            Vector3 offset = GetEditableOffsetForPreview(slot);
            offset.x += dx;
            offset.z += dz;
            SetEditableOffsetForPreview(slot, offset);

            isDirty = true;
            RefreshPreview();
            return true;
        }

        private Vector3 GetEditableOffsetForPreview(BaseAppearanceSlotConfig slot)
        {
            if (previewRotation == Rot4.North)
            {
                return slot.offsetNorth;
            }

            if (previewRotation == Rot4.East || previewRotation == Rot4.West)
            {
                return slot.offsetEast;
            }

            return slot.offset;
        }

        private void SetEditableOffsetForPreview(BaseAppearanceSlotConfig slot, Vector3 value)
        {
            if (previewRotation == Rot4.North)
            {
                slot.offsetNorth = value;
                return;
            }

            if (previewRotation == Rot4.East || previewRotation == Rot4.West)
            {
                slot.offsetEast = value;
                return;
            }

            slot.offset = value;
        }

        private Vector2 GetEditableSlotScaleForPreview(BaseAppearanceSlotConfig slot)
        {
            if (!editLayerOffsetPerFacing || previewRotation == Rot4.South)
                return slot.scale;

            if (previewRotation == Rot4.North)
                return new Vector2(slot.scale.x * slot.scaleNorthMultiplier.x, slot.scale.y * slot.scaleNorthMultiplier.y);

            return new Vector2(slot.scale.x * slot.scaleEastMultiplier.x, slot.scale.y * slot.scaleEastMultiplier.y);
        }

        private void SetEditableSlotScaleForPreview(BaseAppearanceSlotConfig slot, Vector2 value)
        {
            if (!editLayerOffsetPerFacing || previewRotation == Rot4.South)
            {
                slot.scale = value;
                return;
            }

            if (previewRotation == Rot4.North)
            {
                slot.scaleNorthMultiplier = new Vector2(slot.scale.x != 0f ? value.x / slot.scale.x : 1f, slot.scale.y != 0f ? value.y / slot.scale.y : 1f);
                return;
            }

            slot.scaleEastMultiplier = new Vector2(slot.scale.x != 0f ? value.x / slot.scale.x : 1f, slot.scale.y != 0f ? value.y / slot.scale.y : 1f);
        }

        private float GetEditableSlotRotationForPreview(BaseAppearanceSlotConfig slot)
        {
            if (!editLayerOffsetPerFacing || previewRotation == Rot4.South)
                return slot.rotation;

            if (previewRotation == Rot4.North)
                return slot.rotation + slot.rotationNorthOffset;

            return previewRotation == Rot4.West ? slot.rotation - slot.rotationEastOffset : slot.rotation + slot.rotationEastOffset;
        }

        private void SetEditableSlotRotationForPreview(BaseAppearanceSlotConfig slot, float value)
        {
            if (!editLayerOffsetPerFacing || previewRotation == Rot4.South) { slot.rotation = value; return; }
            if (previewRotation == Rot4.North) { slot.rotationNorthOffset = value - slot.rotation; return; }
            slot.rotationEastOffset = previewRotation == Rot4.West ? slot.rotation - value : value - slot.rotation;
        }

        private void TrySelectLayerAt(Vector2 mousePos, Rect previewRect)
        {
            // 简单的距离检测选中算法
            // 假设 Pawn 在预览框中心
            Vector2 center = previewRect.center;

            // 估算世界单位到屏幕像素的转换比例
            // 假设标准 Pawn 高度约 1.8 单位，占满预览框时 height 对应 1.8
            // 实际上还要考虑 previewZoom
            float pixelsPerUnit = GetPreviewPixelsPerUnit(previewRect);
            float minDistance = 30f; // 选中阈值 (像素)
            int bestIndex = -1;

            for (int i = 0; i < workingSkin.layers.Count; i++)
            {
                var layer = workingSkin.layers[i];
                if (!layer.visible) continue;

                // 计算图层的大致屏幕位置
                // 注意：这里只考虑了 Offset，没有考虑 Body/Head 等锚点的基础位置
                // 这是一个简化的估算，假设用户主要通过 Offset 调整位置
                Vector3 displayOffset = GetDisplayedLayerOffsetForPreview(layer);
                Vector2 layerScreenPos = center + new Vector2(displayOffset.x, -displayOffset.z) * pixelsPerUnit;

                float dist = Vector2.Distance(mousePos, layerScreenPos);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestIndex = i;
                }
            }

            if (bestIndex != -1)
            {
                selectedLayerIndex = bestIndex;
                selectedNodePath = ""; // 清除节点选择
                // 自动滚动到列表位置
                // layerScrollPos.y = bestIndex * TreeNodeHeight; // 简单估算
                ShowStatus("CS_Studio_Msg_SelectedLayer".Translate(workingSkin.layers[bestIndex].layerName));
            }
        }
    }
}