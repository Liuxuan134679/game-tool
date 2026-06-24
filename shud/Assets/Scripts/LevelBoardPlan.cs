using UnityEngine;

namespace Shud
{
    public static class LevelBoardPlan
    {
        public const int FirstLevelIndex = 0;
        public const int LastLevelIndex = 43;
        private const int AreaEightNineStartLevel = 14;
        private const int AreaTenStartLevel = 24;
        private const int AreaTwelveStartLevel = 34;

        private static readonly int[] BaseRegionAreas = { 2, 3, 4, 5, 6 };
        private static readonly int[] EightNineRegionAreas = { 2, 3, 4, 5, 6, 8, 9 };
        private static readonly int[] TenRegionAreas = { 2, 3, 4, 5, 6, 8, 9, 10 };
        private static readonly int[] TwelveRegionAreas = { 2, 3, 4, 5, 6, 8, 9, 10, 12 };
        private static readonly int[] NoRequiredRegionAreas = new int[0];
        private static readonly int[] RequiredEightRegionAreas = { 8 };
        private static readonly int[] RequiredTenRegionAreas = { 10 };
        private static readonly int[] RequiredTwelveRegionAreas = { 12 };

        private static readonly Vector2Int[] TutorialSizes =
        {
            new Vector2Int(1, 2),
            new Vector2Int(2, 2),
            new Vector2Int(2, 3),
            new Vector2Int(3, 3)
        };

        private static readonly Vector2Int[] RegularSizes =
        {
            new Vector2Int(3, 4),
            new Vector2Int(4, 4),
            new Vector2Int(4, 5),
            new Vector2Int(5, 5),
            new Vector2Int(5, 6),
            new Vector2Int(6, 6),
            new Vector2Int(6, 7),
            new Vector2Int(7, 7)
        };

        public static Vector2Int GetBoardSize(int levelIndex)
        {
            int clampedIndex = Mathf.Clamp(levelIndex, FirstLevelIndex, LastLevelIndex);
            if (clampedIndex < TutorialSizes.Length)
            {
                return TutorialSizes[clampedIndex];
            }

            int regularOffset = clampedIndex - TutorialSizes.Length;
            for (int i = 0; i < RegularSizes.Length; i++)
            {
                Vector2Int size = RegularSizes[i];
                bool square = size.x == size.y;
                int levelCount = square ? 4 : 6;
                if (regularOffset < levelCount)
                {
                    if (!square && regularOffset >= 3)
                    {
                        return new Vector2Int(size.y, size.x);
                    }

                    return size;
                }

                regularOffset -= levelCount;
            }

            return RegularSizes[RegularSizes.Length - 1];
        }

        public static RegionAreaProfile GetRegionAreaProfile(int levelIndex)
        {
            int clampedIndex = Mathf.Clamp(levelIndex, FirstLevelIndex, LastLevelIndex);
            if (clampedIndex >= AreaTwelveStartLevel)
            {
                return new RegionAreaProfile(
                    TwelveRegionAreas,
                    clampedIndex == AreaTwelveStartLevel ? RequiredTwelveRegionAreas : NoRequiredRegionAreas,
                    maxLargeRegionCount: 2,
                    maxAreaTenCount: 1,
                    maxAreaTwelveCount: 1,
                    maxLargeCoverageRatio: 0.45f);
            }

            if (clampedIndex >= AreaTenStartLevel)
            {
                return new RegionAreaProfile(
                    TenRegionAreas,
                    clampedIndex == AreaTenStartLevel ? RequiredTenRegionAreas : NoRequiredRegionAreas,
                    maxLargeRegionCount: 2,
                    maxAreaTenCount: 1,
                    maxAreaTwelveCount: 0,
                    maxLargeCoverageRatio: 0.4f);
            }

            if (clampedIndex >= AreaEightNineStartLevel)
            {
                return new RegionAreaProfile(
                    EightNineRegionAreas,
                    clampedIndex == AreaEightNineStartLevel ? RequiredEightRegionAreas : NoRequiredRegionAreas,
                    maxLargeRegionCount: 1,
                    maxAreaTenCount: 0,
                    maxAreaTwelveCount: 0,
                    maxLargeCoverageRatio: 0.35f);
            }

            return new RegionAreaProfile(
                BaseRegionAreas,
                NoRequiredRegionAreas,
                maxLargeRegionCount: 0,
                maxAreaTenCount: 0,
                maxAreaTwelveCount: 0,
                maxLargeCoverageRatio: 0f);
        }

        public static RegionAreaProfile GetFallbackRegionAreaProfile()
        {
            return new RegionAreaProfile(
                BaseRegionAreas,
                NoRequiredRegionAreas,
                maxLargeRegionCount: 0,
                maxAreaTenCount: 0,
                maxAreaTwelveCount: 0,
                maxLargeCoverageRatio: 0f);
        }
    }

    public readonly struct RegionAreaProfile
    {
        public readonly int[] AllowedAreas;
        public readonly int[] RequiredAreas;
        public readonly int MaxLargeRegionCount;
        public readonly int MaxAreaTenCount;
        public readonly int MaxAreaTwelveCount;
        public readonly float MaxLargeCoverageRatio;

        public RegionAreaProfile(int[] allowedAreas, int[] requiredAreas, int maxLargeRegionCount, int maxAreaTenCount, int maxAreaTwelveCount, float maxLargeCoverageRatio)
        {
            AllowedAreas = allowedAreas;
            RequiredAreas = requiredAreas;
            MaxLargeRegionCount = maxLargeRegionCount;
            MaxAreaTenCount = maxAreaTenCount;
            MaxAreaTwelveCount = maxAreaTwelveCount;
            MaxLargeCoverageRatio = maxLargeCoverageRatio;
        }

        public bool HasAllowedAreas => AllowedAreas != null && AllowedAreas.Length > 0;
        public bool HasRequiredAreas => RequiredAreas != null && RequiredAreas.Length > 0;

        public int MinArea
        {
            get
            {
                if (!HasAllowedAreas)
                {
                    return 1;
                }

                int result = AllowedAreas[0];
                for (int i = 1; i < AllowedAreas.Length; i++)
                {
                    result = Mathf.Min(result, AllowedAreas[i]);
                }

                return result;
            }
        }

        public bool IsAllowed(int area)
        {
            return Contains(AllowedAreas, area);
        }

        public bool IsRequired(int area)
        {
            return Contains(RequiredAreas, area);
        }

        public bool IsLargeAllowed(int area)
        {
            return area >= 8 && IsAllowed(area);
        }

        public bool Accepts(System.Collections.Generic.IReadOnlyList<RectInt> rects, int boardArea)
        {
            if (rects == null || boardArea <= 0)
            {
                return false;
            }

            bool hasRequiredArea = !HasRequiredAreas;
            int largeRegionCount = 0;
            int largeCoverage = 0;
            int areaTenCount = 0;
            int areaTwelveCount = 0;

            for (int i = 0; i < rects.Count; i++)
            {
                int area = rects[i].width * rects[i].height;
                if (!IsAllowed(area))
                {
                    return false;
                }

                if (IsRequired(area))
                {
                    hasRequiredArea = true;
                }

                if (area >= 8)
                {
                    largeRegionCount++;
                    largeCoverage += area;
                }

                if (area == 10)
                {
                    areaTenCount++;
                }

                if (area == 12)
                {
                    areaTwelveCount++;
                }
            }

            if (!hasRequiredArea || largeRegionCount > MaxLargeRegionCount || areaTenCount > MaxAreaTenCount || areaTwelveCount > MaxAreaTwelveCount)
            {
                return false;
            }

            if (MaxLargeCoverageRatio <= 0f)
            {
                return true;
            }

            int maxLargeCoverage = Mathf.FloorToInt(boardArea * MaxLargeCoverageRatio);
            if (HasRequiredAreas)
            {
                maxLargeCoverage = Mathf.Max(maxLargeCoverage, GetSmallestRequiredArea());
            }

            return largeCoverage <= maxLargeCoverage;
        }

        private int GetSmallestRequiredArea()
        {
            int result = RequiredAreas[0];
            for (int i = 1; i < RequiredAreas.Length; i++)
            {
                result = Mathf.Min(result, RequiredAreas[i]);
            }

            return result;
        }

        private static bool Contains(int[] values, int value)
        {
            if (values == null)
            {
                return false;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == value)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
