using System.Collections.Generic;
using UnityEngine;

namespace Shud
{
    public static class PuzzleSolver
    {
        public static bool HasUniqueSolution(LevelData level)
        {
            return CountSolutions(level, 2) == 1;
        }

        public static int CountSolutions(LevelData level, int maxSolutions)
        {
            int areaSum = 0;
            for (int i = 0; i < level.clues.Count; i++)
            {
                areaSum += level.clues[i].value;
            }

            if (areaSum != level.rows * level.columns)
            {
                return 0;
            }

            List<Candidate>[] candidates = BuildCandidates(level);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i].Count == 0)
                {
                    return 0;
                }
            }

            var usedClues = new bool[level.clues.Count];
            var occupied = new bool[level.rows * level.columns];
            int solutions = 0;
            Search(level, candidates, usedClues, occupied, maxSolutions, ref solutions);
            return solutions;
        }

        private static List<Candidate>[] BuildCandidates(LevelData level)
        {
            var result = new List<Candidate>[level.clues.Count];

            for (int clueId = 0; clueId < level.clues.Count; clueId++)
            {
                ClueData clue = level.clues[clueId];
                var clueCandidates = new List<Candidate>();

                for (int height = 1; height <= clue.value; height++)
                {
                    if (clue.value % height != 0)
                    {
                        continue;
                    }

                    int width = clue.value / height;
                    for (int row = clue.row - height + 1; row <= clue.row; row++)
                    {
                        for (int column = clue.column - width + 1; column <= clue.column; column++)
                        {
                            var rect = new RectInt(column, row, width, height);
                            if (rect.xMin < 0 || rect.yMin < 0 || rect.xMax > level.columns || rect.yMax > level.rows || ContainsMultipleClues(level, rect))
                            {
                                continue;
                            }

                            clueCandidates.Add(new Candidate(rect, BuildCellIndexes(level, rect)));
                        }
                    }
                }

                result[clueId] = clueCandidates;
            }

            return result;
        }

        private static void Search(LevelData level, List<Candidate>[] candidates, bool[] usedClues, bool[] occupied, int maxSolutions, ref int solutions)
        {
            if (solutions >= maxSolutions)
            {
                return;
            }

            int clueId = ChooseNextClue(candidates, usedClues, occupied);
            if (clueId == -1)
            {
                if (AllCellsCovered(occupied))
                {
                    solutions++;
                }

                return;
            }

            usedClues[clueId] = true;
            List<Candidate> clueCandidates = candidates[clueId];
            for (int i = 0; i < clueCandidates.Count; i++)
            {
                Candidate candidate = clueCandidates[i];
                if (HasOverlap(candidate, occupied))
                {
                    continue;
                }

                SetCells(candidate, occupied, true);
                Search(level, candidates, usedClues, occupied, maxSolutions, ref solutions);
                SetCells(candidate, occupied, false);

                if (solutions >= maxSolutions)
                {
                    break;
                }
            }

            usedClues[clueId] = false;
        }

        private static int ChooseNextClue(List<Candidate>[] candidates, bool[] usedClues, bool[] occupied)
        {
            int bestClueId = -1;
            int bestCount = int.MaxValue;

            for (int clueId = 0; clueId < candidates.Length; clueId++)
            {
                if (usedClues[clueId])
                {
                    continue;
                }

                int availableCount = 0;
                List<Candidate> clueCandidates = candidates[clueId];
                for (int i = 0; i < clueCandidates.Count; i++)
                {
                    if (!HasOverlap(clueCandidates[i], occupied))
                    {
                        availableCount++;
                    }
                }

                if (availableCount < bestCount)
                {
                    bestClueId = clueId;
                    bestCount = availableCount;
                }
            }

            return bestClueId;
        }

        private static bool ContainsMultipleClues(LevelData level, RectInt rect)
        {
            int count = 0;
            for (int i = 0; i < level.clues.Count; i++)
            {
                ClueData clue = level.clues[i];
                if (RegionValidator.Contains(rect, clue.row, clue.column))
                {
                    count++;
                    if (count > 1)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int[] BuildCellIndexes(LevelData level, RectInt rect)
        {
            var indexes = new int[rect.width * rect.height];
            int index = 0;
            for (int row = rect.yMin; row < rect.yMax; row++)
            {
                for (int column = rect.xMin; column < rect.xMax; column++)
                {
                    indexes[index] = row * level.columns + column;
                    index++;
                }
            }

            return indexes;
        }

        private static bool HasOverlap(Candidate candidate, bool[] occupied)
        {
            for (int i = 0; i < candidate.cells.Length; i++)
            {
                if (occupied[candidate.cells[i]])
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetCells(Candidate candidate, bool[] occupied, bool value)
        {
            for (int i = 0; i < candidate.cells.Length; i++)
            {
                occupied[candidate.cells[i]] = value;
            }
        }

        private static bool AllCellsCovered(bool[] occupied)
        {
            for (int i = 0; i < occupied.Length; i++)
            {
                if (!occupied[i])
                {
                    return false;
                }
            }

            return true;
        }

        private readonly struct Candidate
        {
            public readonly RectInt rect;
            public readonly int[] cells;

            public Candidate(RectInt rect, int[] cells)
            {
                this.rect = rect;
                this.cells = cells;
            }
        }
    }
}
