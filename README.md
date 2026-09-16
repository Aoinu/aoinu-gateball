# Aoinu Gateball

VRChat向けゲートボールギミックの開発プロジェクトです。VPMパッケージとして配布し、v0.3で10球・複数人プレイと分散Physics同期の技術検証を完了することを目標にします。現在は公開開発用のv0.1.0パッケージ骨格です。

## Package

- Package ID: `pm.booth.aoinu607.udon.gateball`
- Namespace: `Pm.Booth.Aoinu607.Udon.Gateball`
- Unity: `2022.3`
- VRChat Worlds SDK: `3.10.x`（開発確認: `3.10.5`）
- License: MIT（第三者アセットは各ライセンスに従います）

リリース後はVCCのリポジトリ一覧へ次のURLを登録して導入します。

`https://aoinu.github.io/aoinu-gateball/index.json`

## Development

Unity 2022.3.22f1でプロジェクトを開き、VPM Resolverによる依存関係の復元とUdonSharpコンパイルを待ちます。EditMode/PlayModeテストはUnity Test Runner、VRChat上の確認はSDK BuilderのBuild & Testを使用します。Unityを実行するGitHub Actions CIはRunner方針確定後に追加します。

Unityのシリアライズ差分には`git-vrc` 0.1.0（filter v1）を使用します。clone後に次を実行してください。

```powershell
git config --local include.path ../.gitconfig
git-vrc --version
```

## Documentation

仕様の中心は日本語のMVP定義です。実装時のネットワーク権限・同期状態の詳細は、Owner、Ownership transfer、Late Join、Disconnect recoveryを確認しながら別途確定します。

個人の実メールアドレス、認証情報、ローカル専用パス、権利未確認のアセットは公開ファイルへ追加しないでください。
