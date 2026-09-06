using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ADMorpher.Models;

namespace ADMorpher.Services
{
    public class LifecycleService
    {
        private readonly JitAdminService _jitAdminService = new();

        /// <summary>
        /// 社員台帳CSVを解析してプレビュー行を生成
        /// フォーマット: 氏名, 部署, 役職, アカウント名(空なら自動生成), 初期パスワード(空なら自動生成)
        /// </summary>
        public List<LifecycleImportRow> ParseCsv(string csvContent, string domainSuffix = "corp.example.local")
        {
            var rows = new List<LifecycleImportRow>();
            using var reader = new StringReader(csvContent);
            string? line;
            int rowIndex = 0;

            while ((line = reader.ReadLine()) != null)
            {
                rowIndex++;
                if (string.IsNullOrWhiteSpace(line)) continue;

                // ヘッダー行判定
                if (rowIndex == 1 && (line.Contains("氏名") || line.Contains("名前") || line.Contains("Name")))
                    continue;

                string[] parts = line.Split(',');
                string fullName = parts.Length > 0 ? parts[0].Trim() : "";
                string dept = parts.Length > 1 ? parts[1].Trim() : "";
                string title = parts.Length > 2 ? parts[2].Trim() : "";
                string sam = parts.Length > 3 ? parts[3].Trim() : "";
                string pwd = parts.Length > 4 ? parts[4].Trim() : "";

                if (string.IsNullOrWhiteSpace(fullName)) continue;

                var row = new LifecycleImportRow
                {
                    RowIndex = rowIndex,
                    FullName = fullName,
                    Department = dept,
                    Title = title
                };

                // アカウント名の自動補完（ローマ字推定または空時の連番）
                if (string.IsNullOrWhiteSpace(sam))
                {
                    sam = GenerateSamFromName(fullName, rowIndex);
                }

                row.SamAccountName = sam.ToLowerInvariant();
                row.UserPrincipalName = $"{row.SamAccountName}@{domainSuffix}";
                row.TargetOu = $"OU={dept},OU=Users,DC=corp,DC=example,DC=local";
                row.AssignedGroups = $"Domain Users, {dept}-Group";

                if (string.IsNullOrWhiteSpace(pwd))
                {
                    row.InitialPassword = _jitAdminService.GenerateJitPassword(16);
                }
                else
                {
                    row.InitialPassword = pwd;
                }

                // バリデーション
                if (row.SamAccountName.Length > 20)
                {
                    row.IsValid = false;
                    row.ErrorMessage = "SamAccountName は20文字以内である必要があります。";
                }

                rows.Add(row);
            }

            return rows;
        }

        private static string GenerateSamFromName(string name, int index)
        {
            // 簡易ローマ字・インデックスベース
            return $"user.{index:D3}";
        }
    }
}
