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
        public double WindowLeft   { get; set; } = double.NaN;  // NaN → centered on first run
        public double WindowTop    { get; set; } = double.NaN;
        public bool   WindowMaximized { get; set; }

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

        /// <summary>Save preferences. Swallows errors (writing prefs must never break the app).</summary>
        public static void Save(UserPreferences prefs, string? path = null)
        {
            path ??= DefaultPath;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(prefs, Json));
            }
            catch
            {
                // Quietly give up — preference loss is not worth surfacing.
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
