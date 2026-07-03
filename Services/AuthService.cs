using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace SaoKeBot.Services
{
    public class AuthService
    {
        private HashSet<long> _allowedUsers = new HashSet<long>();
        private readonly string _filePath;

        public AuthService(string filePath = "Config/whitelist.json")
        {
            _filePath = Path.GetFullPath(filePath);
            LoadWhitelist();
            WatchFile();
        }

        /// <summary>
        /// Loads the list of allowed user IDs from a JSON file.
        /// </summary>
        /// <remarks>
        /// Logic: Read the file and deserialize into a HashSet.
        /// Why: Use HashSet instead of List because HashSet has O(1) complexity for lookups (Contains), ensuring instant access checks.
        /// </remarks>
        private void LoadWhitelist()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    Thread.Sleep(100);
                    var json = File.ReadAllText(_filePath);
                    var ids = JsonSerializer.Deserialize<List<long>>(json);
                    _allowedUsers = ids?.ToHashSet() ?? new HashSet<long>();
                    Console.WriteLine($"[INFO] Whitelist updated! Total: {_allowedUsers.Count} users.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to load whitelist: {ex.Message}");
            }
        }

        /// <summary>
        /// Monitors whitelist.json and auto-updates when modified by an Admin.
        /// </summary>
        /// <remarks>
        /// Logic: FileSystemWatcher triggers LoadWhitelist() as soon as the file is saved.
        /// Why: Allows Admins to grant permissions by manually editing the file without restarting the Bot.
        /// </remarks>
        private void WatchFile()
        {
            var directory = Path.GetDirectoryName(_filePath) ?? Directory.GetCurrentDirectory();
            var watcher = new FileSystemWatcher(directory)
            {
                Filter = Path.GetFileName(_filePath),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
            };

            watcher.Changed += (s, e) => LoadWhitelist();
            watcher.Created += (s, e) => LoadWhitelist();
            watcher.EnableRaisingEvents = true;
        }

        public bool IsAllowed(long userId) => _allowedUsers.Contains(userId);

        /// <summary>
        /// Adds a new user to the list and persists it to the physical file.
        /// </summary>
        /// <remarks>
        /// Logic: Add to memory first, then overwrite the entire list to the JSON file.
        /// Why: Synchronous file writing ensures data persistence if the Bot is unexpectedly shut down.
        /// </remarks>
        public void AddUser(long userId)
        {
            if (!_allowedUsers.Contains(userId))
            {
                _allowedUsers.Add(userId);
                try
                {
                    var json = JsonSerializer.Serialize(_allowedUsers.ToList(), new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_filePath, json);
                }
                catch (Exception ex) { Console.WriteLine($"[ERROR] Save failed: {ex.Message}"); }
            }
        }
    }
}
