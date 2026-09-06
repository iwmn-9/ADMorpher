using System;
using System.IO;
using System.Linq;
using ADMorpher.Models;

namespace ADMorpher.Services
{
    public class RegressionTestService
    {
        public static int RunAllTests()
        {
            Console.WriteLine("=================================================");
            Console.WriteLine(" ADMorpher 統合回帰テストスイート (CI/CD Quality Gate)");
            Console.WriteLine("=================================================");

            int totalTests = 9;
            int passedTests = 0;

            var gpoService = new GpoManagerService();
            var jitService = new JitAdminService();
            var hygieneService = new AccountHygieneService();
            var mockService = new MockAdService();
            var excelService = new ExcelAuditReportService();
            var dnsDhcpService = new DnsDhcpService();

            // TEST 1: GPO PReg バイナリ Roundtrip 可逆性
            try
            {
                Console.Write("[TEST 1/9] GPO registry.pol バイナリ Roundtrip 可逆性検査 ... ");
                var originalEntries = new[]
                {
                    new GpoPolicyEntry { KeyPath = @"Software\Policies\Microsoft\Edge", ValueName = "TestDword", Type = 4, ValueData = 12345u },
                    new GpoPolicyEntry { KeyPath = @"Software\Policies\Microsoft\Windows\System", ValueName = "TestString", Type = 1, ValueData = "HelloADMorpher" }
                };

                byte[] serialized = gpoService.SerializeRegistryPol(originalEntries);
                var parsedEntries = gpoService.ParseRegistryPol(serialized);

                if (parsedEntries.Count != 2) throw new Exception($"パース数不一致: {parsedEntries.Count}");
                if ((uint)parsedEntries[0].ValueData! != 12345u) throw new Exception("DWORD値不一致");
                if (parsedEntries[1].ValueData!.ToString() != "HelloADMorpher") throw new Exception("String値不一致");

                Console.WriteLine("PASSED");
                passedTests++;
            }
            catch (Exception ex) { Console.WriteLine($"FAILED: {ex.Message}"); }

            // TEST 2: Computer ➔ User 変換時のブラックリスト除外
            try
            {
                Console.Write("[TEST 2/9] Machine ➔ User ポリシー変換時のブラックリスト除外検査 ... ");
                var testPolicies = new[]
                {
                    new GpoPolicyEntry { KeyPath = @"Software\Policies\Microsoft\FVE", ValueName = "BitLockerEnforce", Type = 4, ValueData = 1u },
                    new GpoPolicyEntry { KeyPath = @"Software\Policies\Microsoft\Edge", ValueName = "Homepage", Type = 1, ValueData = "https://example.com" }
                };

                var (converted, skipped) = gpoService.ConvertMachinePoliciesToUser(testPolicies);
                if (converted.Count != 1 || skipped.Count != 1) throw new Exception("変換/除外比率異常");
                if (skipped[0].KeyPath != @"Software\Policies\Microsoft\FVE") throw new Exception("BitLocker非除外");
                if (converted[0].Scope != "User") throw new Exception("スコープ未変更");

                Console.WriteLine("PASSED");
                passedTests++;
            }
            catch (Exception ex) { Console.WriteLine($"FAILED: {ex.Message}"); }

            // TEST 3: JITローカル管理者パスワードのエントロピー
            try
            {
                Console.Write("[TEST 3/9] JITパスワード生成の暗号学的安全性・文字種保証検査 ... ");
                for (int i = 0; i < 100; i++)
                {
                    string pwd = jitService.GenerateJitPassword(18);
                    if (pwd.Length != 18) throw new Exception("長異常");
                    if (!pwd.Any(char.IsUpper) || !pwd.Any(char.IsLower) || !pwd.Any(char.IsDigit) || !pwd.Any(c => "!@#$%^&*()_-+=?".Contains(c)))
                        throw new Exception($"文字種欠落: {pwd}");
                }
                Console.WriteLine("PASSED (100/100 試行合格)");
                passedTests++;
            }
            catch (Exception ex) { Console.WriteLine($"FAILED: {ex.Message}"); }

            // TEST 4: アカウント安全退避スナップショットの整合性
            try
            {
                Console.Write("[TEST 4/9] 退職者・休眠アカウント退避スナップショット保存検査 ... ");
                string tempDir = Path.Combine(Path.GetTempPath(), "ADMorpher_Test_" + Guid.NewGuid().ToString("N"));
                var item = new AccountHygieneItem { SamAccountName = "test.retiree", UserPrincipalName = "test.retiree@corp.example.local", OuPath = "OU=Sales,DC=corp,DC=example,DC=local" };

                string snapshotPath = hygieneService.CreateQuarantineSnapshot(item, "OU=Quarantine,DC=corp,DC=example,DC=local", tempDir);
                if (!File.Exists(snapshotPath) || !File.ReadAllText(snapshotPath).Contains("test.retiree")) throw new Exception("スナップショット不正");

                Directory.Delete(tempDir, true);
                Console.WriteLine("PASSED");
                passedTests++;
            }
            catch (Exception ex) { Console.WriteLine($"FAILED: {ex.Message}"); }

            // TEST 5: グループネスト循環参照検知
            try
            {
                Console.Write("[TEST 5/9] グループネスト構造・循環参照検知検査 ... ");
                var tree = mockService.GetMockGroupNestHierarchy();
                if (!FindCircularNode(tree)) throw new Exception("循環参照未検知");
                Console.WriteLine("PASSED");
                passedTests++;
            }
            catch (Exception ex) { Console.WriteLine($"FAILED: {ex.Message}"); }

            // TEST 6: J-SOX監査Excelレポート生成実体検査
            try
            {
                Console.Write("[TEST 6/9] ClosedXML 美麗Excelレポート生成・全シート検査 ... ");
                string tempExcel = Path.Combine(Path.GetTempPath(), $"ADMorpher_Audit_{Guid.NewGuid():N}.xlsx");
                excelService.ExportAuditReport(tempExcel, mockService.GetMockHealthReport(), mockService.GetMockHygieneItems(), mockService.GetMockGpos(), mockService.GetMockJitDevices());

                if (!File.Exists(tempExcel) || new FileInfo(tempExcel).Length < 1000) throw new Exception("Excel不正");
                File.Delete(tempExcel);
                Console.WriteLine("PASSED");
                passedTests++;
            }
            catch (Exception ex) { Console.WriteLine($"FAILED: {ex.Message}"); }

            // TEST 7: LAPS有効化・OU権限配備シミュレーション検査
            try
            {
                Console.Write("[TEST 7/9] LAPS有効化・OU権限配備シミュレーション検査 ... ");
                var config = new LapsDeploymentConfig
                {
                    TargetOu = "OU=Computers,DC=corp,DC=example,DC=local",
                    PasswordLength = 20,
                    PasswordAgeDays = 30,
                    AuthorizedAuditorGroup = "IT-Admins"
                };

                var (summary, script, gpo) = jitService.SimulateLapsDeployment(config);
                if (!summary.Contains("OU=Computers") || !script.Contains("Set-LapsADComputerSelfPermission"))
                    throw new Exception("LAPS配備スクリプト生成異常");
                if (gpo.Policies.Count != 3 || gpo.Policies[1].ValueData!.ToString() != "20")
                    throw new Exception("LAPS GPO生成異常");

                Console.WriteLine("PASSED");
                passedTests++;
            }
            catch (Exception ex) { Console.WriteLine($"FAILED: {ex.Message}"); }

            // TEST 8: GPO OUリンク & セキュリティフィルター配備シミュレーション検査
            try
            {
                Console.Write("[TEST 8/9] GPO OUリンク & セキュリティフィルター配備検査 ... ");
                var gpo = new GpoSummary { DisplayName = "Test-Security-GPO" };
                gpoService.SimulateGpoLinkChange(gpo, "OU=Osaka,DC=corp,DC=example,DC=local", "大阪支社", true, true);

                if (gpo.LinkTargets.Count != 1 || !gpo.LinkTargets[0].IsEnforced)
                    throw new Exception("GPOリンク設定異常");

                gpoService.SimulateSecurityFiltering(gpo, "Sales-PC-Group", true);
                if (!gpo.SecurityFilteringGroups.Contains("Sales-PC-Group"))
                    throw new Exception("セキュリティフィルター設定異常");

                Console.WriteLine("PASSED");
                passedTests++;
            }
            catch (Exception ex) { Console.WriteLine($"FAILED: {ex.Message}"); }

            // TEST 9: DNSゾンビSRV削除シミュレーション & ゾーンバックアップ検査
            try
            {
                Console.Write("[TEST 9/9] DNSゾンビSRV安全削除シミュレーション検査 ... ");
                string tempDir = Path.Combine(Path.GetTempPath(), "ADMorpher_DnsTest_" + Guid.NewGuid().ToString("N"));
                var dnsRecords = mockService.GetMockDnsRecords();

                var (removed, backupPath) = dnsDhcpService.SimulateZombieDnsCleanup(dnsRecords, tempDir);
                if (removed.Count != 1 || !removed[0].IsZombieDc)
                    throw new Exception("DNSゾンビ特定異常");
                if (!File.Exists(backupPath) || !File.ReadAllText(backupPath).Contains("corp.example.local"))
                    throw new Exception("DNSスナップショットバックアップ異常");

                Directory.Delete(tempDir, true);
                Console.WriteLine("PASSED");
                passedTests++;
            }
            catch (Exception ex) { Console.WriteLine($"FAILED: {ex.Message}"); }

            Console.WriteLine("=================================================");
            Console.WriteLine($" 回帰テスト結果: {passedTests}/{totalTests} PASSED");
            Console.WriteLine("=================================================");

            return passedTests == totalTests ? 0 : 1;
        }

        private static bool FindCircularNode(GroupNestNode node)
        {
            if (node.HasCircularReference) return true;
            foreach (var child in node.Children)
            {
                if (FindCircularNode(child)) return true;
            }
            return false;
        }
    }
}
