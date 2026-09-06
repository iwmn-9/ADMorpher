using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using ADMorpher.Models;

namespace ADMorpher.Services
{
    public class ExcelAuditReportService
    {
        public void ExportAuditReport(
            string filePath,
            AdHealthReport healthReport,
            List<AccountHygieneItem> hygieneItems,
            List<GpoSummary> gpos,
            List<JitDevice> jitDevices)
        {
            using var wb = new XLWorkbook();

            // 1. エグゼクティブサマリー
            var wsSummary = wb.Worksheets.Add("監査エグゼクティブサマリー");
            wsSummary.Cell("B2").Value = "Active Directory セキュリティ & ガバナンス 監査報告書";
            wsSummary.Cell("B2").Style.Font.Bold = true;
            wsSummary.Cell("B2").Style.Font.FontSize = 16;
            wsSummary.Cell("B2").Style.Font.FontColor = XLColor.FromHtml("#1E293B");

            wsSummary.Cell("B4").Value = "ドメイン名";
            wsSummary.Cell("C4").Value = healthReport.DomainName;
            wsSummary.Cell("B5").Value = "監査出力日時";
            wsSummary.Cell("C5").Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            wsSummary.Cell("B6").Value = "健全性スコア";
            wsSummary.Cell("C6").Value = $"{healthReport.HealthScore} / 100";
            wsSummary.Cell("B7").Value = "ドメイン機能レベル";
            wsSummary.Cell("C7").Value = healthReport.DomainFunctionalLevel;

            wsSummary.Range("B4:B7").Style.Font.Bold = true;
            wsSummary.Range("B4:B7").Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");

            // 2. 休眠・リスクアカウント一覧
            var wsHygiene = wb.Worksheets.Add("休眠・要注意アカウント台帳");
            wsHygiene.Cell(1, 1).Value = "アカウント名 (SAM)";
            wsHygiene.Cell(1, 2).Value = "表示名";
            wsHygiene.Cell(1, 3).Value = "種別";
            wsHygiene.Cell(1, 4).Value = "有効状態";
            wsHygiene.Cell(1, 5).Value = "最終ログオン日";
            wsHygiene.Cell(1, 6).Value = "パスワード無期限";
            wsHygiene.Cell(1, 7).Value = "リスク・注意事項";

            var headerRange = wsHygiene.Range(1, 1, 1, 7);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E293B");

            int r = 2;
            foreach (var item in hygieneItems)
            {
                wsHygiene.Cell(r, 1).Value = item.SamAccountName;
                wsHygiene.Cell(r, 2).Value = item.DisplayName;
                wsHygiene.Cell(r, 3).Value = item.ObjectType;
                wsHygiene.Cell(r, 4).Value = item.IsEnabled ? "有効" : "無効";
                wsHygiene.Cell(r, 5).Value = item.LastLogonDate?.ToString("yyyy-MM-dd") ?? "未ログオン";
                wsHygiene.Cell(r, 6).Value = item.PasswordNeverExpires ? "無期限 (要是正)" : "定期変更";
                wsHygiene.Cell(r, 7).Value = item.RiskTagsString;
                r++;
            }
            wsHygiene.Columns().AdjustToContents();

            // 3. GPO一覧
            var wsGpo = wb.Worksheets.Add("GPO設定一覧");
            wsGpo.Cell(1, 1).Value = "GPO名";
            wsGpo.Cell(1, 2).Value = "ステータス";
            wsGpo.Cell(1, 3).Value = "リンク先OU";
            wsGpo.Cell(1, 4).Value = "有効設定数";

            var gpoHeader = wsGpo.Range(1, 1, 1, 4);
            gpoHeader.Style.Font.Bold = true;
            gpoHeader.Style.Font.FontColor = XLColor.White;
            gpoHeader.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E293B");

            r = 2;
            foreach (var gpo in gpos)
            {
                wsGpo.Cell(r, 1).Value = gpo.DisplayName;
                wsGpo.Cell(r, 2).Value = gpo.Status;
                wsGpo.Cell(r, 3).Value = gpo.LinkedOus;
                wsGpo.Cell(r, 4).Value = gpo.ActivePolicyCount;
                r++;
            }
            wsGpo.Columns().AdjustToContents();

            // 4. JIT端末ローカル管理者一覧
            var wsJit = wb.Worksheets.Add("JITローカルAdmin台帳");
            wsJit.Cell(1, 1).Value = "端末名";
            wsJit.Cell(1, 2).Value = "OS";
            wsJit.Cell(1, 3).Value = "OUパス";
            wsJit.Cell(1, 4).Value = "管理者アカウント";
            wsJit.Cell(1, 5).Value = "パスワード有効期限";
            wsJit.Cell(1, 6).Value = "JIT状態";

            var jitHeader = wsJit.Range(1, 1, 1, 6);
            jitHeader.Style.Font.Bold = true;
            jitHeader.Style.Font.FontColor = XLColor.White;
            jitHeader.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E293B");

            r = 2;
            foreach (var dev in jitDevices)
            {
                wsJit.Cell(r, 1).Value = dev.ComputerName;
                wsJit.Cell(r, 2).Value = dev.OperatingSystem;
                wsJit.Cell(r, 3).Value = dev.OuPath;
                wsJit.Cell(r, 4).Value = dev.AdminAccountName;
                wsJit.Cell(r, 5).Value = dev.PasswordExpiration.ToString("yyyy-MM-dd HH:mm");
                wsJit.Cell(r, 6).Value = dev.IsExpired ? "期限切れ (次回即時ローテーション)" : "有効 (保護中)";
                r++;
            }
            wsJit.Columns().AdjustToContents();

            wsSummary.Columns().AdjustToContents();
            wb.SaveAs(filePath);
        }
    }
}
