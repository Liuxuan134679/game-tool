using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shud
{
    [Serializable]
    public sealed class ReleaseSave
    {
        public int version = 1;
        public int levelIndex;
        public int completedLevels;
        public string puzzle;
        public float seconds = 60f;
        public float frozenSeconds;
        public bool freezeUsed;
        public bool tutorialSeen;
        public bool muted;
        public List<SavedRectangle> rectangles = new List<SavedRectangle>();
    }

    [Serializable]
    public sealed class SavedRectangle
    {
        public int x, y, width, height;
        public SavedRectangle() { }
        public SavedRectangle(RectInt rect) { x = rect.x; y = rect.y; width = rect.width; height = rect.height; }
        public RectInt Rect => new RectInt(x, y, width, height);
    }

    public static class ReleaseProgress
    {
        public const string Key = "AreaGrid.Pairs.TwelveLevels.Save.v1";
        private const string BackupKey = Key + ".backup";
        public static string Fingerprint(LevelData level)
        {
            var result = new System.Text.StringBuilder("pairs|").Append(level.rows).Append('x').Append(level.columns);
            foreach (var clue in level.clues)
                result.Append('|').Append(clue.row).Append(',').Append(clue.column).Append(',').Append(clue.value);
            return result.ToString();
        }

        public static bool TryParse(string json, out ReleaseSave save)
        {
            save = null;
            if (string.IsNullOrEmpty(json) || json.Length > 32768) return false;
            try
            {
                var candidate = JsonUtility.FromJson<ReleaseSave>(json);
                if (candidate == null || candidate.version != 1 || candidate.levelIndex < 0 ||
                    candidate.levelIndex > LevelBoardPlan.LastLevelIndex || candidate.completedLevels < 0 ||
                    candidate.completedLevels > LevelBoardPlan.LastLevelIndex + 1 ||
                    float.IsNaN(candidate.seconds) || float.IsInfinity(candidate.seconds) || candidate.seconds < 0 || candidate.seconds > LevelBoardPlan.MaximumTargetSeconds ||
                    float.IsNaN(candidate.frozenSeconds) || float.IsInfinity(candidate.frozenSeconds) || candidate.frozenSeconds < 0 || candidate.frozenSeconds > 10 ||
                    candidate.rectangles == null || candidate.rectangles.Count > 64 || string.IsNullOrEmpty(candidate.puzzle)) return false;
                save = candidate;
                return true;
            }
            catch (Exception) { return false; }
        }

        public static bool TryLoad(out ReleaseSave save)
        {
            save = null;
            try { return TryParse(PlayerPrefs.GetString(Key, ""), out save) || TryParse(PlayerPrefs.GetString(BackupKey, ""), out save); }
            catch (Exception) { return false; }
        }

        public static bool TryRestoreRegions(ReleaseSave save, LevelData level, out List<PlayerRegion> regions)
        {
            regions = new List<PlayerRegion>();
            if (save == null || save.puzzle != Fingerprint(level) || save.rectangles == null) return false;
            foreach (var saved in save.rectangles)
            {
                if (saved == null || saved.x < 0 || saved.y < 0 || saved.width <= 0 || saved.height <= 0 ||
                    saved.width > level.columns || saved.height > level.rows ||
                    saved.x > level.columns - saved.width || saved.y > level.rows - saved.height ||
                    !RegionValidator.TryValidateLocal(level, saved.Rect, regions, out int clue))
                { regions.Clear(); return false; }
                regions.Add(new PlayerRegion { clueId = clue, colorIndex = regions.Count, rect = saved.Rect });
            }
            return true;
        }

        public static bool Store(ReleaseSave save)
        {
            try
            {
                string previous = PlayerPrefs.GetString(Key, "");
                if (TryParse(previous, out _)) PlayerPrefs.SetString(BackupKey, previous);
                PlayerPrefs.SetString(Key, JsonUtility.ToJson(save));
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception) { return false; }
        }
    }
}
