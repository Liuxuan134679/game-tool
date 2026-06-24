using System.Collections.Generic;
using UnityEngine;

namespace Shud
{
    public static class LevelGenerator
    {
        public static bool TryGenerate(int rows, int columns, int seed, RegionAreaProfile areaProfile, int maxRegionCount, int maxAttempts, out LevelData level)
        {
            level = null;

            if (rows <= 0 || columns <= 0 || maxRegionCount <= 0 || maxAttempts <= 0 || !areaProfile.HasAllowedAreas)
            {
                return false;
            }

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var random = new System.Random(seed + attempt * 7919);
                List<RectInt> solutionRects = BuildSolutionRects(rows, columns, areaProfile, maxRegionCount, random);
                if (solutionRects == null || !areaProfile.Accepts(solutionRects, rows * columns))
                {
                    continue;
                }

                LevelData candidate = BuildLevel(rows, columns, solutionRects, random);
                if (PuzzleSolver.HasUniqueSolution(candidate))
                {
                    level = candidate;
                    return true;
                }
            }

            return false;
        }

        public static bool TryGenerateTiledFallback(int rows, int columns, int seed, RegionAreaProfile areaProfile, int maxRegionCount, out LevelData level)
        {
            level = null;
            if (rows <= 0 || columns <= 0 || maxRegionCount <= 0 || !areaProfile.HasAllowedAreas)
            {
                return false;
            }

            if (!TryBuildTiledFallbackRects(rows, columns, areaProfile, maxRegionCount, out List<RectInt> solutionRects) || !areaProfile.Accepts(solutionRects, rows * columns))
            {
                return false;
            }

            LevelData firstCandidate = null;
            for (int attempt = 0; attempt < 32; attempt++)
            {
                var random = new System.Random(seed + attempt * 131);
                LevelData candidate = BuildLevel(rows, columns, solutionRects, random);
                firstCandidate ??= candidate;
                if (PuzzleSolver.HasUniqueSolution(candidate))
                {
                    level = candidate;
                    return true;
                }
            }

            level = firstCandidate;
            return level != null;
        }

        private static List<RectInt> BuildSolutionRects(int rows, int columns, RegionAreaProfile areaProfile, int maxRegionCount, System.Random random)
        {
            var rects = new List<RectInt>
            {
                new RectInt(0, 0, columns, rows)
            };

            for (int guard = 0; guard < rows * columns * 4; guard++)
            {
                int splitIndex = ChooseSplitRect(rects, areaProfile, maxRegionCount, random);
                if (splitIndex < 0)
                {
                    break;
                }

                RectInt rect = rects[splitIndex];
                if (!TrySplit(rect, areaProfile, random, out RectInt first, out RectInt second))
                {
                    if (!areaProfile.IsAllowed(rect.width * rect.height))
                    {
                        return null;
                    }

                    break;
                }

                rects[splitIndex] = first;
                rects.Add(second);
                if (rects.Count > maxRegionCount)
                {
                    return null;
                }
            }

            for (int i = 0; i < rects.Count; i++)
            {
                int area = rects[i].width * rects[i].height;
                if (!areaProfile.IsAllowed(area))
                {
                    return null;
                }
            }

            return rects;
        }

        private static int ChooseSplitRect(List<RectInt> rects, RegionAreaProfile areaProfile, int maxRegionCount, System.Random random)
        {
            var forced = new List<int>();
            var optional = new List<int>();
            bool canAddOptionalRegion = rects.Count < maxRegionCount;

            for (int i = 0; i < rects.Count; i++)
            {
                RectInt rect = rects[i];
                int area = rect.width * rect.height;
                if (!CanSplit(rect, areaProfile.MinArea))
                {
                    continue;
                }

                if (!areaProfile.IsAllowed(area))
                {
                    forced.Add(i);
                }
                else if (canAddOptionalRegion && area < 8 && area >= areaProfile.MinArea * 2 && random.NextDouble() < 0.2)
                {
                    optional.Add(i);
                }
            }

            if (forced.Count > 0)
            {
                return forced[random.Next(forced.Count)];
            }

            if (optional.Count > 0)
            {
                return optional[random.Next(optional.Count)];
            }

            return -1;
        }

        private static bool CanSplit(RectInt rect, int minRegionArea)
        {
            if (rect.width > 1)
            {
                for (int cut = 1; cut < rect.width; cut++)
                {
                    if (cut * rect.height >= minRegionArea && (rect.width - cut) * rect.height >= minRegionArea)
                    {
                        return true;
                    }
                }
            }

            if (rect.height > 1)
            {
                for (int cut = 1; cut < rect.height; cut++)
                {
                    if (cut * rect.width >= minRegionArea && (rect.height - cut) * rect.width >= minRegionArea)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TrySplit(RectInt rect, RegionAreaProfile areaProfile, System.Random random, out RectInt first, out RectInt second)
        {
            var choices = new List<SplitChoice>();

            for (int cut = 1; cut < rect.width; cut++)
            {
                int leftArea = cut * rect.height;
                int rightArea = (rect.width - cut) * rect.height;
                if (leftArea >= areaProfile.MinArea && rightArea >= areaProfile.MinArea)
                {
                    AddWeightedSplitChoices(choices, new SplitChoice(true, cut), leftArea, rightArea, areaProfile);
                }
            }

            for (int cut = 1; cut < rect.height; cut++)
            {
                int topArea = cut * rect.width;
                int bottomArea = (rect.height - cut) * rect.width;
                if (topArea >= areaProfile.MinArea && bottomArea >= areaProfile.MinArea)
                {
                    AddWeightedSplitChoices(choices, new SplitChoice(false, cut), topArea, bottomArea, areaProfile);
                }
            }

            if (choices.Count == 0)
            {
                first = default;
                second = default;
                return false;
            }

            SplitChoice choice = choices[random.Next(choices.Count)];
            if (choice.vertical)
            {
                first = new RectInt(rect.xMin, rect.yMin, choice.offset, rect.height);
                second = new RectInt(rect.xMin + choice.offset, rect.yMin, rect.width - choice.offset, rect.height);
                return true;
            }

            first = new RectInt(rect.xMin, rect.yMin, rect.width, choice.offset);
            second = new RectInt(rect.xMin, rect.yMin + choice.offset, rect.width, rect.height - choice.offset);
            return true;
        }

        private static void AddWeightedSplitChoices(List<SplitChoice> choices, SplitChoice choice, int firstArea, int secondArea, RegionAreaProfile areaProfile)
        {
            int weight = 1;
            if (areaProfile.IsRequired(firstArea) || areaProfile.IsRequired(secondArea))
            {
                weight += 7;
            }
            else if (areaProfile.IsLargeAllowed(firstArea) || areaProfile.IsLargeAllowed(secondArea))
            {
                weight += 1;
            }

            if (areaProfile.IsAllowed(firstArea) && areaProfile.IsAllowed(secondArea))
            {
                weight += 2;
            }

            for (int i = 0; i < weight; i++)
            {
                choices.Add(choice);
            }
        }

        private static LevelData BuildLevel(int rows, int columns, List<RectInt> solutionRects, System.Random random)
        {
            var level = new LevelData
            {
                rows = rows,
                columns = columns,
                strictMode = true
            };

            for (int i = 0; i < solutionRects.Count; i++)
            {
                RectInt rect = solutionRects[i];
                level.clues.Add(new ClueData
                {
                    row = rect.yMin + random.Next(rect.height),
                    column = rect.xMin + random.Next(rect.width),
                    value = rect.width * rect.height,
                    solutionRow = rect.yMin,
                    solutionColumn = rect.xMin,
                    solutionHeight = rect.height,
                    solutionWidth = rect.width
                });
            }

            return level;
        }

        private static bool TryBuildTiledFallbackRects(int rows, int columns, RegionAreaProfile areaProfile, int maxRegionCount, out List<RectInt> rects)
        {
            rects = new List<RectInt>();
            int row = 0;
            while (row < rows)
            {
                int remainingRows = rows - row;
                int height = remainingRows >= 2 ? 2 : 1;
                if (!TryAppendFallbackRow(rects, row, columns, height, areaProfile))
                {
                    height = 1;
                    if (!TryAppendFallbackRow(rects, row, columns, height, areaProfile))
                    {
                        return false;
                    }
                }

                if (rects.Count > maxRegionCount)
                {
                    return false;
                }

                row += height;
            }

            return true;
        }

        private static bool TryAppendFallbackRow(List<RectInt> rects, int row, int columns, int height, RegionAreaProfile areaProfile)
        {
            var widths = new List<int>();
            if (!TryBuildFallbackWidths(columns, height, areaProfile, widths))
            {
                return false;
            }

            int column = 0;
            for (int i = 0; i < widths.Count; i++)
            {
                rects.Add(new RectInt(column, row, widths[i], height));
                column += widths[i];
            }

            return true;
        }

        private static bool TryBuildFallbackWidths(int remainingWidth, int height, RegionAreaProfile areaProfile, List<int> widths)
        {
            if (remainingWidth == 0)
            {
                return true;
            }

            for (int width = remainingWidth; width >= 1; width--)
            {
                if (!areaProfile.IsAllowed(width * height))
                {
                    continue;
                }

                widths.Add(width);
                if (TryBuildFallbackWidths(remainingWidth - width, height, areaProfile, widths))
                {
                    return true;
                }

                widths.RemoveAt(widths.Count - 1);
            }

            return false;
        }

        private readonly struct SplitChoice
        {
            public readonly bool vertical;
            public readonly int offset;

            public SplitChoice(bool vertical, int offset)
            {
                this.vertical = vertical;
                this.offset = offset;
            }
        }
    }
}
