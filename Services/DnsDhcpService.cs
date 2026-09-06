using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ADMorpher.Models;

namespace ADMorpher.Services
{
    public class DnsDhcpService
    {
        /// <summary>
        /// DNSゾンビレコードの安全削除シミュレーション
        /// 実行前に必ずDNSゾーンのスナップショットJSONをバックアップ保存
        /// </summary>
        public (List<DnsRecordItem> removed, string backupPath) SimulateZombieDnsCleanup(
            IEnumerable<DnsRecordItem> currentRecords,
            string backupDirectory)
        {
            Directory.CreateDirectory(backupDirectory);

            var recordList = currentRecords.ToList();
            string backupFile = Path.Combine(backupDirectory, $"DnsZone_Snapshot_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            File.WriteAllText(backupFile, JsonSerializer.Serialize(recordList, new JsonSerializerOptions { WriteIndented = true }));

            // ゾンビDCレコード（SRV残骸）を特定して削除リストへ
            var toRemove = recordList.Where(r => r.IsZombieDc).ToList();
            return (toRemove, backupFile);
        }

        /// <summary>
        /// DHCP放置予約（180日以上未応答）の解放シミュレーション
        /// </summary>
        public (List<DhcpReservationItem> released, int reclaimedIpCount) SimulateDhcpReservationCleanup(
            IEnumerable<DhcpReservationItem> reservations)
        {
            var orphaned = reservations.Where(r => r.IsOrphaned).ToList();
            return (orphaned, orphaned.Count);
        }
    }
}
