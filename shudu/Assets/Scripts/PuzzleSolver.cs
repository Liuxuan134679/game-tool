using System.Collections.Generic;
using UnityEngine;

namespace Shud
{
    public static class PuzzleSolver
    {
        public static bool HasUniqueSolution(LevelData level) => CountSolutions(level, 2) == 1;

        public static int CountSolutions(LevelData level, int maxSolutions)
        {
            int sum = 0;
            foreach (var clue in level.clues) sum += clue.value;
            if (sum != level.rows * level.columns || level.clues.Count % 2 != 0 || maxSolutions < 1) return 0;
            var candidates = new List<RectInt>();
            var empty = new PlayerRegion[0];
            for (int y = 0; y < level.rows; y++)
                for (int x = 0; x < level.columns; x++)
                    for (int h = 1; h <= level.rows - y; h++)
                        for (int w = 1; w <= level.columns - x; w++)
                        {
                            var rect = new RectInt(x, y, w, h);
                            if (RegionValidator.TryValidateLocal(level, rect, empty, out _)) candidates.Add(rect);
                        }
            int solutions = 0;
            Search(level, candidates, new bool[level.rows * level.columns], maxSolutions, ref solutions);
            return solutions;
        }

        private static void Search(LevelData level, List<RectInt> candidates, bool[] occupied, int limit, ref int solutions)
        {
            int first = System.Array.FindIndex(occupied, cell => !cell);
            if (first < 0) { solutions++; return; }
            foreach (var rect in candidates)
            {
                if (!RegionValidator.Contains(rect, first / level.columns, first % level.columns)) continue;
                bool overlaps = false;
                for (int y = rect.yMin; y < rect.yMax; y++)
                    for (int x = rect.xMin; x < rect.xMax; x++)
                        if (occupied[y * level.columns + x]) overlaps = true;
                if (overlaps) continue;
                SetCells(level, rect, occupied, true);
                Search(level, candidates, occupied, limit, ref solutions);
                SetCells(level, rect, occupied, false);
                if (solutions >= limit) return;
            }
        }

        private static void SetCells(LevelData level, RectInt rect, bool[] occupied, bool value)
        {
            for (int y = rect.yMin; y < rect.yMax; y++)
                for (int x = rect.xMin; x < rect.xMax; x++) occupied[y * level.columns + x] = value;
        }
    }
}
