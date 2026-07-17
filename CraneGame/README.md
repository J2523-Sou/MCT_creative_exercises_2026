# Crane Game

`newCrene.fbx` と `magatama.fbx` を使用した、Unity／cluster向けクレーンゲームです。
完成済みの `Prefabs/CraneGame.prefab` に筐体、クレーン、景品、操作ボタン、排出口、照明、入力、物理設定をまとめています。

## まず動かす：別シーンへの導入手順

同じUnityプロジェクト内なら、次の手順だけで導入できます。

1. 導入先のSceneを開きます。
2. Projectウィンドウで `CraneGame/Prefabs/CraneGame.prefab` を選びます。
3. PrefabをHierarchyへドラッグします。
4. `CraneGameRoot` のPositionとRotationだけを、設置したい場所へ合わせます。
5. `CraneGameRoot` のScaleが必ず `(1, 1, 1)` であることを確認します。
6. `Tools > Crane Game > Validate Crane Game` を実行します。
7. Consoleに `CraneGame validation PASS` が表示されることを確認します。
8. Play Modeを開始し、WASD／矢印キーで移動、Space／GでGrab、RでResetを確認します。
9. Sceneを保存します。

Prefab内部の参照は設定済みです。通常はInspectorで参照を割り当て直す必要はありません。

## 導入時にしてよい変更／避ける変更

してよい変更：

- `CraneGameRoot` のPositionとRotationを変更する
- `HomePosition`、`DropPosition`、`RespawnPosition_*` を調整する
- 各Componentの速度、待機時間、滑り確率、景品寿命を調整する
- `Prize.prefab` を複製して景品数を増やす

避ける変更：

- `CraneGameRoot` を拡大・縮小する
- `Carriage`、`LiftAssembly`、爪だけへ非一様Scaleを追加する
- `MovementSpace`、ボタン、景品のcluster Itemを親子にする
- `Carriage` や `LiftAssembly` をAnimationとScriptの両方から同時に動かす
- `claw_armL`、`claw_armL.001`、各Sensorの名前を変更する

筐体サイズを変える場合はPrefabルートではなく、Editor構築スクリプトの設定を変更して再構築してください。

## Prefabに含まれるもの

```text
CraneGameRoot                     ゲーム全体。Scaleは(1,1,1)
├── Cabinet                       筐体、PlayField、天井、フレーム
├── MovementSpace                 クレーンのローカル座標基準／cluster Controller Item
│   └── Carriage                  水平移動
│       └── LiftAssembly          上下移動
│           ├── newCrene          本体、左右の爪、Convex MeshCollider
│           └── GripAnchor        把持補助の基準
├── HomePosition                  待機位置
├── DropPosition                  景品を離す位置
├── Prizes                        magatama景品
├── RespawnPositions              景品の復活位置
├── PrizeChute                    排出口
├── Controls                      移動、Grab、Resetボタン
├── CraneInteriorLight            照明
└── GuideDisplay                  操作案内
```

## 操作方法

- WASD／矢印キー：前後左右へ移動
- Space／G：Grabシーケンス開始
- R：クレーンと全景品をReset
- Editorでは筐体前面の立体ボタンもマウス操作可能

入力がない間は移動しません。Grab中は水平入力とGrabの多重実行を受け付けません。

## 実装の流れ

### 1. 入力を共通APIへ渡す

入力側はTransformを直接変更せず、`ICraneCommandReceiver` を呼びます。

```csharp
receiver.SetMoveInput(new Vector2(x, z));
receiver.TryStartGrab();
receiver.RequestReset();
```

- Editor入力：`KeyboardCraneInputAdapter.cs`
- 立体ボタン：`CraneWorldButton.cs`
- 共通受信API：`ICraneCommandReceiver.cs`
- cluster入力：`Cluster/CraneButton*.js`

入力環境を追加するときは、新しいAdapterからこの3メソッドだけを呼びます。

### 2. 水平移動を処理する

`CraneController.cs` が入力を受け取り、`Carriage` を `MovementSpace` のローカル座標基準で移動します。

1. 入力値を正規化します。
2. `moveSpeed * Time.deltaTime` でフレームレート非依存の移動量を求めます。
3. Xを `-1.70..1.70`、Zを `-0.90..0.90` にClampします。
4. 入力を離すと移動値をゼロへ戻します。
5. Grab中は入力を無効化します。

端に到達しても逆方向入力は受け付けます。

### 3. 初期位置を天井へ合わせる

`CraneGameSetup.cs` の `AlignCraneToCeiling` が、newCrene全Rendererの上端と天井Colliderの内面を計測します。

1. newCreneのRenderer Bounds上端を取得します。
2. `Cabinet/Top` の内面Yを取得します。
3. 上端と内面の差だけ `Carriage` を移動します。
4. 同じYを `HomePosition` と `DropPosition` に保存します。

現在の初期値はCarriage Y約`4.467`、newCrene上端と天井内面はY`4.830`で一致します。

### 4. 爪を床面まで下降させる

`CraneGrabSequence.cs` は固定下降量だけに依存せず、Grab開始時に実際のBoundsを再計算します。

1. newCrene全Rendererの最下点を取得します。
2. `PlayField` Colliderの上面を取得します。
3. 最下点から床上面と`floorClearance`を引いて下降距離を求めます。
4. Inspectorの`lowerDistance`を最大値としてClampします。
5. `LiftAssembly` を算出位置まで下降させます。

標準設定では実下降距離が約`2.975`、開いた爪と床の隙間が約`0.01`です。モデルや床高を変えてもC#側は実Boundsから再計算します。

### 5. 爪を閉じて景品を把持する

1. `CraneClawController` が左右の爪を対称に回転します。
2. `LeftGripSensor` と `RightGripSensor` が接触中の`Prize`を記録します。
3. 両Sensorが同じ景品へ触れた場合だけ `PrizeGripAssist` が把持を補助します。
4. 補助には破断可能な`FixedJoint`を使用します。
5. 距離超過や滑り確率で景品を落とすため、成功率は100%固定ではありません。

爪と景品は簡略化したConvex MeshColliderを使用します。動的Rigidbodyへ非Convex MeshColliderは使用しません。

### 6. Grabシーケンスを進める

`CraneGrabSequence` が次の順に状態を進めます。

```text
Idle
  -> Lowering
  -> Closing
  -> Lifting
  -> MovingToDrop
  -> Releasing
  -> ReturningHome
  -> Idle
```

1. 水平操作を停止します。
2. 床面まで下降します。
3. 爪を閉じます。
4. 景品を持ち上げます。
5. `DropPosition` へ移動します。
6. 爪を開いて景品を離します。
7. `HomePosition` へ戻ります。
8. 操作を再開します。

### 7. Resetする

`RequestReset()` は次を一括実行します。

1. 実行中のCoroutineを停止します。
2. 把持中のJointを解放します。
3. 爪を開きます。
4. `LiftAssembly` を上昇位置 `(0, 0, 0)` へ戻します。
5. `Carriage` をYを含む `HomePosition` 全座標へ戻します。
6. 全景品を重ならないRespawnPositionへ戻します。
7. 速度と角速度をゼロにします。

Yを含めて復元するため、Reset後に古い高さへ戻ることはありません。

### 8. 景品を復活させる

`PrizeRespawner.cs` が次の条件を監視します。

- 排出口へ入った
- Yが`-1.5`未満になった
- ローカル監視範囲外へ出た
- 90秒経過した
- Resetが押された

復活時は空いている`RespawnPosition_*`を選び、Rigidbodyの速度と角速度を消します。

## Inspector参照

Prefabには次の参照が保存済みです。手動で再構築する場合だけ設定してください。

| Component | 設定する参照 |
| --- | --- |
| `CraneController` | `MovementRoot = Carriage`、`MovementSpace`、`GrabSequence`、`PrizeRespawner` |
| `CraneGrabSequence` | `Controller`、`Carriage`、`LiftAssembly`、`Claw`、`GripAssist`、`HomePosition`、`DropPosition`、`CraneModel = newCrene`、`PlayFieldCollider` |
| `CraneClawController` | `claw_armL`、`claw_armL.001`、ローカル回転軸 |
| `PrizeGripAssist` | `GripAnchor`、左右の`ClawContactSensor` |
| `PrizeRespawner` | `CoordinateSpace = CraneGameRoot`、複数の`RespawnPosition` |
| `Prize` | `PrizeRespawner`、非Kinematic Rigidbody、子のConvex MeshCollider |
| `KeyboardCraneInputAdapter` | `CraneController` |

## 景品を増やす／差し替える

1. `Prefabs/Prize.prefab` を複製します。
2. 複製した景品を `CraneGameRoot/Prizes` の子へ置きます。
3. Rigidbodyを非Kinematicにします。
4. 見た目に合うConvex Colliderを設定します。
5. `Prize.respawner` にルートの`PrizeRespawner`を割り当てます。
6. 景品数以上の`RespawnPosition`を用意します。
7. 爪と景品が初期状態で重ならないことを確認します。
8. Grab成功率と滑り方をPlay Modeで調整します。

標準のmagatama景品は、子の`MagatamaVisual`へ簡略化Convex MeshColliderを設定しています。

## clusterで使用する

Prefabにはcluster用ItemとClusterScriptの参照も保存済みです。

1. cluster Creator Kit 3.xが導入されていることを確認します。
2. `CraneGame.prefab` をSceneへ配置します。
3. `MovementSpace` にItem／Scriptable Itemと `CraneClusterController.js` が設定されていることを確認します。
4. 各ボタンに個別のItem／Scriptable Itemと対応する `CraneButton*.js` が設定されていることを確認します。
5. 各景品にItem／Movable Item／Scriptable Itemと `CraneClusterPrize.js` が設定されていることを確認します。
6. `LeftGripSensor` と `RightGripSensor` にOverlap Detector Shapeがあることを確認します。
7. `Tools > Crane Game > Validate Crane Game` を実行します。
8. Creator Kitのアップロード前チェックを実行します。
9. clusterへアップロードして全ボタン、Grab、Resetを確認します。

cluster Itemの配置規則：

- `MovementSpace`、各ボタン、各景品は兄弟Itemにします。
- Itemの子へ別Itemを入れないでください。
- ボタンは半径10m以内へ `mct-crane-command` を送ります。
- 1Sceneへ複数台置く場合は、他台へ命令が届かないようメッセージ名または通信範囲を台ごとに分けてください。

cluster版の高さと下降基準は `CraneClusterController.js` 先頭の定数です。モデルや筐体寸法を変えた場合は、C#側の実測結果に合わせて `HOME_Y` と `MINIMUM_GRIP_HEIGHT` を更新してください。

## 自動構築／修復メニュー

`Tools > Crane Game > Build or Repair newCrene Game` は次を実行します。

1. `newCrene.fbx` と `magatama.fbx` を読み込みます。
2. 筐体、操作盤、排出口、照明を作成または修復します。
3. 爪と景品のConvex MeshColliderを作成します。
4. 左右Sensorと把持補助を接続します。
5. Home、Drop、Respawn位置を設定します。
6. newCrene上端を天井内面へ整列します。
7. 床面到達用の参照を `CraneGrabSequence` へ保存します。
8. cluster Item階層とClusterScript参照を修復します。
9. `CraneGame.prefab` と `Prize.prefab` を保存します。
10. Validatorを実行します。

既存の`CraneGameRoot`がある場合は全面置換せず、既存ルートを維持して参照とColliderを修復します。

## 検証手順

1. `Tools > Crane Game > Validate Crane Game` を実行します。
2. `ceilingGap=0.000`付近であることを確認します。
3. `floorTravel=2.975`付近であることを確認します。
4. Play Modeで入力を離すと停止することを確認します。
5. 端まで移動後、逆方向へ戻れることを確認します。
6. Grabで床面まで下降することを確認します。
7. `Lowering -> Closing -> Lifting -> MovingToDrop -> Releasing -> ReturningHome -> Idle` の順に進むことを確認します。
8. Reset直後にGrabしても同じ床面へ到達することを確認します。
9. 景品が排出口、範囲外、Y下限、寿命、Resetで復活することを確認します。
10. Consoleにコンパイルエラー、NullReferenceException、MissingReferenceExceptionがないことを確認します。

## 初期調整値

- PrefabルートScale：`(1, 1, 1)`
- 想定単位：1 Unity Unit = 約1m
- 筐体グループ：標準寸法の1.5倍
- 移動速度：`1.5 units/sec`
- 可動域：X `-1.70..1.70`、Z `-0.90..0.90`
- Home：`(0, 4.467, 0)`
- Drop：`(1.70, 4.467, -0.90)`
- C#最大下降距離：`3.65`
- C#実下降距離：約`2.975`
- 床クリアランス：約`0.01`
- cluster最大下降距離：`3.58`
- cluster GripAnchor最低Y：`0.46`
- ボタン通信範囲：`10m`
- 景品質量：`0.34`
- 把持補助の滑り確率：毎秒`0.04`
- 景品寿命：`90秒`
- 復活遅延：`1.2秒`
- Y下限：`-1.5`

## 依存関係

必須：

- Unity標準 `MonoBehaviour`、`Transform`、`Rigidbody`、`Collider`、Coroutine

cluster利用時のみ：

- cluster Creator Kit 3.x
- `Cluster/` 内のJavaScriptだけがcluster固有APIを使用します。
- 共通C#はcluster名前空間へ直接依存しません。

別プロジェクトへ移植する場合は、`.meta`を含む`CraneGame`フォルダ全体をコピーしてください。モデル、Material、Collider Mesh、Prefab、ScriptのGUID参照を維持できます。
