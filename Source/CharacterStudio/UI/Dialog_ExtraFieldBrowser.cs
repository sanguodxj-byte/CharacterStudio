using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    /// <summary>
    /// 字段补充器弹窗 - 支持中英文搜索，按类型分类浏览 ThingDef 可用字段。
    /// </summary>
    public class Dialog_ExtraFieldBrowser : Window
    {
        public override Vector2 InitialSize => new Vector2(520f, 640f);

        private string searchText = "";
        private Dictionary<string, string> currentFields;
        private Action<string> onFieldSelected;
        private static List<ExtraFieldEntry>? allFields;
        private Vector2 scrollPosition = Vector2.zero;

        public Dialog_ExtraFieldBrowser(Dictionary<string, string> currentFields, Action<string> onFieldSelected)
        {
            this.currentFields = currentFields;
            this.onFieldSelected = onFieldSelected;
            doCloseX = false;
            EnsureFieldCatalog();
        }

        private static void EnsureFieldCatalog()
        {
            if (allFields != null) return;
            allFields = new List<ExtraFieldEntry>
            {
                // ── 通用字段 ──
                new("thingClass", "类名", "ThingClass", "自定义 ThingClass 全限定名", "class,type,类型,类"),
                new("techLevel", "科技等级", "TechLevel", "科技等级(Neolithic/Medieval/Industrial/Spacer/Ultra等)", "tech,科技,等级,techlevel"),
                new("tradeability", "交易性", "Tradeability", "交易性(All/None/Sellable/Buyable)", "trade,交易,买卖,tradeability"),
                new("possessionCount", "持有数量", "PossessionCount", "初始持有数量", "possession,持有,数量,count"),
                new("uiIconScale", "UI图标缩放", "UIIconScale", "UI图标缩放比例", "ui,icon,scale,图标,缩放"),
                new("uiIconPath", "UI图标路径", "UIIconPath", "自定义UI图标纹理路径", "ui,icon,path,图标,路径"),
                new("uiIconOffset", "UI图标偏移", "UIIconOffset", "UI图标偏移量 格式:(x,y)", "ui,icon,offset,图标,偏移"),
                new("uiOrder", "UI排序", "UIOrder", "UI中排序权重", "ui,order,排序,顺序"),
                new("tickerType", "刷新类型", "TickerType", "刷新类型(Never/Normal/Rare)", "ticker,type,刷新,类型"),
                new("altitudeLayer", "高度层", "AltitudeLayer", "高度层(Item/Building等)", "altitude,layer,高度,层"),
                new("drawGUIOverlay", "绘制GUI覆盖", "DrawGUIOverlay", "是否绘制GUI覆盖层", "gui,overlay,覆盖,绘制"),
                new("alwaysHaulable", "始终可搬运", "AlwaysHaulable", "是否始终可搬运", "haulable,搬运,始终"),
                new("useHitPoints", "使用生命值", "UseHitPoints", "是否使用生命值系统", "hitpoints,生命值,hp"),
                new("selectable", "可选中", "Selectable", "是否可以被选中", "select,选中,选择"),
                new("rotatable", "可旋转", "Rotatable", "是否可以旋转", "rotate,旋转"),
                new("category", "分类", "Category", "物品分类(Item/Building/Pawn等)", "category,分类,类别"),
                new("descriptionHyperlinks", "描述超链接", "DescriptionHyperlinks", "描述中的超链接Def列表", "hyperlink,链接,描述"),
                new("socialPropernessMatters", "社交归属感", "SocialPropernessMatters", "是否影响社交归属感", "social,社交,归属"),
                new("destroyOnDrop", "丢弃即销毁", "DestroyOnDrop", "丢弃时立即销毁", "destroy,销毁,丢弃"),
                new("generateCommonality", "生成普遍性", "GenerateCommonality", "自然生成的普遍性", "commonality,普遍,生成"),
                new("relicChance", "圣物概率", "RelicChance", "成为圣物的概率", "relic,圣物,概率"),
                new("minRewardCount", "最小奖励数", "MinRewardCount", "任务最小奖励数量", "reward,奖励,最小"),
                new("allowedArchonexusCount", "遗迹方舟上限", "AllowedArchonexusCount", "遗迹方舟允许的数量", "archonexus,遗迹,方舟"),

                // ── 武器字段 ──
                new("equippedAngleOffset", "装备角度偏移", "EquippedAngleOffset", "装备时的角度偏移", "angle,角度,偏移,equipped"),
                new("defaultPlacingRot", "默认放置旋转", "DefaultPlacingRot", "默认放置时的旋转角度", "placing,放置,旋转"),
                new("colorGenerator", "颜色生成器", "ColorGenerator", "颜色随机生成器XML", "color,颜色,生成器"),
                new("drawOffscreen", "屏幕外绘制", "DrawOffscreen", "是否在屏幕外绘制", "offscreen,屏幕外,绘制"),
                new("burnableByRecipe", "配方可烧毁", "BurnableByRecipe", "是否可通过配方烧毁", "burn,烧毁,配方"),
                new("equippedStatOffsets", "装备属性偏移", "EquippedStatOffsets", "装备时的属性偏移列表", "equipped,stat,offset,装备,属性,偏移"),

                // ── 建筑字段 ──
                new("size", "建筑尺寸", "Size", "建筑尺寸 格式:(x,y)", "size,尺寸,大小"),
                new("passability", "通行性", "Passability", "通行性(Standable/PassThroughOnly/Impassable)", "pass,通行,通过"),
                new("fillPercent", "填充比例", "FillPercent", "填充比例(0-1)", "fill,填充,比例"),
                new("pathCost", "移动代价", "PathCost", "经过时的移动代价", "path,cost,路径,代价,移动"),
                new("castEdgeShadows", "投射边缘阴影", "CastEdgeShadows", "是否投射边缘阴影", "shadow,阴影,投射"),
                new("staticSunShadowHeight", "静态阳光阴影高度", "StaticSunShadowHeight", "静态阳光阴影高度", "sun,shadow,阳光,阴影"),
                new("blockWind", "挡风", "BlockWind", "是否阻挡风", "wind,风,挡风"),
                new("blockLight", "挡光", "BlockLight", "是否阻挡光线", "light,光,挡光"),
                new("canOverlapZones", "可重叠区域", "CanOverlapZones", "是否可以与区域重叠", "overlap,zone,重叠,区域"),
                new("leaveResourcesWhenKilled", "销毁留资源", "LeaveResourcesWhenKilled", "被摧毁时是否留下部分资源", "leave,resource,资源,销毁"),
                new("drawerType", "绘制器类型", "DrawerType", "绘制器类型(MapMeshOnly/RealtimeOnly/MapMeshAndRealtime)", "drawer,绘制器,类型"),
                new("surfaceType", "表面类型", "SurfaceType", "表面类型(Item/Eat等)", "surface,表面,类型"),
                new("terrainAffordanceNeeded", "地形需求", "TerrainAffordanceNeeded", "放置所需地形类型", "terrain,地形,需求,affordance"),
                new("placeWorkers", "放置工作器", "PlaceWorkers", "放置验证工作器类列表", "place,worker,放置,工作器"),
                new("clearBuildingArea", "清除建筑区域", "ClearBuildingArea", "放置时是否清除建筑区域", "clear,building,清除"),
                new("constructionSkillPrerequisite", "建造技能要求", "ConstructionSkillPrerequisite", "建造所需最低技能等级", "construction,skill,建造,技能"),
                new("holdsRoof", "支撑屋顶", "HoldsRoof", "是否支撑屋顶", "roof,屋顶,支撑"),
                new("coversFloor", "覆盖地板", "CoversFloor", "是否覆盖下方地板", "cover,floor,覆盖,地板"),
                new("neverMultiSelect", "禁止多选", "NeverMultiSelect", "是否禁止多选", "multiselect,多选"),
                new("hasTooltip", "有提示", "HasTooltip", "是否有鼠标悬停提示", "tooltip,提示"),
                new("fertility", "肥沃度", "Fertility", "地面肥沃度", "fertility,肥沃,肥沃度"),

                // ── 可食用字段 ──
                new("ingestible", "可食用", "Ingestible", "可食用属性子节点XML", "ingest,eat,食用,吃"),

                // ── 其他 ──
                new("stealable", "可偷窃", "Stealable", "是否可被偷窃", "steal,偷窃"),
                new("healthAffectsPrice", "耐久影响价格", "HealthAffectsPrice", "耐久度是否影响价格", "health,price,耐久,价格"),
                new("stackLimit", "堆叠上限", "StackLimit", "物品堆叠上限", "stack,limit,堆叠,上限"),
                new("resourceReadoutPriority", "资源显示优先级", "ResourceReadoutPriority", "资源显示优先级", "resource,readout,priority,资源,显示"),
                new("preventDroppingThingsOn", "禁止在上方丢弃", "PreventDroppingThingsOn", "禁止在该建筑上方丢弃物品", "prevent,drop,丢弃"),
                new("inspectorTabs", "检查器标签页", "InspectorTabs", "检查器标签页列表", "inspector,tabs,检查器,标签"),
                new("recipes", "配方列表", "Recipes", "建筑可用的配方列表", "recipe,配方,列表"),
                new("modExtensions", "Mod扩展", "ModExtensions", "Mod扩展XML", "mod,extension,扩展"),
            };
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect contentRect = UIHelper.DrawDialogFrame(inRect, this);
            float y = 0f;
            float width = contentRect.width;

            // 标题
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(contentRect.x, contentRect.y + y, width, 30f), "CS_Studio_Equip_ExtraFieldBrowser_Title".Translate());
            y += 34f;
            Text.Font = GameFont.Small;

            // 搜索框
            string searchTooltip = "CS_Studio_Equip_ExtraFieldBrowser_Search_Tip".Translate();
            UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Search".Translate(), ref searchText, tooltip: searchTooltip);
            y += 4f;

            // 字段列表
            float listHeight = contentRect.height - y - 4f;
            var filtered = GetFilteredFields();
            float entryHeight = 36f;
            float totalHeight = filtered.Count * entryHeight + 10f;

            Rect viewRect = new Rect(0, y, width - 16f, totalHeight);
            Rect scrollRect = new Rect(contentRect.x, contentRect.y + y, width, listHeight);
            Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);

            float ly = 0f;
            for (int i = 0; i < filtered.Count; i++)
            {
                var entry = filtered[i];
                if (ly + entryHeight < scrollPosition.y - entryHeight)
                {
                    ly += entryHeight;
                    continue;
                }
                if (ly > scrollPosition.y + listHeight + entryHeight)
                {
                    ly += entryHeight;
                    continue;
                }

                Rect rowRect = new Rect(0, ly, viewRect.width, entryHeight - 2f);
                bool isAdded = currentFields != null && currentFields.ContainsKey(entry.xmlName);

                if (isAdded)
                    Widgets.DrawHighlight(rowRect);
                else if (i % 2 == 0)
                    Widgets.DrawLightHighlight(rowRect);

                // 字段名
                Text.Font = GameFont.Small;
                string displayName = $"{entry.displayName} ({entry.xmlName})";
                Rect labelRect = new Rect(4, ly + 2f, viewRect.width - 80f, 18f);
                Widgets.Label(labelRect, displayName);
                // 描述
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                Rect descRect = new Rect(4, ly + 18f, viewRect.width - 80f, 14f);
                Widgets.Label(descRect, entry.description);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;

                // 添加/已添加 按钮
                Rect btnRect = new Rect(viewRect.width - 72f, ly + 6f, 68f, 24f);
                if (isAdded)
                {
                    Widgets.DrawHighlight(btnRect);
                    Widgets.Label(btnRect, "  ✓");
                }
                else
                {
                    if (Widgets.ButtonText(btnRect, "+ " + "CS_Studio_Add".Translate()))
                    {
                        onFieldSelected?.Invoke(entry.xmlName);
                    }
                }

                ly += entryHeight;
            }

            Widgets.EndScrollView();
        }

        private List<ExtraFieldEntry> GetFilteredFields()
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return allFields ?? new List<ExtraFieldEntry>();

            string lowerSearch = searchText.ToLowerInvariant();
            return allFields.Where(f =>
                f.xmlName.ToLowerInvariant().Contains(lowerSearch) ||
                f.displayName.Contains(searchText) ||
                f.searchKeywords.ToLowerInvariant().Contains(lowerSearch) ||
                f.description.Contains(searchText)
            ).ToList();
        }

        private class ExtraFieldEntry
        {
            public string xmlName;
            public string displayName;
            public string englishName;
            public string description;
            public string searchKeywords;

            public ExtraFieldEntry(string xmlName, string displayName, string englishName, string description, string searchKeywords)
            {
                this.xmlName = xmlName;
                this.displayName = displayName;
                this.englishName = englishName;
                this.description = description;
                this.searchKeywords = searchKeywords;
            }
        }
    }
}
