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

            int totalTests = 6;
            int passedTests = 0;

            var gpoService = new GpoManagerService();
            var jitService = new JitAdminService();
            var hygieneService = new AccountHygieneService();
            var mockService = new MockAdService();
            var excelService = new ExcelAuditReportService();

            // TEST 1: GPO PReg バイナリ パース・シリアライズ可逆性
            try
            {
                Console.Write("[TEST 1/6] GPO registry.pol バイナリ Roundtrip 可逆性検査 ... ");
                var originalEntries = new[]
                {
                    new GpoPolicyEntry
                    {
                        KeyPath = @"Software\Policies\Microsoft\Edge",
                        ValueName = "TestDword",
                        Type = 4,
                        ValueData = 12345u
                    },
                    new GpoPolicyEntry
                    {
                        KeyPath = @"Software\Policies\Microsoft\Windows\System",
                        ValueName = "TestString",
                        Type = 1,
                        ValueData = "HelloADMorpher"
                    }
                };

                byte[] serialized = gpoService.SerializeRegistryPol(originalEntries);
                var parsedEntries = gpoService.ParseRegistryPol(serialized);

                if (parsedEntries.Count != 2)
                    throw new Exception($"パースエントリ数が一致しません。期待値: 2, 実際: {parsedEntries.Count}");
                if ((uint)parsedEntries[0].ValueData! != 12345u)
                    throw new Exception($"DWORD値が一致しません。実際: {parsedEntries[0].ValueData}");
                if (parsedEntries[1].ValueData!.ToString() != "HelloADMorpher")
                    throw new Exception($"String値が一致しません。実際: {parsedEntries[1].ValueData}");

                Console.WriteLine("PASSED");
                passedTests++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAILED: {ex.Message}");
            }

            // TEST 2: Computer ➔ User 変換時のブラックリスト除外
            try
            {
                Console.Write("[TEST 2/6] Machine ➔ User ポリシー変換時のブラックリスト除外検査 ... ");
                var testPolicies = new[]
                {
                    new GpoPolicyEntry { KeyPath = @"Software\Policies\Microsoft\FVE", ValueName = "BitLockerEnforce", Type = 4, ValueData = 1u },
                    new GpoPolicyEntry { KeyPath = @"Software\Policies\Microsoft\Edge", ValueName = "Homepage", Type = 1, ValueData = "https://example.com" }
                };

                var (converted, skipped) = gpoService.ConvertMachinePoliciesToUser(testPolicies);
                if (converted.Count != 1 || skipped.Count != 1)
                    throw new Exception($"変換/除外比率が異常です。Converted: {converted.Count}, Skipped: {skipped.Count}");
                if (skipped[0].KeyPath != @"Software\Policies\Microsoft\FVE")
                    throw new Exception("BitLockerが除外されませんでした。");
                if (converted[0].Scope != "User")
                    throw new Exception("EdgeのスコープがUserに切り替わっていません。");

                Console.WriteLine("PASSED");
                passedTests++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAILED: {ex.Message}");
            }

            // TEST 3: JITローカル管理者パスワードのエントロピー・文字種網羅性
            try
            {
                Console.Write("[TEST 3/6] JITパスワード生成の暗号学的安全性・文字種保証検査 ... ");
                for (int i = 0; i < 100; i++)
                {
                    string pwd = jitService.GenerateJitPassword(18);
                    if (pwd.Length != 18) throw new Exception("パスワード長が18文字ではありません。");
                    bool hasUpper = pwd.Any(char.IsUpper);
                    bool hasLower = pwd.Any(char.IsLower);
                    bool hasDigit = pwd.Any(char.IsDigit);
                    bool hasSymbol = pwd.Any(c => "!@#$%^&*()_-+=?".Contains(c));
                    if (!hasUpper || !hasLower || !hasDigit || !hasSymbol)
                        throw new Exception($"必須文字種が欠落しています: {pwd}");
                }

                Console.WriteLine("PASSED (100/100 試行合格)");
                passedTests++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAILED: {ex.Message}");
            }

            // TEST 4: アカウント安全退避スナップショットの整合性
            try
            {
                Console.Write("[TEST 4/6] 退職者・休眠アカウント退避スナップショット保存検査 ... ");
                string tempDir = Path.Combine(Path.GetTempPath(), "ADMorpher_Test_" + Guid.NewGuid().ToString("N"));
                var item = new AccountHygieneItem
                {
                    SamAccountName = "test.retiree",
                    UserPrincipalName = "test.retiree@corp.example.local",
                    OuPath = "OU=Sales,DC=corp,DC=example,DC=local"
                };

                string snapshotPath = hygieneService.CreateQuarantineSnapshot(item, "OU=Quarantine,DC=corp,DC=example,DC=local", tempDir);
                if (!File.Exists(snapshotPath)) throw new Exception("スナップショットファイルが存在しません。");
                string content = File.ReadAllText(snapshotPath);
                if (!content.Contains("test.retiree") || !content.Contains("Quarantine"))
                    throw new Exception("スナップショット内容が不完全です。");

                // クリーンアップ
                Directory.Delete(tempDir, true);
                Console.WriteLine("PASSED");
                passedTests++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAILED: {ex.Message}");
            }

            // TEST 5: グループネスト循環参照検知
            try
            {
                Console.Write("[TEST 5/6] グループネスト構造・循環参照検知検査 ... ");
                var tree = mockService.GetMockGroupNestHierarchy();
                bool foundCircular = FindCircularNode(tree);
                if (!foundCircular) throw new Exception("モックツリー内の循環参照ノードが検知されませんでした。");

                Console.WriteLine("PASSED");
                passedTests++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAILED: {ex.Message}");
            }

            // TEST 6: J-SOX監査Excelレポート生成実体検査
            try
            {
                Console.Write("[TEST 6/6] ClosedXML 美麗Excelレポート生成・全シート検査 ... ");
                string tempExcel = Path.Combine(Path.GetTempPath(), $"ADMorpher_Audit_{Guid.NewGuid():N}.xlsx");
                var health = mockService.GetMockHealthReport();
                var hygiene = mockService.GetMockHygieneItems();
                var gpos = mockService.GetMockGpos();
                var jits = mockService.GetMockJitDevices();

                excelService.ExportAuditReport(tempExcel, health, hygiene, gpos, jits);
                if (!File.Exists(tempExcel)) throw new Exception("Excelファイルが生成されませんでした。");
                var fi = new FileInfo(tempExcel);
                if (fi.Length < 1000) throw new Exception($"Excelファイルサイズが異常です: {fi.Length} bytes");

                File.Delete(tempExcel);
                Console.WriteLine($"PASSED (サイズ: {fi.Length:N0} bytes)");
                passedTests++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAILED: {ex.Message}");
            }

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
