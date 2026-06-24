using System.Collections.Generic;
using UnityEngine;

namespace Shud
{
    public static class RegionValidator
    {
        public static bool TryValidateLocal(LevelData level, RectInt rect, IReadOnlyList<PlayerRegion> existingRegions, out int clueId)
        {
            clueId = -1;

            if (rect.width <= 0 || rect.height <= 0 || rect.xMin < 0 || rect.yMin < 0 || rect.xMax > level.columns || rect.yMax > level.rows)
            {
                return false;
            }

            for (int i = 0; i < existingRegions.Count; i++)
            {
                if (Overlaps(rect, existingRegions[i].rect))
                {
                    return false;
                }
            }

            for (int i = 0; i < level.clues.Count; i++)
            {
                ClueData clue = level.clues[i];
                if (Contains(rect, clue.row, clue.column))
                {
                    if (clueId != -1)
                    {
                        return false;
                    }

                    clueId = i;
                }
            }

            return clueId != -1 && rect.width * rect.height == level.clues[clueId].value;
        }

        public static bool ValidateFinal(LevelData level, IReadOnlyList<PlayerRegion> regions)
        {
            if (regions.Count != level.clues.Count)
            {
                return false;
            }

            var occupied = new bool[level.rows * level.columns];
            var clueUsed = new bool[level.clues.Count];

            for (int i = 0; i < regions.Count; i++)
            {
                PlayerRegion region = regions[i];
                if (!TryValidateLocal(level, region.rect, EmptyRegions, out int clueId) || clueUsed[clueId])
                {
                    return false;
                }

                clueUsed[clueId] = true;

                for (int row = region.rect.yMin; row < region.rect.yMax; row++)
                {
                    for (int column = region.rect.xMin; column < region.rect.xMax; column++)
                    {
                        int cellIndex = row * level.columns + column;
                        if (occupied[cellIndex])
                        {
                            return false;
                        }

                        occupied[cellIndex] = true;
                    }
                }
            }

            for (int i = 0; i < occupied.Length; i++)
            {
                if (!occupied[i])
                {
                    return false;
                }
            }

            return true;
        }

        public static bool Contains(RectInt rect, int row, int column)
        {
            return column >= rect.xMin && column < rect.xMax && row >= rect.yMin && row < rect.yMax;
        }

        private static bool Overlaps(RectInt a, RectInt b)
        {
            return a.xMin < b.xMax && a.xMax > b.xMin && a.yMin < b.yMax && a.yMax > b.yMin;
        }

        private static readonly PlayerRegion[] EmptyRegions = new PlayerRegion[0];
    }
}
