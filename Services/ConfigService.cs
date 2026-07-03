using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SaoKeBot.Services
{
    public class ConfigService
    {
        private readonly string _adminFilePath;

        private List<long> _adminIds = new List<long>();

        private DateTime _lastAdminReadTime = DateTime.MinValue;

        public ConfigService()
        {
            _adminFilePath = Path.GetFullPath("Config/admins.json");
            EnsureConfigsLoaded();
        }

        // Lazy-load config via timestamp validation.
        public void EnsureConfigsLoaded()
        {
            try
            {
                if (System.IO.File.Exists(_adminFilePath))
                {
                    var adminWrite = System.IO.File.GetLastWriteTime(_adminFilePath);
                    if (adminWrite > _lastAdminReadTime)
                    {
                        string adminJson = System.IO.File.ReadAllText(_adminFilePath);
                        _adminIds = JsonSerializer.Deserialize<List<long>>(adminJson) ?? new List<long>();
                        _lastAdminReadTime = adminWrite;
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"[ERROR] ConfigService: {ex.Message}"); }
        }

        // Admin list getter.
        public List<long> GetAdmins() => _adminIds;

        public bool IsAdmin(long userId) => _adminIds.Contains(userId);
    }
}
