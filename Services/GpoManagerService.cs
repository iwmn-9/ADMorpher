using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ADMorpher.Models;

namespace ADMorpher.Services
{
    public class GpoManagerService
    {
        private static readonly byte[] HeaderSignature = new byte[] { 0x50, 0x52, 0x65, 0x67 }; // "PReg"
        private const uint HeaderVersion = 0x00000001;

        // コンピューター構成専用（ユーザー構成へ移植してはならないブラックリストキー）
        private static readonly string[] MachineOnlyPrefixes = new[]
        {
            @"Software\Policies\Microsoft\FVE", // BitLocker
            @"Software\Policies\Microsoft\Windows Defender", // Defender core
            @"System\CurrentControlSet",
            @"Software\Policies\Microsoft\Windows NT\Terminal Services", // RDP host
            @"Software\Policies\Microsoft\Windows NT\CurrentVersion\Winlogon",
            @"Software\Policies\Microsoft\Cryptography",
            @"Software\Policies\Microsoft\Windows\System\Logon",
            @"Software\Policies\Microsoft\Windows\NetworkProvider"
        };

        /// <summary>
        /// registry.pol バイナリを解析してポリシー一覧を取得
        /// 仕様: [key;value;type;size;data] （すべてUTF-16LEセパレータ）
        /// </summary>
        public List<GpoPolicyEntry> ParseRegistryPol(byte[] polBytes, string scope = "Machine")
        {
            var entries = new List<GpoPolicyEntry>();
            if (polBytes == null || polBytes.Length < 8) return entries;

            using var ms = new MemoryStream(polBytes);
            using var reader = new BinaryReader(ms, Encoding.Unicode);

            // ヘッダー検査 (8 bytes: "PReg" + Version)
            byte[] sig = reader.ReadBytes(4);
            if (sig.Length < 4 || sig[0] != 'P' || sig[1] != 'R' || sig[2] != 'e' || sig[3] != 'g')
                throw new InvalidDataException("無効な PReg シグネチャです。");

            uint version = reader.ReadUInt32();
            if (version != HeaderVersion)
                throw new InvalidDataException($"未サポートの PReg バージョン: {version}");

            // レコード解析
            while (ms.Position < ms.Length)
            {
                if (ms.Length - ms.Position < 4) break; // 最低ブラケット分もない

                char startBracket = reader.ReadChar();
                if (startBracket != '[') continue;

                string key = ReadNullTerminatedString(reader);
                // セパレータ ';' のスキップ（ReadNullTerminatedStringで消費されなかった場合）
                ConsumeOptionalSemicolon(reader);

                string value = ReadNullTerminatedString(reader);
                ConsumeOptionalSemicolon(reader);

                uint type = reader.ReadUInt32();
                ConsumeOptionalSemicolon(reader);

                uint size = reader.ReadUInt32();
                ConsumeOptionalSemicolon(reader);

                byte[] data = reader.ReadBytes((int)size);

                // 末尾の ']' をスキップ
                while (ms.Position < ms.Length)
                {
                    char c = reader.ReadChar();
                    if (c == ']') break;
                }

                object? displayVal = FormatDataValue(type, data);
                var entry = new GpoPolicyEntry
                {
                    Scope = scope,
                    KeyPath = key,
                    ValueName = value,
                    Type = type,
                    ValueData = displayVal,
                    FriendlyName = ResolveFriendlyName(key, value),
                    Explanation = $"レジストリ設定: {key}\\{value}"
                };

                EvaluateConversionFeasibility(entry);
                entries.Add(entry);
            }

            return entries;
        }

        /// <summary>
        /// ポリシー一覧を registry.pol バイナリにシリアライズ（可逆）
        /// </summary>
        public byte[] SerializeRegistryPol(IEnumerable<GpoPolicyEntry> entries)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, Encoding.Unicode);

            // ヘッダー書き込み
            writer.Write(HeaderSignature);
            writer.Write(HeaderVersion);

            foreach (var entry in entries)
            {
                writer.Write('[');
                WriteNullTerminatedString(writer, entry.KeyPath);
                writer.Write(';');
                WriteNullTerminatedString(writer, entry.ValueName);
                writer.Write(';');
                writer.Write(entry.Type);
                writer.Write(';');

                byte[] dataBytes = ConvertValueToBytes(entry.Type, entry.ValueData);
                writer.Write((uint)dataBytes.Length);
                writer.Write(';');
                writer.Write(dataBytes);
                writer.Write(']');
            }

            return ms.ToArray();
        }

        public (List<GpoPolicyEntry> converted, List<GpoPolicyEntry> skipped) ConvertMachinePoliciesToUser(IEnumerable<GpoPolicyEntry> machinePolicies)
        {
            var converted = new List<GpoPolicyEntry>();
            var skipped = new List<GpoPolicyEntry>();

            foreach (var policy in machinePolicies)
            {
                if (IsMachineOnlyPolicy(policy.KeyPath))
                {
                    var skippedCopy = ClonePolicy(policy);
                    skippedCopy.ExclusionReason = "コンピューター専用ポリシー（ハードウェア/暗号/OS基盤）のためユーザー構成への移植は安全に除外されました。";
                    skipped.Add(skippedCopy);
                    continue;
                }

                var userPolicy = ClonePolicy(policy);
                userPolicy.Scope = "User";
                userPolicy.Explanation = $"[変換済] ユーザー構成への移植ポリシー: {policy.KeyPath}\\{policy.ValueName}";
                converted.Add(userPolicy);
            }

            return (converted, skipped);
        }

        public bool IsMachineOnlyPolicy(string keyPath)
        {
            if (string.IsNullOrWhiteSpace(keyPath)) return false;
            foreach (var prefix in MachineOnlyPrefixes)
            {
                if (keyPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private void EvaluateConversionFeasibility(GpoPolicyEntry entry)
        {
            if (entry.Scope == "Machine" && IsMachineOnlyPolicy(entry.KeyPath))
            {
                entry.CanConvertToUser = false;
                entry.ExclusionReason = "マシン専用アーキテクチャ（BitLocker/セキュリティコア等）のため変換不可";
            }
            else
            {
                entry.CanConvertToUser = true;
                entry.ExclusionReason = "移植可能";
            }
        }

        private static string ReadNullTerminatedString(BinaryReader reader)
        {
            var sb = new StringBuilder();
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                char c = reader.ReadChar();
                if (c == '\0') break;
                if (c == ';')
                {
                    // セミコロンで区切られた場合は巻き戻さずに終了
                    break;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        private static void ConsumeOptionalSemicolon(BinaryReader reader)
        {
            if (reader.BaseStream.Position >= reader.BaseStream.Length) return;
            long pos = reader.BaseStream.Position;
            char c = reader.ReadChar();
            if (c != ';')
            {
                reader.BaseStream.Position = pos; // 違ったら戻す
            }
        }

        private static void WriteNullTerminatedString(BinaryWriter writer, string s)
        {
            foreach (char c in s) writer.Write(c);
            writer.Write('\0');
        }

        private static object? FormatDataValue(uint type, byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            if (type == 4 && data.Length >= 4) // REG_DWORD
                return BitConverter.ToUInt32(data, 0);
            if (type == 1) // REG_SZ
                return Encoding.Unicode.GetString(data).TrimEnd('\0');
            return BitConverter.ToString(data).Replace("-", " ");
        }

        private static byte[] ConvertValueToBytes(uint type, object? value)
        {
            if (value == null) return Array.Empty<byte>();
            if (type == 4)
            {
                uint val = Convert.ToUInt32(value);
                return BitConverter.GetBytes(val);
            }
            if (type == 1)
            {
                string s = value.ToString() ?? "";
                return Encoding.Unicode.GetBytes(s + "\0");
            }
            if (value is byte[] bytes) return bytes;
            return Array.Empty<byte>();
        }

        private static string ResolveFriendlyName(string key, string value)
        {
            if (key.Contains("Edge", StringComparison.OrdinalIgnoreCase))
                return $"Microsoft Edge 設定 ({value})";
            if (key.Contains("ScreenSaver", StringComparison.OrdinalIgnoreCase))
                return "スクリーンセーバー・ロックアウト設定";
            if (key.Contains("WindowsUpdate", StringComparison.OrdinalIgnoreCase))
                return "Windows Update 配信設定";
            if (key.Contains("FVE", StringComparison.OrdinalIgnoreCase))
                return "BitLocker ドライブ暗号化設定";
            return string.IsNullOrEmpty(value) ? key : $"{key} [{value}]";
        }

        private static GpoPolicyEntry ClonePolicy(GpoPolicyEntry source)
        {
            return new GpoPolicyEntry
            {
                Scope = source.Scope,
                Category = source.Category,
                KeyPath = source.KeyPath,
                ValueName = source.ValueName,
                Type = source.Type,
                ValueData = source.ValueData,
                FriendlyName = source.FriendlyName,
                Explanation = source.Explanation,
                CanConvertToUser = source.CanConvertToUser,
                ExclusionReason = source.ExclusionReason
            };
        }
    }
}
