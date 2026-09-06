using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ADMorpher.Models
{
    // === Tab 1: ヘルスチェック ===
    public class AdHealthReport
    {
        public string DomainName { get; set; } = "corp.example.local";
        public string ForestFunctionalLevel { get; set; } = "Windows Server 2016";
        public string DomainFunctionalLevel { get; set; } = "Windows Server 2016";
        public int HealthScore { get; set; } = 88; // 0-100
        public List<DcInfo> DomainControllers { get; set; } = new();
        public List<DnsCheckResult> DnsIssues { get; set; } = new();
        public List<DhcpScopeInfo> DhcpScopes { get; set; } = new();
    }

    public class DcInfo
    {
        public string HostName { get; set; } = "";
        public string IpAddress { get; set; } = "";
        public string SiteName { get; set; } = "Default-First-Site-Name";
        public bool IsOnline { get; set; } = true;
        public string ReplicationStatus { get; set; } = "健全 (同期済)";
        public DateTime LastSyncTime { get; set; } = DateTime.Now;
        public List<string> FsmoRoles { get; set; } = new();
        public string FsmoRolesString => FsmoRoles.Count > 0 ? string.Join(", ", FsmoRoles) : "―";
    }

    public class DnsCheckResult
    {
        public string RecordType { get; set; } = "SRV";
        public string Name { get; set; } = "";
        public string Target { get; set; } = "";
        public string IssueDescription { get; set; } = "";
        public bool IsZombieDc { get; set; } = false;
    }

    public class DhcpScopeInfo
    {
        public string ScopeId { get; set; } = "";
        public string Name { get; set; } = "";
        public string SubnetMask { get; set; } = "255.255.255.0";
        public int TotalAddresses { get; set; } = 254;
        public int InUseAddresses { get; set; } = 200;
        public double UsagePercentage => TotalAddresses > 0 ? Math.Round((double)InUseAddresses / TotalAddresses * 100, 1) : 0;
        public string Status => UsagePercentage >= 90 ? "危険 (枯渇寸前)" : UsagePercentage >= 80 ? "注意" : "正常";
    }

    // === 新設: DNS & DHCP 詳細モデル ===
    public class DnsRecordItem
    {
        public string ZoneName { get; set; } = "corp.example.local";
        public string HostName { get; set; } = "";
        public string RecordType { get; set; } = "A"; // A, SRV, CNAME, PTR
        public string TargetValue { get; set; } = "";
        public DateTime? Timestamp { get; set; }
        public bool IsZombieDc { get; set; } = false;
        public bool IsScavengeCandidate { get; set; } = false; // 古い未更新レコード
        public string StatusNote => IsZombieDc ? "⚠️ 廃止DC残骸 (要削除)" : IsScavengeCandidate ? "⚠️ 長期未更新 (廃棄PC候補)" : "正常";
    }

    public class DhcpReservationItem
    {
        public string ScopeId { get; set; } = "192.168.20.0";
        public string IpAddress { get; set; } = "";
        public string MacAddress { get; set; } = "";
        public string ReservationName { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime? LastActiveDate { get; set; }
        public bool IsOrphaned { get; set; } = false; // 180日以上未応答
        public string StatusNote => IsOrphaned ? "⚠️ 放置予約 (解放推奨)" : "アクティブ";
    }

    // === Tab 2: アカウント衛生管理 (断捨離) ===
    public class AccountHygieneItem
    {
        public string SamAccountName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string UserPrincipalName { get; set; } = "";
        public string ObjectType { get; set; } = "User"; // User or Computer
        public bool IsEnabled { get; set; } = true;
        public DateTime? LastLogonDate { get; set; }
        public int InactiveDays => LastLogonDate.HasValue ? (int)(DateTime.Now - LastLogonDate.Value).TotalDays : 999;
        public bool PasswordNeverExpires { get; set; } = false;
        public bool DoesNotRequirePreAuth { get; set; } = false; // AS-REP Roasting vulnerable
        public string DistinguishedName { get; set; } = "";
        public string OuPath { get; set; } = "";
        public List<string> RiskTags { get; set; } = new();
        public string RiskTagsString => string.Join(", ", RiskTags);
        public List<string> CurrentGroups { get; set; } = new();
        public bool IsSelected { get; set; } = false;
    }

    // === Tab 3: 権限・グループ可視化 ===
    public class GroupNestNode
    {
        public string GroupName { get; set; } = "";
        public string SamAccountName { get; set; } = "";
        public string Sid { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsPrivileged { get; set; } = false; // Domain Admins etc
        public bool HasCircularReference { get; set; } = false;
        public List<GroupNestNode> Children { get; set; } = new();
        public List<string> DirectMembers { get; set; } = new();
    }

    public class EffectivePermissionUser
    {
        public string UserName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public List<string> DirectGroups { get; set; } = new();
        public List<string> InheritedGroups { get; set; } = new();
        public List<string> AllEffectiveGroups { get; set; } = new();
        public bool HasDomainAdminPrivilege { get; set; } = false;
    }

    // === Tab 4: ライフサイクル管理 ===
    public class LifecycleImportRow
    {
        public int RowIndex { get; set; }
        public string FullName { get; set; } = "";
        public string Department { get; set; } = "";
        public string Title { get; set; } = "";
        public string SamAccountName { get; set; } = "";
        public string UserPrincipalName { get; set; } = "";
        public string InitialPassword { get; set; } = "";
        public string TargetOu { get; set; } = "";
        public string AssignedGroups { get; set; } = "";
        public bool IsValid { get; set; } = true;
        public string ErrorMessage { get; set; } = "";
    }

    public class OffboardingBackupSnapshot
    {
        public string SnapshotId { get; set; } = Guid.NewGuid().ToString("N");
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string SamAccountName { get; set; } = "";
        public string UserPrincipalName { get; set; } = "";
        public string OriginalOu { get; set; } = "";
        public List<string> OriginalGroups { get; set; } = new();
        public string QuarantineOu { get; set; } = "";
    }

    // === Tab 5: GPOスマートマネージャー ===
    public class GpoSummary
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string DisplayName { get; set; } = "";
        public string Status { get; set; } = "AllEnabled";
        public int UserVersion { get; set; } = 1;
        public int MachineVersion { get; set; } = 1;
        public string LinkedOus { get; set; } = "";
        public int ActivePolicyCount { get; set; } = 0;
        public List<GpoPolicyEntry> Policies { get; set; } = new();
        public List<GpoLinkTarget> LinkTargets { get; set; } = new();
        public List<string> SecurityFilteringGroups { get; set; } = new();
        public string SecurityFiltersString => SecurityFilteringGroups.Count > 0 ? string.Join(", ", SecurityFilteringGroups) : "Authenticated Users (全ユーザー・全PC)";
    }

    public class GpoLinkTarget
    {
        public string OuDisplayName { get; set; } = "";
        public string OuDistinguishedName { get; set; } = "";
        public bool IsEnabled { get; set; } = true;
        public bool IsEnforced { get; set; } = false;
    }

    public class GpoPolicyEntry
    {
        public string Scope { get; set; } = "Machine";
        public string Category { get; set; } = "管理用テンプレート";
        public string KeyPath { get; set; } = "";
        public string ValueName { get; set; } = "";
        public uint Type { get; set; } = 4;
        public object? ValueData { get; set; }
        public string FriendlyName { get; set; } = "";
        public string Explanation { get; set; } = "";
        public bool CanConvertToUser { get; set; } = true;
        public string ExclusionReason { get; set; } = "";
    }

    // === Tab 6: JITローカル管理者 & LAPS有効化 ===
    public class JitDevice
    {
        public string ComputerName { get; set; } = "";
        public string OperatingSystem { get; set; } = "Windows 11 Pro";
        public string OuPath { get; set; } = "";
        public string AdminAccountName { get; set; } = "LocalAdmin";
        public string CurrentLapsPassword { get; set; } = "";
        public DateTime PasswordExpiration { get; set; } = DateTime.Now.AddDays(14);
        public bool IsExpired => DateTime.Now >= PasswordExpiration;
        public bool IsMasked { get; set; } = true;
        public string DisplayPassword => IsMasked ? "••••••••••••••••" : CurrentLapsPassword;
    }

    public class LapsDeploymentConfig
    {
        public string TargetOu { get; set; } = "OU=Computers,DC=corp,DC=example,DC=local";
        public string AdminAccountName { get; set; } = "LapsLocalAdmin";
        public int PasswordLength { get; set; } = 18;
        public int PasswordAgeDays { get; set; } = 14;
        public bool RequireComplexity { get; set; } = true;
        public string AuthorizedAuditorGroup { get; set; } = "IT-Helpdesk-Admins";
    }
}
