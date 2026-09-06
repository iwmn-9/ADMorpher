using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ADMorpher.Models;

namespace ADMorpher.Services
{
    public class AccountHygieneService
    {
        /// <summary>
        /// アカウントの安全退避スナップショットをJSONに永続化（ロールバック・監査台帳用）
        /// </summary>
        public string CreateQuarantineSnapshot(AccountHygieneItem item, string quarantineOu, string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);

            var snapshot = new OffboardingBackupSnapshot
            {
                SamAccountName = item.SamAccountName,
                UserPrincipalName = item.UserPrincipalName,
                OriginalOu = item.OuPath,
                QuarantineOu = quarantineOu,
                OriginalGroups = new List<string> { "Domain Users", "Sales-Dept-Group", "FileServer-ReadWrite" }
            };

            string filename = $"Snapshot_{item.SamAccountName}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            string filePath = Path.Combine(outputDirectory, filename);
            string json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);

            return filePath;
        }
    }
}
