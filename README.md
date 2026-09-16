# Aoinu Gateball

VRChat向けゲートボールギミックの開発プロジェクトです。現在のv0.1.0は、10球のローカル物理、3ゲート、Goal Pole、Desktop/VR打撃、Gate・Touch・Out・Goal判定を検証するLocal Gateball Coreです。ネットワーク同期や正式競技ルールは後続範囲です。

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

Desktopでは `DesktopControls` を操作し、左右で方向、上下で強度、Use/Interactで選択中のBallを打撃します。VRでは `VRMallet` をPickupし、Mallet HeadをBallへ振ります。`TestShotController` はInteractでPresetを切り替え、Useで固定Test Shotを実行します。

EditMode/PlayModeテストは **Window > General > Test Runner** から実行します。テストはパッケージの `Tests/Editor` と `Tests/Runtime` にあり、PlayModeの物理確認には実行時Sceneが使われます。VRChat上の確認はSDK BuilderのBuild & Testを使用します。

Unityのシリアライズ差分には`git-vrc` 0.1.0（filter v1）を使用します。clone後に次を実行してください。

```powershell
git config --local include.path ../.gitconfig
git-vrc --version
```

## Documentation

配布Runtimeは `Packages/pm.booth.aoinu607.udon.gateball/Runtime`、パッケージ固有の説明とテストは同パッケージ内の `Documentation~` と `Tests` にあります。開発用Sceneと配置物は現時点ではこの開発リポジトリの `Assets/Aoinu Works/Gateball/` にのみ含まれます。

個人の実メールアドレス、認証情報、ローカル専用パス、権利未確認のアセットは公開ファイルへ追加しないでください。
