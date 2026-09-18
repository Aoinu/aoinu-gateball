# Aoinu Gateball

VRChat向けゲートボールギミックの開発プロジェクトです。v0.3.0では、実寸の10球Rigidbody物理を各Clientで同じShotStartから実行し、Stroke開始OwnerだけがShotEndの正式結果を確定するDistributed Physics architectureの上に、2人以上のMatch、打順、Gate、得点、Touch、Spark、Out、late join、disconnect recoveryを統合します。正式競技ルール完全実装ではありません。

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

Desktopでは `DesktopControls` を操作し、左右で方向、上下で強度、Use/Interactで選択中のBallを打撃します。VRでは `VRMallet` をPickupし、実際のMallet Head形状による通常HitまたはSweepでBallへ振ります。`PracticeMode` と `MatchMode` でモードを開始し、`SparkPlacement` はTouch後の簡易配置を確定します。`TestShotController` はInteractでPresetを切り替え、Useで固定Test Shotを実行します。`NetworkState` はv0.2のShotStart/ShotEnd同期を維持し、`GameplayState` はassignment、turn、Gate/Score、Touch/Spark、Out、GameOverをManual Syncします。`Telemetry` はFixedUpdateごとの位置・速度・イベント列とOwner/Remote誤差を開発用に記録します。

EditMode/PlayModeテストは **Window > General > Test Runner** から実行します。テストはパッケージの `Tests/Editor` と `Tests/Runtime` にあり、v0.1/v0.2の物理回帰に加え、v0.3のassignment、turn、Gate/Score、Touch/Spark、Out、GameOver、disconnect、snapshot applyを確認します。詳細と最終検証結果は `Documentation~/gameplay-integration-v0.3.md` に記録します。VRChat上の確認はSDK BuilderのBuild & Testを使用し、v0.2の測定記録は `Documentation~/distributed-physics-poc-v0.2.md` に残します。

Unityのシリアライズ差分には`git-vrc` 0.1.0（filter v1）を使用します。clone後に次を実行してください。

```powershell
git config --local include.path ../.gitconfig
git-vrc --version
```

## Documentation

配布Runtimeは `Packages/pm.booth.aoinu607.udon.gateball/Runtime`、パッケージ固有の説明とテストは同パッケージ内の `Documentation~` と `Tests` にあります。開発用Sceneと配置物は現時点ではこの開発リポジトリの `Assets/Aoinu Works/Gateball/` にのみ含まれます。

個人の実メールアドレス、認証情報、ローカル専用パス、権利未確認のアセットは公開ファイルへ追加しないでください。
