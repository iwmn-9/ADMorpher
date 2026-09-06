using System.Collections.Generic;
using ADMorpher.Models;

namespace ADMorpher.Services
{
    /// <summary>
    /// ADデータプロバイダー抽象インターフェース (Mock / Live AD の完全分離)
    /// </summary>
    public interface IAdDataProvider
    {
        AdHealthReport GetHealthReport();
        List<DnsRecordItem> GetDnsRecords();
        List<DhcpReservationItem> GetDhcpReservations();
        List<AccountHygieneItem> GetHygieneItems();
        GroupNestNode GetGroupNestHierarchy();
        List<GpoSummary> GetGpos();
        List<JitDevice> GetJitDevices();
        bool IsLiveEnvironment { get; }
    }
}
