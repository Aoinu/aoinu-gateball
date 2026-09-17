# v0.1 Physics Calibration

測定日: 2026-09-17

## Environment

- Unity 2022.3.22f1
- VRChat SDK / UdonSharp project dependencies from `Packages/manifest.json`
- Development Scene: `Assets/Aoinu Works/Gateball/Scenes/VRCDefaultWorldScene.unity`
- Project Fixed Timestep: 0.020 s
- Play Mode実効 Fixed Timestep: 0.0111111114 s（ClientSim/VR実行時の値）
- Rigidbody physicsを使用。独自Physics Solverは追加していない。

Ballは直径0.075 m、質量0.230 kg、SphereCollider半径0.5をscale 0.075で使用した。測定はBall01、初期位置 `(0, 0.0375, -7.5)`、進行方向`Vector3.forward`で行い、他球との衝突を除外した単独レーンで実施した。停止は速度0.03 m/s以下を基準にした。

## Baseline

コード変更前のDevelopment Sceneでは、他球との衝突を除外した同条件で以下を観測した。

| Stroke | Impulse | Initial speed | Travel distance | Stop time | Issue |
| --- | ---: | ---: | ---: | ---: | --- |
| Weak | 1.5 | 6.48 m/s | 8.80 m | 16.21 s | 既定値として強すぎる |
| Normal | 3.5 | 15.16 m/s | 17.46 m | 1.48 s | Court端でBoundary/Out |
| Strong | 6.0 | 26.01 m/s | 17.46 m | 0.73 s | Court端でBoundary/Out |

## Final calibration

Desktop、Test Shot、VR Malletの最大Impulseを1.40に揃え、Weak / Normal / Strongを0.60 / 1.00 / 1.40とした。最終値の同一条件測定は、同じPlay Mode Fixed Timestepで実UdonのStrokeイベントを発火して行った。

| Stroke | Final Impulse | Initial speed | Travel distance | Stop time |
| --- | ---: | ---: | ---: | ---: |
| Weak | 0.60 | 2.58 m/s | 2.89 m | 14.88 s |
| Normal | 1.00 | 4.32 m/s | 5.07 m | 15.51 s |
| Strong | 1.40 | 6.05 m/s | 7.98 m | 16.08 s |

Weak < Normal < Strongが初速・停止距離ともに分離し、Strongでも通常Courtの全長を大きく超えない。Test Shotの補助シナリオもこのレンジ内に揃えた。

`OutMargin`は`BallRadius * 2`、すなわち0.075 mに変更した。物理Boundary collisionを取り逃した場合でも、旧値0.35 mのように大きく遅れてOut判定されない値である。

MalletのSweepはHeadのBoxColliderについて、world-space AABBの`bounds.extents`を使わず、`BoxCollider.size`と`lossyScale`からhalf extentsを算出するようにした。Development Sceneで確認したHeadはsize `(1,1,1)`、lossyScale `(0.078, 0.135, 0.0264)`、half extents `(0.039, 0.0675, 0.0132)`相当である。実Play ModeのVR Mallet sweepでBall01にImpulse 1.40が適用された。

## QA results

- UdonSharp compile: succeeded, 0 errors
- EditMode tests: 9/9 passed
- PlayMode tests: 8/8 passed
- Desktop Stroke: Weak / Normal / Strong = 0.60 / 1.00 / 1.40
- VR Mallet Stroke: Ball01 hit and routed Impulse 1.40
- Ball-Ball collision: Ball01 touch count 1, target Ball02
- Gate passage: Gate1 progress 1
- Gate post collision: Ball01 count 1
- Out: Ball01 Out=true, Boundary collision count 1
- Goal Pole: Ball01 hit=true, collision count 1
- Test Shot reset/replay: initial position reproduced exactly and replayed Impulse 0.60

v0.2のPhysics divergence比較では、Test ShotのStraight Weak / Straight Strongを代表入力とし、NormalはDesktop `_StrokeNormal()` の1.00を基準入力として使用する。すべてFixedUpdate / Rigidbody経由で、FPS依存の`Update`測定や一時的な計測コードは成果物に残していない。
