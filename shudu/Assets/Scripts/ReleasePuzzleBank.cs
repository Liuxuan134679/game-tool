using System;
using UnityEngine;

namespace Shud
{
    [Serializable] public sealed class BakedPuzzle { public int rows, columns; public ClueData[] clues; }
    [Serializable] public sealed class BakedPuzzles { public BakedPuzzle[] levels; }
    public static class ReleasePuzzleBank
    {
        private static BakedPuzzles data;
        public static bool TryGet(int index, out LevelData level)
        {
            level = null;
            if (data == null)
            {
                var asset = Resources.Load<TextAsset>("ReleasePuzzles");
                if (asset == null) return false;
                data = JsonUtility.FromJson<BakedPuzzles>(asset.text);
            }
            if (data?.levels == null || index < 0 || index >= data.levels.Length) return false;
            var entry = data.levels[index];
            level = new LevelData {rows=entry.rows,columns=entry.columns,strictMode=true};
            level.clues.AddRange(entry.clues);
            return true;
        }
    }
}
