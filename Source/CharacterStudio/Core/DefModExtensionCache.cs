using System.Collections.Generic;
using Verse;

namespace CharacterStudio.Core
{
    /// <summary>
    /// DefModExtension 启动时预缓存。
    /// 将 GetModExtension&lt;T&gt; 的热路径查询从 O(N) 遍历降为 O(1) 字典查找。
    ///
    /// 原理: RimWorld 的 GetModExtension&lt;T&gt; 内部遍历 def.modExtensions 数组做类型匹配。
    /// 在渲染管线中，Pawn 每次装备变化或渲染树重建都会重复调用，
    /// 通过启动时一次性扫描 DefDatabase 预构建字典来消除运行时开销。
    ///
    /// 覆盖的扩展类型:
    /// - DefModExtension_EquipmentRender: 装备渲染数据（挂载到 Apparel ThingDef）
    /// - DefModExtension_WeaponRender:   武器渲染数据（挂载到 Weapon ThingDef）
    /// - DefModExtension_SkinLink:       基因-皮肤关联（挂载到 GeneDef）
    /// </summary>
    public static class DefModExtensionCache
    {
        private static Dictionary<ThingDef, DefModExtension_EquipmentRender>? equipmentRenderByDef;
        private static Dictionary<ThingDef, DefModExtension_WeaponRender>? weaponRenderByDef;
        private static Dictionary<GeneDef, DefModExtension_SkinLink>? skinLinkByDef;

        /// <summary>
        /// 在 ModEntryPoint 启动流程中调用，一次性预构建所有缓存。
        /// 扫描 DefDatabase 中所有 ThingDef 和 GeneDef，提取关联的 DefModExtension。
        /// </summary>
        public static void BuildAll()
        {
            BuildEquipmentRenderCache();
            BuildWeaponRenderCache();
            BuildSkinLinkCache();
        }

        // ─────────────────────────────────────────────
        // EquipmentRender 缓存
        // ─────────────────────────────────────────────

        private static void BuildEquipmentRenderCache()
        {
            equipmentRenderByDef = new Dictionary<ThingDef, DefModExtension_EquipmentRender>();
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                var ext = def.GetModExtension<DefModExtension_EquipmentRender>();
                if (ext != null)
                {
                    equipmentRenderByDef[def] = ext;
                }
            }
        }

        /// <summary>
        /// 获取 ThingDef 关联的 EquipmentRender 扩展，O(1) 查找。
        /// 返回 null 表示该 Def 无此扩展或扩展未注册。
        /// </summary>
        public static DefModExtension_EquipmentRender? GetEquipmentRender(ThingDef? def)
        {
            if (def == null || equipmentRenderByDef == null) return null;
            equipmentRenderByDef.TryGetValue(def, out var ext);
            return ext;
        }

        // ─────────────────────────────────────────────
        // WeaponRender 缓存
        // ─────────────────────────────────────────────

        private static void BuildWeaponRenderCache()
        {
            weaponRenderByDef = new Dictionary<ThingDef, DefModExtension_WeaponRender>();
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                var ext = def.GetModExtension<DefModExtension_WeaponRender>();
                if (ext != null)
                {
                    weaponRenderByDef[def] = ext;
                }
            }
        }

        /// <summary>
        /// 获取 ThingDef 关联的 WeaponRender 扩展，O(1) 查找。
        /// 返回 null 表示该 Def 无此扩展或扩展未注册。
        /// </summary>
        public static DefModExtension_WeaponRender? GetWeaponRender(ThingDef? def)
        {
            if (def == null || weaponRenderByDef == null) return null;
            weaponRenderByDef.TryGetValue(def, out var ext);
            return ext;
        }

        // ─────────────────────────────────────────────
        // SkinLink 缓存
        // ─────────────────────────────────────────────

        private static void BuildSkinLinkCache()
        {
            skinLinkByDef = new Dictionary<GeneDef, DefModExtension_SkinLink>();
            foreach (var def in DefDatabase<GeneDef>.AllDefsListForReading)
            {
                var ext = def.GetModExtension<DefModExtension_SkinLink>();
                if (ext != null)
                {
                    skinLinkByDef[def] = ext;
                }
            }
        }

        /// <summary>
        /// 获取 GeneDef 关联的 SkinLink 扩展，O(1) 查找。
        /// 返回 null 表示该 Def 无此扩展或扩展未注册。
        /// </summary>
        public static DefModExtension_SkinLink? GetSkinLink(GeneDef? def)
        {
            if (def == null || skinLinkByDef == null) return null;
            skinLinkByDef.TryGetValue(def, out var ext);
            return ext;
        }

        // ─────────────────────────────────────────────
        // 运行时动态注册（供导出模组热加载使用）
        // ─────────────────────────────────────────────

        /// <summary>
        /// 当运行时动态创建/注册 ThingDef 并附带 EquipmentRender 扩展时调用。
        /// 确保后续缓存查询能命中。
        /// </summary>
        public static void RegisterEquipmentRender(ThingDef def, DefModExtension_EquipmentRender ext)
        {
            if (def == null || ext == null) return;
            if (equipmentRenderByDef == null)
                equipmentRenderByDef = new Dictionary<ThingDef, DefModExtension_EquipmentRender>();
            equipmentRenderByDef[def] = ext;
        }

        /// <summary>
        /// 当运行时动态创建/注册 ThingDef 并附带 WeaponRender 扩展时调用。
        /// </summary>
        public static void RegisterWeaponRender(ThingDef def, DefModExtension_WeaponRender ext)
        {
            if (def == null || ext == null) return;
            if (weaponRenderByDef == null)
                weaponRenderByDef = new Dictionary<ThingDef, DefModExtension_WeaponRender>();
            weaponRenderByDef[def] = ext;
        }

        /// <summary>
        /// 当运行时动态创建/注册 GeneDef 并附带 SkinLink 扩展时调用。
        /// </summary>
        public static void RegisterSkinLink(GeneDef def, DefModExtension_SkinLink ext)
        {
            if (def == null || ext == null) return;
            if (skinLinkByDef == null)
                skinLinkByDef = new Dictionary<GeneDef, DefModExtension_SkinLink>();
            skinLinkByDef[def] = ext;
        }
    }
}
