using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shud
{
    [Serializable]
    public sealed class ClueData
    {
        public int row;
        public int column;
        public int value;
        public int solutionRow;
        public int solutionColumn;
        public int solutionHeight;
        public int solutionWidth;

        public RectInt SolutionRect => new RectInt(solutionColumn, solutionRow, solutionWidth, solutionHeight);
    }

    public sealed class LevelData
    {
        public int rows;
        public int columns;
        public bool strictMode;
        public readonly List<ClueData> clues = new List<ClueData>();

        public int FindClueIdAt(int row, int column)
        {
            for (int i = 0; i < clues.Count; i++)
            {
                if (clues[i].row == row && clues[i].column == column)
                {
                    return i;
                }
            }

            return -1;
        }

        public static LevelData CreateSingleRegion(int rows, int columns, int seed)
        {
            var random = new System.Random(seed);
            var level = new LevelData
            {
                rows = rows,
                columns = columns,
                strictMode = true
            };

            level.clues.Add(new ClueData
            {
                row = random.Next(rows),
                column = random.Next(columns),
                value = rows * columns,
                solutionRow = 0,
                solutionColumn = 0,
                solutionHeight = rows,
                solutionWidth = columns
            });

            return level;
        }
    }

    public sealed class PlayerRegion
    {
        public int clueId;
        public int colorIndex;
        public RectInt rect;
    }
}
