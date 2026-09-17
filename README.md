# Aoinu Gateball

VRChat向けゲートボールギミックの開発プロジェクトです。v0.2.0では、実寸の10球Rigidbody物理を各Clientで同じShotStartから実行し、Stroke開始OwnerだけがShotEndの正式結果を確定するDistributed Physics PoCを検証します。正式競技ルールは後続範囲です。

## Package

- Package ID: `pm.booth.aoinu607.udon.gateball`
- Namespace: `Pm.Booth.Aoinu607.Udon.Gateball`
- Unity: `2022.3`
- VRChat Worlds SDK: `3.10.x`（開発確認: `3.10.5`）
- License: MIT（第三者アセットは各ライセンスに従います）

リリース後はVCCのリポジトリ一覧へ次のURLを登録して導入します。

`https://aoinu.github.io/aoinu-gateball/index.json`

## Development

Unity 2022.3.22f1でプロジェクトを開き、VPM Resolverによる依存関係の復元とUdonSharpコンパイルを待ちます。開発用Sceneは `Assets/Aoinu Works/Gateball/Scenes/VRCDefaultWorldScene.unity`、Scene生成メニューは `Aoinu Gateball/Build Development Scene` です。開発専用のマテリアルや配置物は `Assets/Aoinu Works/Gateball/` に置きます。

主要寸法は Ball 直径0.075 m・質量0.230 kg、Gate開口0.22 m × 0.19 m、Gate post直径0.02 m・高さ0.20 m、Goal Pole直径0.02 m・高さ0.20 m、Court 15 m × 20 mです。停止・Gate/Out/Goal軌道サンプリングとMallet履歴はFixedUpdate基準で評価します。

Desktopでは `DesktopControls` を操作し、左右で方向、上下で強度、Use/Interactで選択中のBallを打撃します。VRでは `VRMallet` をPickupし、実際のMallet Head形状による通常HitまたはSweepでBallへ振ります。`TestShotController` はInteractでPresetを切り替え、Useで固定Test Shotを実行します。v0.2では `NetworkState` が10球の初期位置、Stroke、phase、ShotEndの正式位置をManual Syncし、各ClientのRigidbody結果はShotEndまでローカルに保持します。`Telemetry` はFixedUpdateごとの位置・速度・イベント列とOwner/Remote誤差を開発用に記録します。

EditMode/PlayModeテストは **Window > General > Test Runner** から実行します。テストはパッケージの `Tests/Editor` と `Tests/Runtime` にあり、GeometryのForward/Reverse/Edge/Above-bar、実寸値、Rigidbody stroke、低速停止、Ball-Ball、Gate post、Goal Pole、Test Shot再現性、Shot state copy、phase、telemetry誤差、event divergence、late-join static reconstruction、Mallet head hit/sweepを回帰確認します。VRChat上の確認はSDK BuilderのBuild & Testを使用し、測定記録は `Documentation~/distributed-physics-poc-v0.2.md` に追記します。

Unityのシリアライズ差分には`git-vrc` 0.1.0（filter v1）を使用します。clone後に次を実行してください。

```powershell
git config --local include.path ../.gitconfig
git-vrc --version
```

## Documentation

配布Runtimeは `Packages/pm.booth.aoinu607.udon.gateball/Runtime`、パッケージ固有の説明とテストは同パッケージ内の `Documentation~` と `Tests` にあります。開発用Sceneと配置物は現時点ではこの開発リポジトリの `Assets/Aoinu Works/Gateball/` にのみ含まれます。

個人の実メールアドレス、認証情報、ローカル専用パス、権利未確認のアセットは公開ファイルへ追加しないでください。
