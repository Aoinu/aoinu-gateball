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

Development Sceneは物理・同期を単独で確認するSandboxです。Gameplayパッケージは残しますが、このSceneではMatch、Turn、Score、Sparkを生成しません。Desktopでは `DesktopControls` を操作し、`TestShotController` はInteractでPresetを切り替えて固定Test Shotを実行します。

VRでは `GripA` または `GripB` をPickupします。最初に掴んだGripがPrimary、もう一方がSecondaryになり、両Gripの間隔でMalletの軸を決めます。StrikeFaceをBall近くへ向けるとローカルのAim Previewが表示されます。Primary GripのUse DownでAimを固定してArmedになり、実際にStrikeFaceをBallへ振り当てると打球します。Use UpでAimを解除します。Armed前の接触、StrikeFace以外の接触、選択中ではないBallへの接触はShotになりません。

`NetworkState` はShotStartでInitial Linear/Angular Velocityを同期し、各ClientがLocal Rigidbody Physicsを実行してOwnerのShotEnd状態へ収束します。MalletRigの表示位置・回転はVRC Object Syncで別に同期し、Aim、Grip、Hit判定は各Clientのローカル処理に限定します。`Telemetry` はOwner/Remote、Shot phase、初期速度、FixedUpdateと補正誤差を開発用に記録します。

EditMode/PlayModeテストは **Window > General > Test Runner** から実行します。テストはパッケージの `Tests/Editor` と `Tests/Runtime` にあり、物理ルール、時間窓速度推定、Aim Assist、初期速度同期、Gameplayコードの個別契約を確認します。Gameplay統合の説明は `Documentation~/gameplay-integration-v0.3.md`、Distributed Physicsの測定記録は `Documentation~/distributed-physics-poc-v0.2.md` にあります。VRChat上の複数Client確認はSDK BuilderのBuild & Testを使用します。

Unityのシリアライズ差分には`git-vrc` 0.1.0（filter v1）を使用します。clone後に次を実行してください。

```powershell
git config --local include.path ../.gitconfig
git-vrc --version
```

## Documentation

配布Runtimeは `Packages/pm.booth.aoinu607.udon.gateball/Runtime`、パッケージ固有の説明とテストは同パッケージ内の `Documentation~` と `Tests` にあります。開発用Sceneと配置物は現時点ではこの開発リポジトリの `Assets/Aoinu Works/Gateball/` にのみ含まれます。

個人の実メールアドレス、認証情報、ローカル専用パス、権利未確認のアセットは公開ファイルへ追加しないでください。
