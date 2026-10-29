using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Shud
{
    /// <summary>
    /// 单个关卡的全套可配置参数（数据驱动）。
    /// 关卡索引 = 该条目在 <see cref="LevelConfig.levels"/> 列表中的下标。
    /// 留空 / 取默认值时会回退到全局默认或 FirstVersionGame 的现有设置（非破坏性）。
    /// </summary>
    [System.Serializable]
    public sealed class LevelSettings
    {
        [Header("棋盘尺寸")]
        [Tooltip("行数（对应 LevelData.rows）")]
        [Min(1)] public int rows = 4;

        [Tooltip("列数（对应 LevelData.columns）")]
        [Min(1)] public int columns = 4;

        [Header("区域面积约束")]
        [Tooltip("允许出现的区域面积集合。所有区域面积必须落在此集合内，否则该布局被拒绝。不能为空。")]
        public int[] allowedAreas = { 2, 3, 4, 5, 6 };

        [Tooltip("必须至少各出现一次的区域面积集合。留空表示无强制要求。")]
        public int[] requiredAreas = new int[0];

        [Header("大区域（面积≥8）上限")]
        [Tooltip("面积≥8 的大区域最多数量")]
        [Min(0)] public int maxLargeRegionCount = 0;

        [Tooltip("面积=10 的区域最多数量")]
        [Min(0)] public int maxAreaTenCount = 0;

        [Tooltip("面积=12 的区域最多数量")]
        [Min(0)] public int maxAreaTwelveCount = 0;

        [Tooltip("大区域（面积≥8）的总覆盖格数占棋盘比例上限。0 表示不限制。")]
        [Range(0f, 1f)] public float maxLargeCoverageRatio = 0f;

        [Header("生成参数（≤0 表示沿用全局默认）")]
        [Tooltip("本关区域数量上限。≤0 时沿用 LevelConfig.defaultMaxRegionCount / FirstVersionGame 颜色数。")]
        public int maxRegionCount = 0;

        [Tooltip("本关生成尝试次数。≤0 时沿用 LevelConfig.defaultGenerationAttempts / FirstVersionGame.generationAttempts。")]
        public int generationAttempts = 0;

        [Tooltip("勾选后使用下面的固定种子；否则使用 baseSeed + 关卡索引 * 步长 自动推导。")]
        public bool overrideSeed = false;

        [Tooltip("固定随机种子（仅当 overrideSeed 勾选时生效）。用于锁定某一关生成出的具体题面。")]
        public int seed = 0;

        [Header("玩法参数")]
        [Tooltip("勾选后用本关自定义限时；否则沿用 FirstVersionGame.levelSeconds。")]
        public bool overrideLevelSeconds = false;

        [Tooltip("本关限时（秒），仅当 overrideLevelSeconds 勾选时生效。")]
        [Min(0f)] public float levelSeconds = 60f;

        [Tooltip("勾选后用本关自定义冻结时长；否则沿用 FirstVersionGame.freezeSeconds。")]
        public bool overrideFreezeSeconds = false;

        [Tooltip("本关冻结道具时长（秒），仅当 overrideFreezeSeconds 勾选时生效。")]
        [Min(0f)] public float freezeSeconds = 10f;

        /// <summary>把本关的面积约束转换成生成器/校验器使用的 <see cref="RegionAreaProfile"/>。</summary>
        public RegionAreaProfile ToRegionAreaProfile()
        {
            return new RegionAreaProfile(
                allowedAreas != null ? allowedAreas : new int[0],
                requiredAreas != null ? requiredAreas : new int[0],
                maxLargeRegionCount,
                maxAreaTenCount,
                maxAreaTwelveCount,
                maxLargeCoverageRatio);
        }
    }

    /// <summary>
    /// 关卡配置工具类（ScriptableObject）。
    ///
    /// 用途：把原本硬编码在 <see cref="LevelBoardPlan"/> / FirstVersionGame 里的每关参数抽成可在 Inspector 编辑的数据。
    /// 关卡仍然走程序化生成（<see cref="LevelGenerator"/>），但棋盘尺寸、面积约束、限时、种子等由本资源驱动。
    ///
    /// 关键约定：
    ///   - 列表 <see cref="levels"/> 的下标 == 关卡索引（0 起）。配置激活时，可玩关卡数 = 列表长度。
    ///   - 某一关“没有条目”或“留默认值”时，会回退到 <see cref="LevelBoardPlan"/> / FirstVersionGame 的现有设置，
    ///     因此“空配置 + 打开开关”与“关闭开关”的运行表现完全一致（非破坏性）。
    ///
    /// 创建方式：在 Project 窗口右键 → Create → Shud → Level Config 生成 .asset，然后拖到 FirstVersionGame 上。
    /// </summary>
    [CreateAssetMenu(fileName = "LevelConfig", menuName = "Shud/Level Config", order = 0)]
    public sealed class LevelConfig : ScriptableObject
    {
        /// <summary>种子推导步长，与 FirstVersionGame 原有实现保持一致。</summary>
        public const int SeedStride = 10007;

        [Header("每关配置（下标 = 关卡索引，从 0 开始）")]
        [Tooltip("列表长度即配置激活时的可玩关卡数量。某一关留空时回退到 LevelBoardPlan。")]
        [SerializeField] private List<LevelSettings> levels = new List<LevelSettings>();

        [Header("全局默认值")]
        [Tooltip("自动推导种子的基准值（与 FirstVersionGame.levelSeed 默认一致）。某关未勾选 overrideSeed 时：seed = baseSeed + 关卡索引 * SeedStride。")]
        [SerializeField] private int baseSeed = 20260611;

        [Tooltip("区域数量上限的全局默认值。建议与 FirstVersionGame.regionColors 的数量保持一致（默认 13）。")]
        [SerializeField] private int defaultMaxRegionCount = 13;

        [Tooltip("生成尝试次数的全局默认值。")]
        [SerializeField] private int defaultGenerationAttempts = 300;

        /// <summary>配置中的关卡数量。</summary>
        public int LevelCount => levels != null ? levels.Count : 0;

        /// <summary>判断给定关卡索引是否存在有效配置条目。</summary>
        public bool HasLevel(int index)
        {
            return levels != null && index >= 0 && index < levels.Count && levels[index] != null;
        }

        /// <summary>取出某关的配置条目（不存在时返回 null）。</summary>
        public LevelSettings GetLevel(int index)
        {
            return HasLevel(index) ? levels[index] : null;
        }

        /// <summary>解析某关棋盘尺寸。返回 false 表示该关无配置条目（应回退到 LevelBoardPlan）。</summary>
        public bool TryGetBoardSize(int index, out int rows, out int columns)
        {
            if (HasLevel(index))
            {
                rows = levels[index].rows;
                columns = levels[index].columns;
                return true;
            }

            rows = 0;
            columns = 0;
            return false;
        }

        /// <summary>解析某关的区域面积约束；无配置条目时回退到 LevelBoardPlan。</summary>
        public RegionAreaProfile ResolveRegionAreaProfile(int index)
        {
            if (HasLevel(index))
            {
                return levels[index].ToRegionAreaProfile();
            }

            return LevelBoardPlan.GetRegionAreaProfile(index);
        }

        /// <summary>解析某关区域数量上限。优先级：本关值(&gt;0) → 全局默认(&gt;0) → 调用方 fallback。</summary>
        public int ResolveMaxRegionCount(int index, int fallback)
        {
            if (HasLevel(index) && levels[index].maxRegionCount > 0)
            {
                return levels[index].maxRegionCount;
            }

            return defaultMaxRegionCount > 0 ? defaultMaxRegionCount : fallback;
        }

        /// <summary>解析某关生成尝试次数。优先级：本关值(&gt;0) → 全局默认(&gt;0) → 调用方 fallback。</summary>
        public int ResolveGenerationAttempts(int index, int fallback)
        {
            if (HasLevel(index) && levels[index].generationAttempts > 0)
            {
                return levels[index].generationAttempts;
            }

            return defaultGenerationAttempts > 0 ? defaultGenerationAttempts : fallback;
        }

        /// <summary>解析某关随机种子。勾选 overrideSeed 用固定值，否则自动推导（与运行时/校验保持一致）。</summary>
        public int ResolveSeed(int index)
        {
            if (HasLevel(index) && levels[index].overrideSeed)
            {
                return levels[index].seed;
            }

            return baseSeed + index * SeedStride;
        }

        /// <summary>解析某关限时（秒）。未勾选 override 时返回调用方 fallback。</summary>
        public float ResolveLevelSeconds(int index, float fallback)
        {
            if (HasLevel(index) && levels[index].overrideLevelSeconds)
            {
                return levels[index].levelSeconds;
            }

            return fallback;
        }

        /// <summary>解析某关冻结时长（秒）。未勾选 override 时返回调用方 fallback。</summary>
        public float ResolveFreezeSeconds(int index, float fallback)
        {
            if (HasLevel(index) && levels[index].overrideFreezeSeconds)
            {
                return levels[index].freezeSeconds;
            }

            return fallback;
        }

        // ───────────────────────────── 可解性校验工具 ─────────────────────────────

        /// <summary>
        /// 校验某一关：用配置参数尝试生成，并确认存在唯一解。
        /// 注意：此处只校验“配置的目标参数”本身，不使用运行时的降级兜底，
        /// 因此失败信息能真实反映“当前参数能否直接切出唯一解题面”。
        /// </summary>
        public bool TryValidateLevel(int index, out string message)
        {
            if (!HasLevel(index))
            {
                message = "无配置条目（运行时将回退到 LevelBoardPlan）";
                return false;
            }

            LevelSettings s = levels[index];
            if (s.rows <= 0 || s.columns <= 0)
            {
                message = "非法棋盘尺寸 rows=" + s.rows + ", columns=" + s.columns;
                return false;
            }

            RegionAreaProfile profile = s.ToRegionAreaProfile();
            if (!profile.HasAllowedAreas)
            {
                message = "allowedAreas 为空：必须至少配置一个允许面积";
                return false;
            }

            int maxRegionCount = ResolveMaxRegionCount(index, defaultMaxRegionCount > 0 ? defaultMaxRegionCount : 13);
            int attempts = ResolveGenerationAttempts(index, defaultGenerationAttempts > 0 ? defaultGenerationAttempts : 300);
            int seed = ResolveSeed(index);

            if (!LevelGenerator.TryGenerate(s.rows, s.columns, seed, profile, maxRegionCount, attempts, out LevelData level))
            {
                message = "生成失败：当前参数无法在 " + attempts +
                          " 次尝试内切出满足约束且唯一解的布局（可调大 generationAttempts、放宽 allowedAreas，或提高 maxRegionCount）";
                return false;
            }

            int solutions = PuzzleSolver.CountSolutions(level, 2);
            if (solutions != 1)
            {
                message = "生成的关卡解数 = " + solutions + "（期望唯一解）";
                return false;
            }

            message = "OK 行" + s.rows + " x 列" + s.columns + "，区域数 = " + level.clues.Count + "，seed = " + seed;
            return true;
        }

        /// <summary>校验全部关卡，返回可读报告。</summary>
        public string ValidateAll()
        {
            var sb = new StringBuilder();
            int total = LevelCount;
            sb.AppendLine("[LevelConfig] 开始校验，共 " + total + " 关");

            int passed = 0;
            for (int i = 0; i < total; i++)
            {
                bool ok = TryValidateLevel(i, out string msg);
                if (ok)
                {
                    passed++;
                }

                sb.AppendLine("  关卡 " + i + ": " + (ok ? "通过" : "失败") + " - " + msg);
            }

            sb.AppendLine("[LevelConfig] 通过 " + passed + " / " + total);
            return sb.ToString();
        }

        [ContextMenu("校验全部关卡 (Validate All Levels)")]
        private void ValidateAllLevelsContextMenu()
        {
            if (LevelCount == 0)
            {
                Debug.LogWarning("[LevelConfig] 当前没有任何关卡配置，无需校验。");
                return;
            }

            Debug.Log(ValidateAll());
        }

        // ───────────────────────────── 可选：从现有 LevelBoardPlan 填充 ─────────────────────────────
        // 默认资源为空。需要以“现有 0–43 关调参”为起点时，可手动右键执行此菜单（会覆盖现有列表）。

        [ContextMenu("从 LevelBoardPlan 填充 (覆盖现有，可选)")]
        private void PopulateFromLevelBoardPlan()
        {
            var generated = new List<LevelSettings>();
            for (int i = LevelBoardPlan.FirstLevelIndex; i <= LevelBoardPlan.LastLevelIndex; i++)
            {
                Vector2Int size = LevelBoardPlan.GetBoardSize(i);
                RegionAreaProfile profile = LevelBoardPlan.GetRegionAreaProfile(i);
                generated.Add(new LevelSettings
                {
                    rows = size.x,
                    columns = size.y,
                    allowedAreas = CloneOrEmpty(profile.AllowedAreas),
                    requiredAreas = CloneOrEmpty(profile.RequiredAreas),
                    maxLargeRegionCount = profile.MaxLargeRegionCount,
                    maxAreaTenCount = profile.MaxAreaTenCount,
                    maxAreaTwelveCount = profile.MaxAreaTwelveCount,
                    maxLargeCoverageRatio = profile.MaxLargeCoverageRatio
                });
            }

            levels = generated;
            Debug.Log("[LevelConfig] 已从 LevelBoardPlan 填充 " + levels.Count + " 关（生成/玩法参数留默认，运行时回退到 FirstVersionGame 设置）。");
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private static int[] CloneOrEmpty(int[] source)
        {
            if (source == null || source.Length == 0)
            {
                return new int[0];
            }

            var copy = new int[source.Length];
            System.Array.Copy(source, copy, source.Length);
            return copy;
        }
    }
}
