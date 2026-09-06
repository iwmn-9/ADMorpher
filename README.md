# ADMorpher — Active Directory 統合運用・セキュリティ統制スタジオ

[![Build, Test & Release](https://github.com/iwmn-9/ADMorpher/actions/workflows/build-test-release.yml/badge.svg)](https://github.com/iwmn-9/ADMorpher/actions/workflows/build-test-release.yml)
[![Platform](https://img.shields.io/badge/platform-Windows%20WPF-blue.svg)](https://dotnet.microsoft.com/)
[![Framework](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
[![Status](https://img.shields.io/badge/status-Prototype%20%2F%20High--Fidelity%20Simulator-orange.svg)](https://github.com/iwmn-9/ADMorpher)

> **【ステータス: 高忠実度シミュレータ / Prototype (v1.1.0)】**  
> 本ツールは現在、**実機ドメインコントローラー投入直前の高忠実度シミュレーション＆安全検証スタジオ**として設計・動作しています。オフライン検証モード（Mock Mode）と実環境向けデータプロバイダ（IAdDataProvider）が完全分離されており、破壊的操作を行わずにすべての運用シナリオを安全にシミュレーション可能です。

> **情シス三部作 第2弾：Active Directory 運用自動化・GPOスマート管理・JITローカルAdmin・J-SOX監査統制スタジオ**  
> 親会社からの「サプライチェーンセキュリティ統制」の要求に対し、親会社以上のセキュリティ水準と完全な監査エビデンス（J-SOX対応Excel台帳）をワンクリックで叩き出す情シスのための自衛・統制ツール。

---

## 主な機能（全8機能スタジオ）

### 1. 🛡️ ダッシュボード & DCヘルスチェック (Tab 1)
- 全ドメインコントローラー（DC）の死活、複製（Replication）同期遅延、FSMOロール保持DCの常時監視。
- 総合健全性スコア（Health Score）のリアルタイム算出。

### 2. 🌐 DNS & DHCP 基盤管理スタジオ (Tab 2)
- **DNSゾンビSRVレコード安全削除シミュレーション**: 昔撤去された旧DCのSRVレコード（認証・ログオン遅延の原因）を事前ゾーンバックアップ（JSONスナップショット）付きで安全に削除シミュレーション。
- **長期未更新Aレコード棚卸し**: 90日以上更新のない放置端末レコードを検出。
- **DHCPスコープ枯渇対策 & 放置固定予約解放**: 180日以上未通信の放置MACアドレス固定予約を自動特定し、IPアドレスプールを安全に回収。

### 3. 🧹 アカウント衛生管理（ADゴーストバスター） (Tab 3)
- 90日/180日以上の休眠アカウント、退職後も放置された無効化アカウントを抽出。
- **セキュリティリスク設定の検出**: パスワード無期限（`PasswordNeverExpires`）、Kerberos事前認証不要（AS-REP Roasting脆弱アカウント）をあぶり出し。
- **安全退避スナップショット**: アカウントを即削除せず、現行の所属グループ情報（動的取得）をJSONバックアップした上で選択アカウントのみを安全に退避OUへ隔離。

### 4. 👥 権限・グループ可視化（ネスト逆引き & 循環参照検知） (Tab 4)
- 深くネストされたグループ構造をツリー展開し、循環参照（A ➔ B ➔ A）をDFS（深さ優先探索）アルゴリズムで動的検知し赤色警告。
- ユーザーの実効権限（親・先祖グループ経由で保持している全権限）の逆引きインスペクター。

### 5. 📋 アカウント ライフサイクル自動化 (Tab 5)
- 新入社員台帳CSV（氏名、部署、役職）のドラッグ＆ドロップによる事前検証と一括登録。
- 暗号学的に安全な初期パスワード自動付与、初回ログオン時パスワード変更要求の自動構成。
- 1クリック退職者オフボーディング（即時無効化＋ランダムパスワード変更＋所属グループ全解除）。

### 6. ⚙️ GPOスマートマネージャー (Tab 6)
- **有効設定フラットビュー**: 深すぎる階層ツリーを排除し、実際に変更されている設定値だけをテーブル一覧表示。
- **階層レス・キーワード検索**: 「Edge」「パスワード」「壁紙」「スクリーンセーバー」等を即座に検索。
- **コンピューター構成 ⇔ ユーザー構成の安全変換**:
  - `registry.pol`（PReg形式バイナリ）を高速可逆解析。
  - ホワイトリスト検証（Edge/Office/Explorer等）およびブラックリスト除外（BitLocker/Defenderコア等）の二重防御により、移植可能と証明された設定のみを安全にユーザー構成へ変換。
  - SYSVOLの完全Zipバックアップを事前自動生成。
- **OUリンク & セキュリティフィルター配備シミュレーション**: GPOを適用する対象OUの追加/解除（Enforce設定対応）およびセキュリティフィルター（適用対象グループ）の絞り込みをシミュレーション。

### 7. 🔑 JITローカルAdmin（Windows LAPS連携） (Tab 7)
- クライアントPCのキッティングやトラブル対応時のみ、ワンタイムでローカル管理者パスワードを表示・安全コピー。
- **作業完了後の即時ローテーション強制**: ボタン1クリックで有効期限を現在時刻へリセットし、平文パスワードの再利用リスク（Pass-the-Hash等）を最小化。
- **Windows LAPS 自動有効化・権限配備ウィザード**: 対象OUのコンピュータ自身へのパスワード書き込み権限（`SELF`）および監査グループへの読み取り権限を自動付与するPowerShellスクリプトとLAPS GPO設定（Active Directoryバックアップ: `BackupDirectory = 2`）をワンクリック自動生成。

### 8. 📊 J-SOX & 親会社監査提出用 美麗Excelレポート (Tab 8)
- ClosedXMLによるワンクリック台帳出力。
- エグゼクティブサマリー、休眠・要注意アカウント一覧、GPO一覧、JIT端末一覧を監査提出可能な美麗フォーマットで生成。

---

## クイックスタート & 検証

### ビルド
```powershell
& "$HOME\.dotnet\dotnet.exe" build -c Release
```

### 配布用単一EXEの生成（Release Self-Contained）
```powershell
& "$HOME\.dotnet\dotnet.exe" publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
Copy-Item -Path ".\bin\Release\net8.0-windows\win-x64\publish\ADMorpher.exe" -Destination ".\ADMorpher.exe" -Force
```

### 自動回帰テストスイート（9/9 ALL PASSED CI品質ゲート）
```powershell
& ".\bin\Debug\net8.0-windows\win-x64\ADMorpher.exe" --test-regression
```
