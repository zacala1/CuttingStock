using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CuttingStock.Core.Persistence
{
    /// <summary>
    /// JSON-backed user preferences. Persists window state, last-used tab,
    /// last-selected algorithm per tab, and the recent-scenarios MRU list.
    /// Stored at <c>%LOCALAPPDATA%/CuttingStock/preferences.json</c>.
    /// </summary>
    public sealed class UserPreferences
    {
        public const int RecentMax = 5;

        // Window
        public double WindowWidth  { get; set; } = 1400;
        public double WindowHeight { get; set; } = 820;
        /// <summary>Null on first run — caller centres the window. Was previously
        /// double.NaN, but System.Text.Json with default options throws on NaN, so
        /// any close-from-maximized that kept the NaN default lost the entire
        /// preferences file. Nullable double round-trips cleanly.</summary>
        public double? WindowLeft  { get; set; }
        public double? WindowTop   { get; set; }
        public bool    WindowMaximized { get; set; }

        /// <summary>0 = 1D Rebar tab, 1 = 2D Sheet tab.</summary>
        public int LastTopTabIndex { get; set; }

        // Algorithm selection per tab (matches ComboBox SelectedIndex).
        public int LastAlgorithm1D { get; set; }
        public int LastAlgorithm2D { get; set; }

        // Recent scenarios — most-recent first, max RecentMax entries.
        public List<string> Recent1D { get; set; } = new();
        public List<string> Recent2D { get; set; } = new();
    }

    /// <summary>I/O for <see cref="UserPreferences"/>.</summary>
    public static class UserPreferencesStore
    {
        private static readonly JsonSerializerOptions Json = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>Default location: %LOCALAPPDATA%/CuttingStock/preferences.json.</summary>
        public static string DefaultPath
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CuttingStock");
                return Path.Combine(dir, "preferences.json");
            }
        }

        /// <summary>Load preferences from <paramref name="path"/>; returns defaults on any failure.</summary>
        public static UserPreferences Load(string? path = null)
        {
            path ??= DefaultPath;
            try
            {
                if (!File.Exists(path)) return new UserPreferences();
                var text = File.ReadAllText(path);
                return JsonSerializer.Deserialize<UserPreferences>(text, Json) ?? new UserPreferences();
            }
            catch
            {
                // Corrupt / unreadable file — fall back to defaults rather than crash.
                return new UserPreferences();
            }
        }

        /// <summary>
        /// Save preferences atomically — write to a temp file in the same directory
        /// then rename over the target so a power loss / crash mid-write cannot leave
        /// a half-written prefs.json that would prevent the app from starting next
        /// time. Swallows errors (writing prefs must never break the app).
        /// </summary>
        public static void Save(UserPreferences prefs, string? path = null)
        {
            path ??= DefaultPath;
            string? tempPath = null;
            try
            {
                var dir = Path.GetDirectoryName(path)!;
                Directory.CreateDirectory(dir);
                tempPath = Path.Combine(dir, Path.GetFileName(path) + ".tmp");
                File.WriteAllText(tempPath, JsonSerializer.Serialize(prefs, Json));
                // File.Move with overwrite is atomic on NTFS for files on the same
                // volume — which is guaranteed because tempPath is in the same dir.
                File.Move(tempPath, path, overwrite: true);
            }
            catch
            {
                // Quietly give up. Best-effort cleanup of the temp file.
                if (tempPath != null)
                {
                    try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                }
            }
        }

        /// <summary>Push a freshly-opened scenario path onto the MRU list (dedup + cap).</summary>
        public static void PushRecent(List<string> recent, string path)
        {
            recent.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            recent.Insert(0, path);
            while (recent.Count > UserPreferences.RecentMax)
                recent.RemoveAt(recent.Count - 1);
        }
    }
}
