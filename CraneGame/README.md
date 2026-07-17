# cluster Crane Game

cluster Creator Kitで動作するクレーンゲームです。

- クレーンモデル：`newCrene.fbx`
- 景品モデル：`magatama.fbx`
- 完成済みPrefab：`CraneGame/Prefabs/CraneGame.prefab`
- cluster用処理：`CraneGame/Cluster/*.js`

筐体、クレーン、景品、操作ボタン、排出口、照明、Collider、Rigidbody、ClusterScript参照はPrefabへ保存済みです。
基本的にはPrefabをSceneへ配置し、位置と向きを合わせてアップロード前検証を行うだけで導入できます。

## 必要な環境

- Unity 6.2系
- cluster Creator Kit 3.x
- clusterへアップロード可能なCreator Kit設定
- `CraneGame` フォルダ一式

別プロジェクトへ移植する場合は、GUID参照を維持するため `.meta` を含む `CraneGame` フォルダ全体をコピーしてください。

## 最短導入手順

### 1. cluster用Sceneを開く

1. clusterへアップロードするSceneをUnityで開きます。
2. SceneにSpawn Pointなど、通常のclusterワールド設定があることを確認します。
3. Sceneを保存します。

### 2. CraneGame Prefabを配置する

1. Projectウィンドウで `CraneGame/Prefabs/CraneGame.prefab` を選びます。
2. PrefabをHierarchyへドラッグします。
3. 追加された `CraneGameRoot` を選択します。
4. Positionを設置場所へ合わせます。
5. RotationのYだけを、筐体を向けたい方向へ変更します。
6. Scaleが必ず `(1, 1, 1)` であることを確認します。

筐体全体を回転させる場合は、子Objectではなく `CraneGameRoot` を回転させてください。Y軸回転には対応しています。
X/Z方向へ筐体を傾ける構成は、重力と床判定がワールドY方向になるため非対応です。

### 3. Prefab階層を確認する

Prefabは次の構成です。

```text
CraneGameRoot                     Itemではないゲーム全体のルート
├── Cabinet                       筐体、PlayField、天井、フレーム
├── MovementSpace                 クレーン制御用Item／Scriptable Item
│   └── Carriage                  水平移動
│       └── LiftAssembly          上下移動
│           ├── newCrene          クレーン本体と左右の爪
│           └── GripAnchor        把持位置の基準
├── HomePosition                  初期位置
├── DropPosition                  景品を離す位置
├── Prizes
│   ├── Prize_1                   Movable Item／Scriptable Item
│   ├── Prize_2                   Movable Item／Scriptable Item
│   └── Prize_3                   Movable Item／Scriptable Item
├── RespawnPositions              景品の復活位置
├── PrizeChute                    排出口
├── Controls
│   ├── LeftButton                個別Item／Scriptable Item
│   ├── RightButton               個別Item／Scriptable Item
│   ├── ForwardButton             個別Item／Scriptable Item
│   ├── BackButton                個別Item／Scriptable Item
│   ├── GrabButton                個別Item／Scriptable Item
│   └── ResetButton               個別Item／Scriptable Item
├── CraneInteriorLight
└── GuideDisplay
```

clusterではItemの子へ別のItemを配置できません。次を維持してください。

- `CraneGameRoot` 自体はItemにしない
- `MovementSpace`、各ボタン、各景品は兄弟Itemにする
- `Carriage` と `LiftAssembly` は `MovementSpace` Itemの子にする
- ボタンItemや景品Itemを別Itemの子へ移動しない

### 4. クレーン用ClusterScriptを確認する

`MovementSpace` を選択し、次を確認します。

1. Itemコンポーネントがある
2. Scriptable Itemコンポーネントがある
3. Source Code Assetに `CraneGame/Cluster/CraneClusterController.js` が設定されている
4. `Carriage`、`LiftAssembly`、`newCrene` が子階層にある
5. `LeftGripSensor` と `RightGripSensor` にOverlap Detector Shapeがある

`CraneClusterController.js` が水平移動、下降、爪の開閉、上昇、Drop移動、Home復帰を管理します。

### 5. 操作ボタン用ClusterScriptを確認する

各ボタンには、次のJavaScriptが設定されています。

| Button | ClusterScript |
| --- | --- |
| `LeftButton` | `CraneButtonLeft.js` |
| `RightButton` | `CraneButtonRight.js` |
| `ForwardButton` | `CraneButtonForward.js` |
| `BackButton` | `CraneButtonBack.js` |
| `GrabButton` | `CraneButtonGrab.js` |
| `ResetButton` | `CraneButtonReset.js` |

各ボタンを選択し、Item、Scriptable Item、Collider、対応するSource Code Assetが設定されていることを確認します。

ボタンは半径10m以内へ `mct-crane-command` を送信します。移動命令は `MovementSpace` のControllerが受信します。

### 6. 景品用ClusterScriptを確認する

各 `Prize_*` を選択し、次を確認します。

1. Itemコンポーネントがある
2. Movable Itemコンポーネントがある
3. Scriptable Itemコンポーネントがある
4. Source Code Assetに `CraneClusterPrize.js` が設定されている
5. Rigidbodyが非Kinematicである
6. 子の `MagatamaVisual` にConvex MeshColliderがある

景品は排出口、Y下限、水平範囲外、寿命、Resetを条件として復活します。

### 7. 自動Validatorを実行する

Unityメニューから次を実行します。

```text
Tools > Crane Game > Validate Crane Game
```

Consoleで次を確認します。

```text
Crane clearance PASS
Crane vertical placement PASS
CraneGame validation PASS
```

標準状態では次の値になります。

- `ceilingGap=0.000`付近
- `floorTravel=2.975`付近
- Home Y=`4.467`付近

ValidatorがFAILEDの場合はアップロードせず、表示された参照、Item階層、Colliderを修正してください。

### 8. Creator Kitのアップロード前チェックを行う

1. Creator Kitのアップロード画面を開きます。
2. 現在のSceneが対象になっていることを確認します。
3. Item入れ子、Missing Script、Collider、容量に関するエラーがないことを確認します。
4. エラーがある場合はアップロード前に修正します。
5. 問題がなければワールドをアップロードします。

### 9. cluster実機で確認する

アップロード後、cluster上で次を順番に確認します。

1. 各移動ボタンが反応する
2. クレーンが筐体外へ出ない
3. 端まで移動した後、逆方向へ戻れる
4. Grabボタンで下降を開始する
5. 爪が床面付近まで届く
6. 爪が閉じて景品を持ち上げる
7. Drop位置へ移動する
8. 景品を離す
9. クレーンがHomeへ戻る
10. Reset後も正しい高さへ戻る
11. Reset直後のGrabでも床面へ届く
12. 景品が条件に応じてRespawnする

## cluster上の動作順序

Grab命令を受けると、`CraneClusterController.js` が次の状態を進めます。

```text
idle
  -> lowering
  -> closing
  -> contactSettle
  -> lifting
  -> drop
  -> opening
  -> releaseWait
  -> home
  -> idle
```

1. 水平移動命令を停止します。
2. `LiftAssembly` を床面付近まで下降します。
3. 左右の爪を閉じます。
4. 両方のOverlap Detectorが同じ景品Itemを検出した場合だけ搬送命令を送ります。
5. 景品とクレーンを同じ時間軸で上昇させます。
6. Drop位置へ水平移動します。
7. 爪を開きます。
8. 景品を離します。
9. Homeへ戻ります。
10. 再び操作可能になります。

Grab命令の多重実行は受け付けません。

## 初期位置と床面調整

### 天井への配置

newCreneのRenderer上端は、筐体天井の内面へ揃えています。

- Carriage／Home Y：約`4.467`
- newCrene上端Y：約`4.830`
- 天井内面Y：約`4.830`

`Tools > Crane Game > Build or Repair newCrene Game` を実行すると、Renderer Boundsから天井位置を再計算します。

### 床面への下降

標準設定では次の値です。

- cluster最大下降距離：`3.58`
- 実際の下降距離：約`2.974`
- GripAnchor最低Y：`0.46`
- 開いた爪と床面の隙間：約`0.01`

cluster版は `CraneClusterController.js` 先頭の `HOME_Y`、`LOWER_DISTANCE`、`MINIMUM_GRIP_HEIGHT` を使用します。
モデル、床、筐体の寸法を変更した場合は、Unity Validatorの実測値に合わせてこれらを更新してください。

## Reset処理

Resetボタンは次を実行します。

1. CarriageをHome `(0, 4.467, 0)` へ戻す
2. LiftAssemblyを `(0, 0, 0)` へ戻す
3. 爪を開く
4. 移動命令を解除する
5. 実行中のシーケンスをIdleへ戻す
6. 景品をRespawn位置へ戻す

起動時の古い座標をReset先として再利用せず、Prefabと同じHome定数へ戻します。

## 景品の追加方法

1. `CraneGame/Prefabs/Prize.prefab` を複製します。
2. 複製した景品を `CraneGameRoot/Prizes` の子へ配置します。
3. 既存の景品Itemの子には配置しないでください。
4. Item、Movable Item、Scriptable Itemを確認します。
5. `CraneClusterPrize.js` を設定します。
6. Rigidbodyを非Kinematicにします。
7. Convex Colliderを設定します。
8. 景品数以上の `RespawnPosition_*` を用意します。
9. 初期位置で景品同士や爪と重ならないことを確認します。

標準の `Prize.prefab` はmagatamaモデル、Rigidbody、簡略化Convex MeshColliderを設定済みです。

## 別Sceneで使用する

同じプロジェクト内の別Sceneでは、次の作業だけで利用できます。

1. `CraneGame.prefab` を新しいSceneへ配置する
2. `CraneGameRoot` のPositionとY Rotationを設定する
3. Scaleを `(1, 1, 1)` にする
4. Validatorを実行する
5. Creator Kitのアップロード前チェックを行う
6. cluster上でボタンとGrabを確認する

ランタイム処理はScene名や `GameObject.Find` に依存しません。Prefab内部参照で動作します。

## 筐体を回転する

`CraneGameRoot` のY Rotationを変更すると、筐体、操作盤、Home、Drop、Respawn、クレーン座標系がまとめて回転します。

例：

- 通常向き：Y Rotation=`0`
- 反対向き：Y Rotation=`180`

Editor用C#とcluster Controllerは、MovementSpaceまたはItemのローカル座標で移動します。

### 複数台／背中合わせの注意

現在のボタンは半径10mへ同じ `mct-crane-command` を送ります。2台を10m以内へ置くと、別筐体のControllerも命令を受信する可能性があります。

背中合わせを実装する場合は、次のいずれかが必要です。

- 筐体ごとに異なるメッセージ名を使用する
- ボタン送信位置をController側で検証し、自分の操作盤からの命令だけ受信する
- 通信範囲が重ならない距離へ離す

1台だけ配置する場合は追加対応不要です。

## 自動構築／修復

Prefab参照やColliderを修復する場合は、次を実行します。

```text
Tools > Crane Game > Build or Repair newCrene Game
```

このメニューは次を自動処理します。

1. newCreneとmagatamaを読み込む
2. 筐体、床、天井、操作盤、排出口を修復する
3. 爪と景品のConvex MeshColliderを生成する
4. 左右Grip Sensorを設定する
5. Home、Drop、Respawn位置を設定する
6. newCrene上端を天井内面へ揃える
7. C#検証用の床Collider参照を保存する
8. cluster Item階層を修復する
9. ClusterScript参照を設定する
10. `CraneGame.prefab` と `Prize.prefab` を保存する
11. Validatorを実行する

既存の `CraneGameRoot` がある場合は、ルートを全面置換せず現在のPrefab構成を維持して修復します。

## Editorでの確認

clusterへアップロードする前の補助確認として、Editor用C#入力も使用できます。

- WASD／矢印キー：水平移動
- Space／G：Grab
- R：Reset
- 立体ボタン：マウス操作

Editor用処理はcluster固有APIへ依存しません。clusterで実際に動く処理は `Cluster/` 内のJavaScriptです。

## 主な設定値

- PrefabルートScale：`(1, 1, 1)`
- 筐体倍率：`1.5`
- 移動速度：`1.5 units/sec`
- 可動域X：`-1.70..1.70`
- 可動域Z：`-0.90..0.90`
- Home：`(0, 4.467, 0)`
- Drop：`(1.70, 4.467, -0.90)`
- ボタン通信範囲：`10m`
- 景品質量：`0.34`
- 滑り確率：毎秒`0.04`
- 景品寿命：`90秒`
- Respawn遅延：`1.2秒`
- Respawn Y下限：`-1.5`

## 関連ファイル

```text
CraneGame/
├── Cluster/
│   ├── CraneClusterController.js
│   ├── CraneClusterPrize.js
│   └── CraneButton*.js
├── Editor/
│   └── CraneGameSetup.cs
├── Models/
│   ├── newCrene.fbx
│   ├── magatama.fbx
│   └── Colliders/
├── Prefabs/
│   ├── CraneGame.prefab
│   └── Prize.prefab
└── Scripts/                       Editor検証／移植用の共通C#
```
