using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using ADMorpher.Models;

namespace ADMorpher.Services
{
    public class JitAdminService
    {
        private const string CharsUpper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        private const string CharsLower = "abcdefghijkmnopqrstuvwxyz";
        private const string CharsDigits = "23456789";
        private const string CharsSymbols = "!@#$%^&*()_-+=?";

        public string GenerateJitPassword(int length = 18)
        {
            if (length < 12) length = 12;

            var chars = new StringBuilder();
            chars.Append(GetRandomChar(CharsUpper));
            chars.Append(GetRandomChar(CharsLower));
            chars.Append(GetRandomChar(CharsDigits));
            chars.Append(GetRandomChar(CharsSymbols));

            string allChars = CharsUpper + CharsLower + CharsDigits + CharsSymbols;
            for (int i = 4; i < length; i++)
            {
                chars.Append(GetRandomChar(allChars));
            }

            return ShuffleString(chars.ToString());
        }

        public void ForceImmediateRotation(JitDevice device)
        {
            device.PasswordExpiration = DateTime.Now;
            device.CurrentLapsPassword = GenerateJitPassword(18);
            device.IsMasked = true;
        }

        /// <summary>
        /// LAPS自動有効化・OU権限配備シミュレーション
        /// </summary>
        public (string deploymentSummary, string generatedScript, GpoSummary generatedGpo) SimulateLapsDeployment(LapsDeploymentConfig config)
        {
            var summary = new StringBuilder();
            summary.AppendLine("=== LAPS 自動有効化・権限配備シミュレーション結果 ===");
            summary.AppendLine($"・対象OU: {config.TargetOu}");
            summary.AppendLine($"・ローカル管理者アカウント名: {config.AdminAccountName}");
            summary.AppendLine($"・パスワード長: {config.PasswordLength} 文字 (複雑性: {(config.RequireComplexity ? "英数記号必須" : "標準")})");
            summary.AppendLine($"・自動ローテーション周期: {config.PasswordAgeDays} 日");
            summary.AppendLine($"・パスワード閲覧許可グループ: {config.AuthorizedAuditorGroup}");
            summary.AppendLine();
            summary.AppendLine("【適用されるAD権限 (ACL)】");
            summary.AppendLine($" 1. 'SELF' (各コンピュータ自身) へ ms-Mcs-AdmPwd / msLAPS-Password の書き込み許可 (WriteProperty)");
            summary.AppendLine($" 2. '{config.AuthorizedAuditorGroup}' へ パスワード属性の読み取り許可 (ExtendedRight: All-Extended-Rights / ReadProperty)");
            summary.AppendLine($" 3. 一般ドメインユーザーからの読み取り権限は明示的に遮断");

            // 実機適用用PowerShellスクリプトの自動生成
            var script = new StringBuilder();
            script.AppendLine("# === ADMorpher LAPS 配備・自動構成スクリプト ===");
            script.AppendLine($"# 対象OU: {config.TargetOu}");
            script.AppendLine("Import-Module ActiveDirectory");
            script.AppendLine($"# 1. コンピュータ自身へのLAPSパスワード書き込み権限付与");
            script.AppendLine($"Set-LapsADComputerSelfPermission -Identity \"{config.TargetOu}\"");
            script.AppendLine($"# 2. 管理グループへのLAPSパスワード読み取り権限付与");
            script.AppendLine($"Set-LapsADReadPasswordPermission -Identity \"{config.TargetOu}\" -AllowedPrincipals \"{config.AuthorizedAuditorGroup}\"");
            script.AppendLine("# 完了");

            // 生成されるLAPS GPOオブジェクト
            var lapsGpo = new GpoSummary
            {
                DisplayName = "Auto-Generated-Windows-LAPS-Enforcement",
                Status = "AllEnabled",
                LinkedOus = config.TargetOu,
                ActivePolicyCount = 3
            };
            lapsGpo.Policies.Add(new GpoPolicyEntry
            {
                Scope = "Machine",
                Category = "LAPS",
                KeyPath = @"Software\Policies\Microsoft\Windows\LAPS",
                ValueName = "PasswordComplexity",
                Type = 4,
                ValueData = 4u,
                FriendlyName = "LAPS パスワード複雑性 (大文字+小文字+数字+記号)",
                CanConvertToUser = false
            });
            lapsGpo.Policies.Add(new GpoPolicyEntry
            {
                Scope = "Machine",
                Category = "LAPS",
                KeyPath = @"Software\Policies\Microsoft\Windows\LAPS",
                ValueName = "PasswordLength",
                Type = 4,
                ValueData = (uint)config.PasswordLength,
                FriendlyName = $"LAPS パスワード長 ({config.PasswordLength}文字)",
                CanConvertToUser = false
            });
            lapsGpo.Policies.Add(new GpoPolicyEntry
            {
                Scope = "Machine",
                Category = "LAPS",
                KeyPath = @"Software\Policies\Microsoft\Windows\LAPS",
                ValueName = "BackupDirectory",
                Type = 4,
                ValueData = 2u, // 2 = Active Directory (0=Disabled, 1=Entra ID, 2=AD)
                FriendlyName = "LAPS バックアップ先 (Active Directory)",
                CanConvertToUser = false
            });

            return (summary.ToString(), script.ToString(), lapsGpo);
        }

        private static char GetRandomChar(string source)
        {
            int index = RandomNumberGenerator.GetInt32(source.Length);
            return source[index];
        }

        private static string ShuffleString(string input)
        {
            char[] array = input.ToCharArray();
            int n = array.Length;
            while (n > 1)
            {
                n--;
                int k = RandomNumberGenerator.GetInt32(n + 1);
                (array[k], array[n]) = (array[n], array[k]);
            }
            return new string(array);
        }
    }
}
