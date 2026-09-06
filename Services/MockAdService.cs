using System;
using System.Collections.Generic;
using ADMorpher.Models;

namespace ADMorpher.Services
{
    public class MockAdService
    {
        public AdHealthReport GetMockHealthReport()
        {
            var report = new AdHealthReport
            {
                DomainName = "corp.example.local",
                ForestFunctionalLevel = "Windows Server 2016",
                DomainFunctionalLevel = "Windows Server 2016",
                HealthScore = 88
            };

            report.DomainControllers.Add(new DcInfo
            {
                HostName = "DC01.corp.example.local",
                IpAddress = "192.168.10.11",
                SiteName = "Tokyo-HQ",
                IsOnline = true,
                ReplicationStatus = "健全 (同期済)",
                LastSyncTime = DateTime.Now.AddMinutes(-3),
                FsmoRoles = new List<string> { "PDC Emulator", "RID Master", "Infrastructure Master" }
            });

            report.DomainControllers.Add(new DcInfo
            {
                HostName = "DC02.corp.example.local",
                IpAddress = "192.168.10.12",
                SiteName = "Tokyo-HQ",
                IsOnline = true,
                ReplicationStatus = "健全 (同期済)",
                LastSyncTime = DateTime.Now.AddMinutes(-5),
                FsmoRoles = new List<string> { "Schema Master", "Domain Naming Master" }
            });

            // ゾンビDNSレコードの検出例（撤去済み旧DCのSRV残骸）
            report.DnsIssues.Add(new DnsCheckResult
            {
                RecordType = "SRV",
                Name = "_ldap._tcp.dc._msdcs.corp.example.local",
                Target = "DC-OLD-2012.corp.example.local",
                IssueDescription = "廃止済みの旧DCレコードがDNS内に残留しています（クライアントのログオン遅延要因）。",
                IsZombieDc = true
            });

            report.DhcpScopes.Add(new DhcpScopeInfo
            {
                ScopeId = "192.168.20.0",
                Name = "本社 クライアントPC Wi-Fi / 有線",
                SubnetMask = "255.255.255.0",
                TotalAddresses = 240,
                InUseAddresses = 222 // 92.5% 危険アラート
            });

            report.DhcpScopes.Add(new DhcpScopeInfo
            {
                ScopeId = "192.168.30.0",
                Name = "大阪支社 クライアントPC",
                SubnetMask = "255.255.255.0",
                TotalAddresses = 120,
                InUseAddresses = 72
            });

            return report;
        }

        public List<AccountHygieneItem> GetMockHygieneItems()
        {
            var list = new List<AccountHygieneItem>
            {
                new AccountHygieneItem
                {
                    SamAccountName = "yamada.t",
                    DisplayName = "山田 太郎",
                    UserPrincipalName = "yamada.t@corp.example.local",
                    ObjectType = "User",
                    IsEnabled = true,
                    LastLogonDate = DateTime.Now.AddDays(-210),
                    PasswordNeverExpires = true,
                    DoesNotRequirePreAuth = false,
                    OuPath = "OU=Sales,OU=Users,DC=corp,DC=example,DC=local",
                    RiskTags = new List<string> { "休眠(210日)", "パスワード無期限" }
                },
                new AccountHygieneItem
                {
                    SamAccountName = "svc_scanner",
                    DisplayName = "複合機スキャン用 共有アカウント",
                    UserPrincipalName = "svc_scanner@corp.example.local",
                    ObjectType = "User",
                    IsEnabled = true,
                    LastLogonDate = DateTime.Now.AddDays(-5),
                    PasswordNeverExpires = true,
                    DoesNotRequirePreAuth = true, // AS-REP Roasting 標的
                    OuPath = "OU=ServiceAccounts,DC=corp,DC=example,DC=local",
                    RiskTags = new List<string> { "Kerberos事前認証不要(危険)", "パスワード無期限" }
                },
                new AccountHygieneItem
                {
                    SamAccountName = "sato.retired",
                    DisplayName = "佐藤 次郎 (退職済)",
                    UserPrincipalName = "sato.retired@corp.example.local",
                    ObjectType = "User",
                    IsEnabled = false,
                    LastLogonDate = DateTime.Now.AddDays(-140),
                    PasswordNeverExpires = false,
                    DoesNotRequirePreAuth = false,
                    OuPath = "OU=Development,OU=Users,DC=corp,DC=example,DC=local",
                    RiskTags = new List<string> { "無効化放置(グループ所属残存)" }
                },
                new AccountHygieneItem
                {
                    SamAccountName = "PC-OLD-XP01$",
                    DisplayName = "PC-OLD-XP01$",
                    UserPrincipalName = "PC-OLD-XP01$@corp.example.local",
                    ObjectType = "Computer",
                    IsEnabled = true,
                    LastLogonDate = DateTime.Now.AddDays(-450),
                    PasswordNeverExpires = false,
                    DoesNotRequirePreAuth = false,
                    OuPath = "OU=Computers,DC=corp,DC=example,DC=local",
                    RiskTags = new List<string> { "休眠PC(450日未ログオン)" }
                }
            };
            return list;
        }

        public GroupNestNode GetMockGroupNestHierarchy()
        {
            var root = new GroupNestNode
            {
                GroupName = "Domain Admins (ドメイン特権管理者)",
                SamAccountName = "Domain Admins",
                Sid = "S-1-5-21-123456789-123456789-123456789-512",
                Description = "ドメイン全体の最上位特権グループ",
                IsPrivileged = true,
                DirectMembers = new List<string> { "Administrator", "admin_operator" }
            };

            var secGroup = new GroupNestNode
            {
                GroupName = "SecOps-Tier1-Admins (運用監視特権)",
                SamAccountName = "SecOps-Tier1-Admins",
                Sid = "S-1-5-21-123456789-123456789-123456789-1101",
                Description = "サーバー監視運用アカウント群",
                IsPrivileged = true,
                DirectMembers = new List<string> { "tanaka_secops" }
            };

            var devGroup = new GroupNestNode
            {
                GroupName = "Dev-Lead-Group",
                SamAccountName = "Dev-Lead-Group",
                Sid = "S-1-5-21-123456789-123456789-123456789-1102",
                Description = "開発リードグループ",
                IsPrivileged = false,
                DirectMembers = new List<string> { "suzuki.dev" }
            };

            // 循環参照テスト用ノード
            var circularNode = new GroupNestNode
            {
                GroupName = "Circular-Ref-Group-A",
                SamAccountName = "Circular-Ref-Group-A",
                Sid = "S-1-5-21-123456789-123456789-123456789-9999",
                Description = "相互参照テスト用グループ",
                HasCircularReference = true,
                DirectMembers = new List<string> { "Circular-Ref-Group-B" }
            };

            root.Children.Add(secGroup);
            secGroup.Children.Add(devGroup);
            devGroup.Children.Add(circularNode);

            return root;
        }

        public List<GpoSummary> GetMockGpos()
        {
            var gpos = new List<GpoSummary>();

            var gpo1 = new GpoSummary
            {
                DisplayName = "Corp-Security-Baseline-2026",
                Status = "AllEnabled",
                LinkedOus = "OU=Tokyo-HQ,DC=corp,DC=example,DC=local",
                ActivePolicyCount = 4
            };
            gpo1.Policies.Add(new GpoPolicyEntry
            {
                Scope = "Machine",
                Category = "セキュリティ/Edge",
                KeyPath = @"Software\Policies\Microsoft\Edge",
                ValueName = "PasswordManagerEnabled",
                Type = 4,
                ValueData = 0u,
                FriendlyName = "Edge パスワードマネージャー無効化",
                Explanation = "ブラウザへのパスワード保存を会社共通で禁止",
                CanConvertToUser = true
            });
            gpo1.Policies.Add(new GpoPolicyEntry
            {
                Scope = "Machine",
                Category = "セキュリティ/画面ロック",
                KeyPath = @"Software\Policies\Microsoft\Windows\Control Panel\Desktop",
                ValueName = "ScreenSaverIsSecure",
                Type = 1,
                ValueData = "1",
                FriendlyName = "スクリーンセーバー パスワード保護",
                Explanation = "離席時の自動ロックと復帰パスワード要求",
                CanConvertToUser = true
            });
            gpo1.Policies.Add(new GpoPolicyEntry
            {
                Scope = "Machine",
                Category = "暗号化/BitLocker",
                KeyPath = @"Software\Policies\Microsoft\FVE",
                ValueName = "UseFVEEnforce",
                Type = 4,
                ValueData = 1u,
                FriendlyName = "BitLocker ドライブ暗号化強制",
                Explanation = "全クライアントPCのCドライブ暗号化義務化",
                CanConvertToUser = false,
                ExclusionReason = "コンピューター専用ポリシー（ハードウェア/暗号/OS基盤）のためユーザー構成への移植は安全に除外されました。"
            });

            gpos.Add(gpo1);

            var gpo2 = new GpoSummary
            {
                DisplayName = "User-Desktop-Customization",
                Status = "AllEnabled",
                LinkedOus = "OU=Sales,OU=Users,DC=corp,DC=example,DC=local",
                ActivePolicyCount = 2
            };
            gpo2.Policies.Add(new GpoPolicyEntry
            {
                Scope = "User",
                Category = "デスクトップ",
                KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\System",
                ValueName = "Wallpaper",
                Type = 1,
                ValueData = @"\\corp.example.local\sysvol\corp.example.local\Policies\Wallpaper.jpg",
                FriendlyName = "社内標準壁紙設定",
                Explanation = "全社統一壁紙の配布",
                CanConvertToUser = true
            });
            gpos.Add(gpo2);

            return gpos;
        }

        public List<JitDevice> GetMockJitDevices()
        {
            var devices = new List<JitDevice>
            {
                new JitDevice
                {
                    ComputerName = "PC-SALES-042",
                    OperatingSystem = "Windows 11 Enterprise 23H2",
                    OuPath = "OU=Sales,OU=Computers,DC=corp,DC=example,DC=local",
                    AdminAccountName = "LapsLocalAdmin",
                    CurrentLapsPassword = "kX9#mQ2$vL8!pZ4@wR",
                    PasswordExpiration = DateTime.Now.AddDays(12),
                    IsMasked = true
                },
                new JitDevice
                {
                    ComputerName = "PC-ENG-007",
                    OperatingSystem = "Windows 11 Pro 23H2",
                    OuPath = "OU=Dev,OU=Computers,DC=corp,DC=example,DC=local",
                    AdminAccountName = "LapsLocalAdmin",
                    CurrentLapsPassword = "aB3*cD9!eF5@gH1#jK",
                    PasswordExpiration = DateTime.Now.AddHours(2), // 間もなく期限
                    IsMasked = true
                },
                new JitDevice
                {
                    ComputerName = "PC-EXEC-001",
                    OperatingSystem = "Windows 11 Enterprise 23H2",
                    OuPath = "OU=Exec,OU=Computers,DC=corp,DC=example,DC=local",
                    AdminAccountName = "LapsLocalAdmin",
                    CurrentLapsPassword = "zY8&xW4^vU2%tS0$rQ",
                    PasswordExpiration = DateTime.Now.AddDays(25),
                    IsMasked = true
                }
            };
            return devices;
        }
    }
}
