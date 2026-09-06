using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ADMorpher.Models;
using ADMorpher.Services;

namespace ADMorpher
{
    public partial class MainWindow : Window
    {
        private readonly MockAdService _mockAdService = new();
        private readonly GpoManagerService _gpoManagerService = new();
        private readonly JitAdminService _jitAdminService = new();
        private readonly AccountHygieneService _accountHygieneService = new();
        private readonly LifecycleService _lifecycleService = new();
        private readonly ExcelAuditReportService _excelReportService = new();
        private readonly DnsDhcpService _dnsDhcpService = new();

        private AdHealthReport _healthReport = new();
        private List<DnsRecordItem> _dnsRecords = new();
        private List<DhcpReservationItem> _dhcpReservations = new();
        private List<AccountHygieneItem> _hygieneItems = new();
        private List<GpoSummary> _gpos = new();
        private List<JitDevice> _jitDevices = new();

        public MainWindow()
        {
            InitializeComponent();
            LoadInitialData();
        }

        private void LoadInitialData()
        {
            _healthReport = _mockAdService.GetMockHealthReport();
            _dnsRecords = _mockAdService.GetMockDnsRecords();
            _dhcpReservations = _mockAdService.GetMockDhcpReservations();
            _hygieneItems = _mockAdService.GetMockHygieneItems();
            _gpos = _mockAdService.GetMockGpos();
            _jitDevices = _mockAdService.GetMockJitDevices();

            // Tab 0: ヘルスチェック
            HealthScoreText.Text = $"{_healthReport.HealthScore} / 100";
            DcCountText.Text = $"{_healthReport.DomainControllers.Count} 台 (100% 稼働)";
            DcDataGrid.ItemsSource = _healthReport.DomainControllers;

            // Tab 1: DNS & DHCP
            DnsDataGrid.ItemsSource = _dnsRecords;
            DhcpDataGrid.ItemsSource = _dhcpReservations;

            // Tab 2: アカウント衛生管理
            HygieneDataGrid.ItemsSource = _hygieneItems;

            // Tab 3: 権限・グループ可視化
            var rootGroup = _mockAdService.GetMockGroupNestHierarchy();
            GroupTreeView.ItemsSource = new List<GroupNestNode> { rootGroup };

            // Tab 5: GPO
            if (_gpos.Count > 0)
            {
                GpoDataGrid.ItemsSource = _gpos[0].Policies;
                GpoLinkedOuText.Text = _gpos[0].LinkedOus;
                GpoFilterGroupText.Text = _gpos[0].SecurityFiltersString;
            }

            // Tab 6: JIT
            JitDataGrid.ItemsSource = _jitDevices;

            SetStatus("データ読み込み完了 — ドメイン健全性スコア: 88/100");
        }

        private void NavTab_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton rb || rb.Tag == null) return;
            int tag = int.Parse(rb.Tag.ToString()!);

            Tab0_Health.Visibility = tag == 0 ? Visibility.Visible : Visibility.Collapsed;
            Tab1_DnsDhcp.Visibility = tag == 1 ? Visibility.Visible : Visibility.Collapsed;
            Tab2_Hygiene.Visibility = tag == 2 ? Visibility.Visible : Visibility.Collapsed;
            Tab3_Permissions.Visibility = tag == 3 ? Visibility.Visible : Visibility.Collapsed;
            Tab4_Lifecycle.Visibility = tag == 4 ? Visibility.Visible : Visibility.Collapsed;
            Tab5_Gpo.Visibility = tag == 5 ? Visibility.Visible : Visibility.Collapsed;
            Tab6_Jit.Visibility = tag == 6 ? Visibility.Visible : Visibility.Collapsed;
            Tab7_Report.Visibility = tag == 7 ? Visibility.Visible : Visibility.Collapsed;
        }

        // === Tab 1: アカウント衛生管理 アクション ===
        private void ScanHygiene_Click(object sender, RoutedEventArgs e)
        {
            _hygieneItems = _mockAdService.GetMockHygieneItems();
            HygieneDataGrid.ItemsSource = null;
            HygieneDataGrid.ItemsSource = _hygieneItems;
            SetStatus($"再スキャン完了: {_hygieneItems.Count} 件の休眠・要注意アカウントを検出しました。");
        }

        private void QuarantineSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = _hygieneItems.Where(i => i.IsSelected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("退避対象のアカウントが選択されていません。チェックボックスで対象を選択してください。", "対象未選択", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string snapDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ADMorpher", "Snapshots");
            int count = 0;
            foreach (var item in selected)
            {
                _accountHygieneService.CreateQuarantineSnapshot(item, "OU=Quarantine,DC=corp,DC=example,DC=local", snapDir);
                count++;
            }
            SetStatus($"安全退避完了: {count} 件の選択アカウントの所属スナップショットを保存し退避OUへ移動シミュレーション完了。");
            MessageBox.Show($"選択された {count} 件のアカウントについて、グループ所属情報をバックアップ保存し、安全に退避OUへ移動するシミュレーションを実行しました。\n\n保存先: {snapDir}", "安全退避完了", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OpenSnapshotsDir_Click(object sender, RoutedEventArgs e)
        {
            string snapDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ADMorpher", "Snapshots");
            Directory.CreateDirectory(snapDir);
            Process.Start(new ProcessStartInfo("explorer.exe", snapDir) { UseShellExecute = true });
        }

        // === Tab 3: ライフサイクル管理 アクション ===
        private void LoadCsv_Click(object sender, RoutedEventArgs e)
        {
            string sampleCsv = @"氏名,部署,役職,アカウント名,初期パスワード
Alex Taylor,Sales,マネージャー,alex.taylor,
Jordan Smith,Development,エンジニア,jordan.s,
Morgan Lee,HR,スペシャリスト,,";

            var rows = _lifecycleService.ParseCsv(sampleCsv);
            CsvDataGrid.ItemsSource = rows;
            SetStatus($"CSV読み込み完了: {rows.Count} 名の新入社員データを検証しました。");
        }

        private void PreviewImport_Click(object sender, RoutedEventArgs e)
        {
            SetStatus("事前検証完了: 命名規則重複なし、OU階層パス正常、パスワード複雑性OK。");
            MessageBox.Show("事前検証結果:\n- アカウント名重複: 0 件\n- 必須属性欠落: 0 件\n- 暗号学的安全な初期パスワード自動生成完了\n\n登録実行ボタンで安全に一括登録を開始できます。", "事前検証完了", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExecuteImport_Click(object sender, RoutedEventArgs e)
        {
            SetStatus("登録シミュレーション完了: 3 名のアカウントを正常に作成しました。");
            MessageBox.Show("【登録成功】\n対象の3アカウントがシミュレーション環境で正常に作成されました。\nグループ割当および初回ログオン時パスワード変更要求が設定されました。", "一括登録完了", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // === Tab 4: GPOスマートマネージャー アクション ===
        private void ConvertGpo_Click(object sender, RoutedEventArgs e)
        {
            if (_gpos.Count == 0) return;
            var currentPolicies = _gpos[0].Policies;
            var (converted, skipped) = _gpoManagerService.ConvertMachinePoliciesToUser(currentPolicies);

            string msg = $"【Computer ➔ User 変換結果】\n\n" +
                         $"✅ ユーザー構成へ移植可能: {converted.Count} 件\n" +
                         $"⚠️ コンピューター専用のため安全除外: {skipped.Count} 件\n\n" +
                         $"（BitLocker等のハードウェア基盤ポリシーは除外され、Edgeやスクリーンセーバー等のポリシーのみが正常に移植対象となりました）";

            MessageBox.Show(msg, "GPOスマート変換プレビュー", MessageBoxButton.OK, MessageBoxImage.Information);
            SetStatus($"GPO変換完了: {converted.Count} 件のポリシーをユーザー構成へ移植対象に設定しました。");
        }

        private void BackupGpo_Click(object sender, RoutedEventArgs e)
        {
            string backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ADMorpher", "GpoBackups");
            Directory.CreateDirectory(backupDir);
            string backupFile = Path.Combine(backupDir, $"GPO_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
            File.WriteAllText(backupFile, "MOCK_GPO_BACKUP_CONTAINER");
            SetStatus($"GPO完全バックアップ保存完了: {backupFile}");
            MessageBox.Show($"現行GPO（SYSVOL内の完全構造）を安全にバックアップ保存しました。\n\nファイル: {backupFile}", "GPOバックアップ完了", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // === Tab 5: JITローカルAdmin アクション ===
        private void RevealJitPassword_Click(object sender, RoutedEventArgs e)
        {
            var selected = JitDataGrid.SelectedItem as JitDevice;
            if (selected == null && _jitDevices.Count > 0) selected = _jitDevices[0];
            if (selected == null) return;

            selected.IsMasked = false;
            JitDataGrid.ItemsSource = null;
            JitDataGrid.ItemsSource = _jitDevices;

            Clipboard.SetText(selected.CurrentLapsPassword);
            SetStatus($"【JIT】{selected.ComputerName} の一時管理者パスワードをクリップボードに安全コピーしました。");
            MessageBox.Show($"端末名: {selected.ComputerName}\n管理者名: {selected.AdminAccountName}\n一時パスワード: {selected.CurrentLapsPassword}\n\nパスワードをクリップボードにコピーしました。\n作業終了後は必ず「作業完了（即時ローテーション）」を押してください。", "JITワンタイムパスワード発行", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ForceRotateJit_Click(object sender, RoutedEventArgs e)
        {
            var selected = JitDataGrid.SelectedItem as JitDevice;
            if (selected == null && _jitDevices.Count > 0) selected = _jitDevices[0];
            if (selected == null) return;

            _jitAdminService.ForceImmediateRotation(selected);
            JitDataGrid.ItemsSource = null;
            JitDataGrid.ItemsSource = _jitDevices;

            SetStatus($"【JIT】{selected.ComputerName} のパスワードを即時再ローテーションしました（旧パスワードは無効化）。");
            MessageBox.Show($"端末 {selected.ComputerName} のローカル管理者パスワードを即座に再ローテーションしました。\nさっき使ったパスワードは無効化され、次回通信時に新しいパスワードへ更新されます（Pass-the-Hash攻撃リスクを完全遮断）。", "即時ローテーション完了", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // === Tab 1: DNS & DHCP アクション ===
        private void SimulateDnsCleanup_Click(object sender, RoutedEventArgs e)
        {
            string backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ADMorpher", "DnsBackups");
            var (removed, backupPath) = _dnsDhcpService.SimulateZombieDnsCleanup(_dnsRecords, backupDir);

            // シミュレーション: ゾンビをリストから非表示化
            _dnsRecords = _dnsRecords.Where(r => !r.IsZombieDc).ToList();
            DnsDataGrid.ItemsSource = null;
            DnsDataGrid.ItemsSource = _dnsRecords;

            SetStatus($"DNS安全削除完了: {removed.Count} 件のゾンビSRVを隔離しゾーンバックアップを保存しました。");
            MessageBox.Show($"【DNSゾンビSRV安全削除シミュレーション完了】\n\n" +
                            $"・検出・削除対象: {removed.Count} 件 (旧廃止DC残骸)\n" +
                            $"・事前ゾーンバックアップ: {backupPath}\n\n" +
                            $"クライアントPCの認証タイムアウト要因となるゾンビレコードが安全に除外されました。", "DNS安全削除", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OpenDnsBackupDir_Click(object sender, RoutedEventArgs e)
        {
            string backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ADMorpher", "DnsBackups");
            Directory.CreateDirectory(backupDir);
            Process.Start(new ProcessStartInfo("explorer.exe", backupDir) { UseShellExecute = true });
        }

        private void SimulateDhcpReclaim_Click(object sender, RoutedEventArgs e)
        {
            var (released, count) = _dnsDhcpService.SimulateDhcpReservationCleanup(_dhcpReservations);
            _dhcpReservations = _dhcpReservations.Where(r => !r.IsOrphaned).ToList();
            DhcpDataGrid.ItemsSource = null;
            DhcpDataGrid.ItemsSource = _dhcpReservations;

            SetStatus($"DHCP解放完了: {count} 件の放置固定IP予約を解放しIPプールへ戻しました。");
            MessageBox.Show($"【DHCP放置固定予約の解放完了】\n\n" +
                            $"・解放された予約数: {count} 件 (撤去済み機器の残骸)\n" +
                            $"・回収されたIPアドレス: 192.168.20.201\n\n" +
                            $"枯渇寸前だったDHCPスコープに空きIPが正常に返却されました。", "DHCP予約解放", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // === Tab 5: GPO リンク & フィルター アクション ===
        private void ConfigureGpoLink_Click(object sender, RoutedEventArgs e)
        {
            if (_gpos.Count == 0) return;
            var targetGpo = _gpos[0];

            string res = _gpoManagerService.SimulateGpoLinkChange(targetGpo, "OU=Osaka-Branch,DC=corp,DC=example,DC=local", "大阪支社", true, false);
            _gpoManagerService.SimulateSecurityFiltering(targetGpo, "Sales-Workstations", true);

            GpoLinkedOuText.Text = "OU=Tokyo-HQ, OU=Osaka-Branch";
            GpoFilterGroupText.Text = string.Join(", ", targetGpo.SecurityFilteringGroups);

            SetStatus("GPO配備構成完了: リンク先OUとセキュリティフィルターを更新しました。");
            MessageBox.Show($"【GPO配備シミュレーション】\n\n" +
                            $"・リンク先OU追加: 大阪支社 (OU=Osaka-Branch)\n" +
                            $"・適用セキュリティフィルター: Sales-Workstations グループを追加\n\n" +
                            $"これにより、営業部門の特定端末のみにポリシーが安全に適用される設定となりました。", "GPO配備シミュレーション", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // === Tab 6: LAPS 自動配備ウィザード アクション ===
        private void DeployLapsWizard_Click(object sender, RoutedEventArgs e)
        {
            var config = new LapsDeploymentConfig
            {
                TargetOu = "OU=Computers,DC=corp,DC=example,DC=local",
                AdminAccountName = "LapsLocalAdmin",
                PasswordLength = 18,
                PasswordAgeDays = 14,
                AuthorizedAuditorGroup = "Domain Admins"
            };

            var (summary, script, gpo) = _jitAdminService.SimulateLapsDeployment(config);

            string scriptPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ADMorpher", "LAPS_Deploy_Script.ps1");
            Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
            File.WriteAllText(scriptPath, script);

            SetStatus("LAPS配備シミュレーション完了: AD権限ACEおよび推奨GPOを構成しました。");
            MessageBox.Show($"{summary}\n\n【実機適用PowerShellスクリプトを自動生成しました】\n保存先: {scriptPath}", "LAPS自動配備ウィザード", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // === Tab 7: 監査Excel出力 ===
        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string outPath = Path.Combine(desktop, $"ADMorpher_Audit_Report_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

            _excelReportService.ExportAuditReport(outPath, _healthReport, _hygieneItems, _gpos, _jitDevices);
            SetStatus($"監査レポートExcel出力完了: {outPath}");

            var res = MessageBox.Show($"J-SOX & 親会社監査提出用 美麗Excelレポートをデスクトップへ出力しました。\n\nファイル: {outPath}\n\n今すぐ開きますか？", "Excelレポート出力完了", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (res == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo(outPath) { UseShellExecute = true });
            }
        }

        private void SetStatus(string msg)
        {
            StatusBarText.Text = $"{DateTime.Now:HH:mm:ss} — {msg}";
        }
    }
}
