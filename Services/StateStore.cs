using System;
using System.IO;
using System.Text.Json;
using ProxyShellReady.Models;

namespace ProxyShellReady.Services
{
    public static class StateStore
    {
        private static readonly string DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ProxyShellReady");

        private static readonly string StateFile = Path.Combine(DataDirectory, "state.json");

        public static AppState Load()
        {
            try
            {
                if (!File.Exists(StateFile))
                    return new AppState();

                string json = File.ReadAllText(StateFile);
                AppState state = JsonSerializer.Deserialize<AppState>(json);
                return state ?? new AppState();
            }
            catch
            {
                return new AppState();
            }
        }

        public static void Save(AppState state)
        {
            Directory.CreateDirectory(DataDirectory);
            string json = JsonSerializer.Serialize(state, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(StateFile, json);
        }

        public static string EnsureRuntimeDirectory()
        {
            Directory.CreateDirectory(DataDirectory);
            return DataDirectory;
        }
    }
}
