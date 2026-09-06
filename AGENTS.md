# ADMorpher — AI Agent & Developer Architecture Guide

> **【AIメンテナ・自律継続規約】**  
> 本プロジェクトは「自律完遂（自ら調査・修正・検証まで行い、完成状態で返す）」を基本方針とする。  
> ⚠️ **後続のAIメンテナへ**: コードベースの構造、ファイル責務、または重要な設計制約を変更した場合は、**必ずこの `AGENTS.md` も最新の状態に同期・更新すること**。過去の設計意図（第3項 ADR）を無視した安易なコードの巻き戻しを禁止する。

---

## 1. プロジェクト概要 & 技術スタック

- **アプリケーション名**: `ADMorpher`
- **種別**: Windows デスクトップ向け Active Directory 統合管理・GPOスマートマネージャー・JITローカルAdmin・セキュリティ統制スタジオ
- **フレームワーク**: .NET 8.0 (Windows WPF), C# 12
- **依存パッケージ**:
  - `ClosedXML` (0.104.2, J-SOX/監査用Excel出力)
  - `System.DirectoryServices` (8.0.0, AD通信用)
  - `System.DirectoryServices.AccountManagement` (8.0.0)
  - `System.DirectoryServices.Protocols` (8.0.0)
- **ビルド形態**: `Release win-x64` の **自己完結型（Self-Contained）単一実行可能ファイル (`ADMorpher.exe`)**
  - ネイティブWPFエンジンDLLはすべてEXE内部にバンドルされる。
- **実行環境デュアルモード**:
  - `Live AD Mode`: 実機ドメインコントローラー環境での本番稼働。
  - `Mock Mode`: ドメイン未参加開発機・CI環境用のオフライン検証データプロバイダ (`MockAdService.cs`)。

---

## 2. システム鳥瞰マップ（機能とソースコードの対応表）

UI層は `MainWindow.xaml` / `MainWindow.xaml.cs` に集約され、内部ロジックは `Services` と `Models` に完全に分離されている。

| 機能領域 / タブ | XAML (MainWindow.xaml) | C# コードビハインド | 関連 Service / Model | 責務と概要 |
| :--- | :--- | :--- | :--- | :--- |
| **全体共通 / 左サイドバー** | `DockPanel` (L108-144) | `NavTab_Checked` | `Converters/ValueConverters.cs` | 7タブ切り替えナビゲーション、ステータスバー表示 |
| **Tab 1: ヘルスチェック**<br>(DC / DNS / DHCP) | `Tab0_Health` (L150-244) | `LoadInitialData` | `AdHealthReport`<br>`DcInfo`<br>`MockAdService.cs` | 全DCの死活・複製状況・FSMO、AD統合DNSのゾンビDCレコード検出、DHCPスコープ枯渇アラート |
| **Tab 2: アカウント衛生管理**<br>(ADゴーストバスター) | `Tab1_Hygiene` (L246-288) | `ScanHygiene_Click`<br>`QuarantineSelected_Click`<br>`OpenSnapshotsDir_Click` | `AccountHygieneService.cs`<br>`AccountHygieneItem` | 90/180日休眠アカウント、PasswordNeverExpires、AS-REP Roasting脆弱アカウントの検出、JSONスナップショット退避 |
| **Tab 3: 権限・グループ可視化**<br>(Nested Permissions) | `Tab2_Permissions` (L290-322) | `LoadInitialData` | `GroupNestNode`<br>`EffectivePermissionUser` | グループ階層ツリーの再帰展開、循環参照（A ➔ B ➔ A）の自動検知と赤色警告 |
| **Tab 4: ライフサイクル管理**<br>(Lifecycle Manager) | `Tab3_Lifecycle` (L324-367) | `LoadCsv_Click`<br>`PreviewImport_Click`<br>`ExecuteImport_Click` | `LifecycleService.cs`<br>`LifecycleImportRow` | 社員台帳CSV読み込み、命名規則重複チェック、安全な初期パスワード自動付与、退職者オフボーディング |
| **Tab 5: GPOスマートマネージャー**<br>(GPO Smart Studio) | `Tab4_Gpo` (L369-411) | `ConvertGpo_Click`<br>`BackupGpo_Click` | `GpoManagerService.cs`<br>`GpoSummary`<br>`GpoPolicyEntry` | 有効設定のフラット一覧、`registry.pol` PRegバイナリパース、Computer ➔ User変換（ブラックリスト除外判定） |
| **Tab 6: JITローカルAdmin**<br>(LAPS JIT Concierge) | `Tab5_Jit` (L413-455) | `RevealJitPassword_Click`<br>`ForceRotateJit_Click` | `JitAdminService.cs`<br>`JitDevice` | LAPSパスワードのマスク表示・安全コピー、作業後の即時再ローテーション強制（Pass-the-Hash防御） |
| **Tab 7: 監査Excelレポート**<br>(J-SOX Audit Report) | `Tab6_Report` (L457-488) | `ExportExcel_Click` | `ExcelAuditReportService.cs` | ClosedXMLベースのJ-SOX・親会社監査提出用 美麗Excel台帳ワンクリック出力 |
| **ヘッドレス自動テスト** | ― | `App.xaml.cs` (`--test-regression`) | `RegressionTestService.cs` | 6大回帰テスト（PRegバイナリ可逆性、変換除外、JITエントロピー、退避完全性、循環参照検知、Excel生成）の自動実行 |

---

## 3. 重要な設計判断の記録（Architecture Decisions / ADR）

後続のAIは、以下の仕様を「不具合」と誤認して勝手に書き換えてはならない。

1. **GPO Computer ➔ User 変換の安全原則（ブラックリスト除外）**:
   - `GpoManagerService.ConvertMachinePoliciesToUser` は、BitLocker (`Software\Policies\Microsoft\FVE`)、Defenderコア、システムドライバ等のMachine専用キーを絶対にユーザー側へ変換してはならない。必ずブラックリストでスキップし、理由（`ExclusionReason`）を明記すること。
2. **registry.pol PRegバイナリのセパレータ仕様**:
   - PRegレコードフォーマットは `[key;value;type;size;data]` であり、区切り文字 `;` は UTF-16LE（2バイト）として扱われる。パースおよびシリアライズの可逆性を崩してはならない。
3. **JITローカル管理者の即時ローテーション（JIT保証）**:
   - 一度表示・使用したローカル管理者パスワードは、作業完了時に `ForceImmediateRotation` を呼び出して期限を即座に現在時刻へ変更し、次回再ローテーションを強制しなければならない（平文の使い回しを物理的に防ぐ）。
4. **アカウント断捨離の安全原則（勝手に削除しない）**:
   - 休眠アカウントや退職者アカウントは直ちに削除せず、必ず元OUおよび所属グループ一覧をJSONスナップショットに保存した上で、退避OUへの移動・無効化にとどめること。
5. **外部監査役（GPT 5.6 Sol）との品質ゲート運用**:
   - コード修正後は、必ず `--test-regression` を実行し、全テスト（6/6）が PASSED であることを確認すること。新たなエッジケースが発見された場合は、必ず回帰テストスイートにテスト項目を追加すること。

---

## 4. ビルド・実行・検証コマンド

コンテキストを持たないAIが修正を行った後は、必ず以下のコマンドで検証すること。

### ビルド（0 警告・0 エラーを維持すること）
```powershell
& "$HOME\.dotnet\dotnet.exe" build -c Release
```

### 配布用単一EXEの生成（Release self-contained）
```powershell
& "$HOME\.dotnet\dotnet.exe" publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
Copy-Item -Path ".\bin\Release\net8.0-windows\win-x64\publish\ADMorpher.exe" -Destination ".\ADMorpher.exe" -Force
```

### 自動回帰テストスイート（ヘッドレス自己検証・CIゲート）
```powershell
& ".\bin\Debug\net8.0-windows\win-x64\ADMorpher.exe" --test-regression
```
