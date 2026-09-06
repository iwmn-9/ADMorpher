# ADMorpher — Active Directory 統合運用・セキュリティ統制スイート

[![Build, Test & Release](https://github.com/iwmn-9/ADMorpher/actions/workflows/build-test-release.yml/badge.svg)](https://github.com/iwmn-9/ADMorpher/actions/workflows/build-test-release.yml)
[![Platform](https://img.shields.io/badge/platform-Windows%20WPF-blue.svg)](https://dotnet.microsoft.com/)
[![Framework](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

> **情シス三部作 第2弾：Active Directory 運用自動化・GPOスマート管理・JITローカルAdmin・J-SOX監査統制スタジオ**  
> 親会社からの「サプライチェーンセキュリティ統制」の要求に対し、親会社以上のセキュリティ水準と完全な監査エビデンス（J-SOX対応Excel台帳）をワンクリックで叩き出す情シスのための自衛・統制ツール。

---

## 主な機能

### 1. 🛡️ ダッシュボード & DC/DNS/DHCPヘルス
- 全ドメインコントローラー（DC）の死活、複製（Replication）同期遅延、FSMOロール保持DCの常時監視。
- **DNSゾンビレコード検出**: 昔撤去された旧DCのSRVレコード（認証・ログオン遅延の原因）を自動検出。
- **DHCPスコープ枯渇アラート**: IPアドレス使用率（80%注意、90%危険）をリアルタイム可視化。

### 2. 🧹 アカウント衛生管理（ADゴーストバスター）
- 90日/180日以上の休眠アカウント、退職後も放置された無効化アカウントを抽出。
- **セキュリティリスク設定の検出**: パスワード無期限（`PasswordNeverExpires`）、Kerberos事前認証不要（AS-REP Roasting脆弱アカウント）をあぶり出し。
- **安全退避スナップショット**: アカウントを即削除せず、現行の所属グループ情報をJSONバックアップした上で安全に退避OUへ隔離。

### 3. 👥 権限・グループ可視化（ネスト逆引き & 循環参照検知）
- 深くネストされたグループ構造をツリー展開し、循環参照（A ➔ B ➔ A）を自動検知して赤色警告。
- ユーザーの実効権限（親・先祖グループ経由で保持している全権限）の逆引きインスペクター。

### 4. 📋 アカウント ライフサイクル自動化
- 新入社員台帳CSV（氏名、部署、役職）のドラッグ＆ドロップによる事前検証と一括登録。
- 暗号学的に安全な初期パスワード自動付与、初回ログオン時パスワード変更要求の自動構成。
- 1クリック退職者オフボーディング（即時無効化＋ランダムパスワード変更＋所属グループ全解除）。

### 5. ⚙️ GPOスマートマネージャー（★独自キラー機能）
- **有効設定フラットビュー**: 深すぎる階層ツリーを排除し、実際に変更されている設定値だけをテーブル一覧表示。
- **階層レス・キーワード検索**: 「Edge」「パスワード」「壁紙」「スクリーンセーバー」等を即座に検索。
- **コンピューター構成 ⇔ ユーザー構成の相互移植**:
  - `registry.pol`（PReg形式バイナリ）を高速解析。
  - BitLocker等のハードウェア・基盤専用キーを自動判定（ブラックリスト除外）し、移植可能なポリシーのみを安全にユーザー構成へ変換。
  - SYSVOLの完全Zipバックアップを事前自動生成。

### 6. 🔑 JITローカルAdmin（Windows LAPS連携）
- クライアントPCのキッティングやトラブル対応時のみ、ワンタイムでローカル管理者パスワードを表示・安全コピー。
- **作業完了後の即時ローテーション強制**: ボタン1クリックで有効期限をリセットし、次回即座に別のランダムパスワードへ更新（Pass-the-Hash攻撃リスクを完全遮断）。

### 7. 📊 J-SOX & 親会社監査提出用 美麗Excelレポート
- ClosedXMLによるワンクリック台帳出力。
- エグゼクティブサマリー、休眠・要注意アカウント一覧、GPO一覧、JIT端末一覧を美麗フォーマットで生成。

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

### 自動回帰テストスイート（6/6 ALL PASSED）
```powershell
& ".\bin\Debug\net8.0-windows\win-x64\ADMorpher.exe" --test-regression
```
