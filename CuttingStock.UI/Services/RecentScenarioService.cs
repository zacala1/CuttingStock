using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CuttingStock.Core.Persistence;

namespace CuttingStock.UI.Services
{
    public sealed record RecentScenarioEntry(
        string Path,
        string DisplayName,
        bool Exists);

    /// <summary>Plain-data operations used by the recent-scenario menu.</summary>
    public static class RecentScenarioService
    {
        public static IReadOnlyList<RecentScenarioEntry> BuildEntries(
            IEnumerable<string> paths,
            Func<string, bool>? exists = null,
            Func<string, string>? displayName = null)
        {
            ArgumentNullException.ThrowIfNull(paths);
            exists ??= File.Exists;
            displayName ??= Path.GetFileName;

            return paths
                .Select(path => new RecentScenarioEntry(
                    path,
                    displayName(path),
                    exists(path)))
                .ToList();
        }

        public static void Touch(List<string> recent, string path)
        {
            ArgumentNullException.ThrowIfNull(recent);
            UserPreferencesStore.PushRecent(recent, path);
        }

        public static bool Remove(List<string> recent, string path)
        {
            ArgumentNullException.ThrowIfNull(recent);
            return recent.RemoveAll(candidate =>
                string.Equals(candidate, path, StringComparison.OrdinalIgnoreCase)) > 0;
        }

        public static void Clear(List<string> recent)
        {
            ArgumentNullException.ThrowIfNull(recent);
            recent.Clear();
        }
    }
}
