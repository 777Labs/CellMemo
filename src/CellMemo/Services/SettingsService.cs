using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CellMemo.Models;

namespace CellMemo.Services
{
    public class SettingsService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        private readonly string _baseDir;
        private readonly string _settingsPath;
        private readonly string _sessionDir;
        private readonly string _manifestPath;
        private readonly string _logDir;
        private readonly string _logPath;

        public AppSettings CurrentSettings { get; private set; } = new();

        public SettingsService()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string primaryDir = Path.Combine(localAppData, "CellMemo");

            try
            {
                Directory.CreateDirectory(primaryDir);
                _baseDir = primaryDir;
            }
            catch
            {
                // ポータブルフォールバック
                _baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "portable_data");
                Directory.CreateDirectory(_baseDir);
            }

            _settingsPath = Path.Combine(_baseDir, "settings.json");
            _sessionDir = Path.Combine(_baseDir, "session");
            _manifestPath = Path.Combine(_sessionDir, "session_manifest.json");
            _logDir = Path.Combine(_baseDir, "logs");
            _logPath = Path.Combine(_logDir, "error.log");

            Directory.CreateDirectory(_sessionDir);
            Directory.CreateDirectory(_logDir);
        }

        public void LoadSettingsSync()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    string json = File.ReadAllText(_settingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (settings != null)
                    {
                        CurrentSettings = settings;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("Failed to load settings.json", ex);
            }
        }

        public async Task LoadSettingsAsync()
        {
            await Task.Run(() => LoadSettingsSync());
        }

        public void SaveSettingsSync()
        {
            try
            {
                string json = JsonSerializer.Serialize(CurrentSettings, JsonOptions);
                AtomicWriteTextSync(_settingsPath, json);
            }
            catch (Exception ex)
            {
                LogError("Failed to save settings.json", ex);
            }
        }

        public async Task SaveSettingsAsync()
        {
            await Task.Run(() => SaveSettingsSync());
        }

        public void SaveSessionSync(SessionManifest manifest, System.Collections.Generic.IEnumerable<TabSessionData> tabs)
        {
            try
            {
                Directory.CreateDirectory(_sessionDir);

                foreach (var tab in tabs)
                {
                    string tabFile = Path.Combine(_sessionDir, $"tab_{tab.Id}.json");
                    string tabJson = JsonSerializer.Serialize(tab, JsonOptions);
                    AtomicWriteTextSync(tabFile, tabJson);
                }

                string manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
                AtomicWriteTextSync(_manifestPath, manifestJson);
            }
            catch (Exception ex)
            {
                LogError("Failed to save session", ex);
            }
        }

        public async Task SaveSessionAsync(SessionManifest manifest, System.Collections.Generic.IEnumerable<TabSessionData> tabs)
        {
            await Task.Run(() => SaveSessionSync(manifest, tabs));
        }

        public (SessionManifest? manifest, System.Collections.Generic.List<TabSessionData> tabs) LoadSessionSync()
        {
            var tabList = new System.Collections.Generic.List<TabSessionData>();
            SessionManifest? manifest = null;

            try
            {
                if (File.Exists(_manifestPath))
                {
                    string manifestJson = File.ReadAllText(_manifestPath);
                    manifest = JsonSerializer.Deserialize<SessionManifest>(manifestJson, JsonOptions);
                    if (manifest != null)
                    {
                        foreach (var entry in manifest.Tabs)
                        {
                            string tabFile = Path.Combine(_sessionDir, $"tab_{entry.Id}.json");
                            if (File.Exists(tabFile))
                            {
                                string tabJson = File.ReadAllText(tabFile);
                                var tabData = JsonSerializer.Deserialize<TabSessionData>(tabJson, JsonOptions);
                                if (tabData != null)
                                {
                                    tabList.Add(tabData);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("Failed to load session", ex);
            }

            return (manifest, tabList);
        }

        public async Task<(SessionManifest? manifest, System.Collections.Generic.List<TabSessionData> tabs)> LoadSessionAsync()
        {
            return await Task.Run(() => LoadSessionSync());
        }

        public void ClearSession()
        {
            try
            {
                if (Directory.Exists(_sessionDir))
                {
                    var files = Directory.GetFiles(_sessionDir, "*.json");
                    foreach (var f in files)
                    {
                        File.Delete(f);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("Failed to clear session", ex);
            }
        }

        public void LogError(string message, Exception? ex)
        {
            try
            {
                Directory.CreateDirectory(_logDir);
                // 5MB 超過チェック
                if (File.Exists(_logPath))
                {
                    var info = new FileInfo(_logPath);
                    if (info.Length > 5 * 1024 * 1024)
                    {
                        string oldLog = Path.Combine(_logDir, "error.log.old");
                        File.Move(_logPath, oldLog, overwrite: true);
                    }
                }

                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n{ex}\n\n";
                File.AppendAllText(_logPath, entry);
            }
            catch
            {
                // ログ保存自体が失敗した場合は握りつぶす
            }
        }

        private static void AtomicWriteTextSync(string targetPath, string content)
        {
            string dir = Path.GetDirectoryName(targetPath) ?? ".";
            string tempPath = Path.Combine(dir, $"{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");

            using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.None))
            using (var writer = new StreamWriter(fs, System.Text.Encoding.UTF8))
            {
                writer.Write(content);
                writer.Flush();
                fs.Flush(true);
            }

            File.Move(tempPath, targetPath, overwrite: true);
        }
    }
}
