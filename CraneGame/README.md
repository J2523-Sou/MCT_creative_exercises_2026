# Crane Game

`クレーン2.fbx` のモデルPrefabを筐体内の可動台へ組み込むクレーンゲームです。

## 必須依存

- Unity標準の `MonoBehaviour`、`Rigidbody`、`Collider`、Coroutine

## 任意依存

- cluster Creator Kit 3.x
  - `Cluster/` 内だけがcluster Scriptable Item APIを使用します。
  - 共通C#はcluster名前空間へ依存しません。

## Editor操作

- WASDまたは矢印: 水平移動
- SpaceまたはG: 把持シーケンス
- R: クレーンと景品をリセット
- 筐体前面の立体ボタンもマウスで操作可能

## 実装方法

### 1. シーンへ配置する

基本構成は `Prefabs/CraneGame.prefab` に保存されています。シーンへPrefabを配置し、
PrefabルートのScaleを `(1, 1, 1)` にしてください。構成を作り直す場合は、Unity Editorの
`Tools > Crane Game > Build or Repair Crane2 Game` を実行します。このメニューは次をまとめて行います。

- `クレーン2.fbx` を可動台へ配置
- 筐体、床、排出口、操作ボタン、案内表示、照明を生成
- 爪のColliderと左右の接触センサーを設定
- 景品、Rigidbody、Collider、復活地点を生成
- 共通C#コンポーネントとcluster用Scriptable Itemを接続
- `CraneGame.prefab` と `Prize.prefab` を保存

既存の `CraneGameRoot` がある場合、メニューは全面置換せず参照とColliderを修復します。
構築後は `Tools > Crane Game > Validate Crane Game` を実行し、必須参照や物理Componentを確認できます。

### 2. 共通C#の構成

入力とゲームロジックは次の順に分離しています。

```text
KeyboardCraneInputAdapter / CraneWorldButton
    -> ICraneCommandReceiver
    -> CraneController
    -> CraneGrabSequence
    -> CraneClawController / PrizeGripAssist / PrizeRespawner
    -> Unity Physics
```

- `CraneController`: `SetMoveInput(Vector2)`、`TryStartGrab()`、`RequestReset()` を公開し、水平移動と可動域を管理します。
- `CraneGrabSequence`: 下降、爪閉じ、上昇、DropPosition移動、解放、HomePosition復帰をCoroutineで順番に実行します。実行中は追加入力を拒否します。
- `CraneClawController`: FBXの開いた姿勢を基準に左右の爪を対称回転させます。
- `ClawContactSensor`: 左右の爪が接触中の `Prize` をそれぞれ記録します。
- `PrizeGripAssist`: 同じ景品が左右両方のセンサーに触れた場合だけ、破断可能な `FixedJoint` で把持を補助します。距離超過または確率判定で滑るため、常に成功する構成ではありません。
- `PrizeRespawner`: 排出口への到達、Y下限、ローカル範囲外、寿命、Resetを監視します。空いているRespawnPositionを選び、速度と角速度を消して復活させます。

別の入力環境を追加する場合はTransformを直接動かさず、`ICraneCommandReceiver` の3メソッドだけを呼ぶAdapterを実装してください。これによりクレーン本体を変更せず、PC、UI、VR入力へ差し替えられます。

### 3. Inspector参照

Prefabを手動で組み直す場合は、最低限次を割り当てます。

| Component | 必須参照 |
| --- | --- |
| `CraneController` | `MovementRoot = Carriage`、`MovementSpace`、`GrabSequence`、`PrizeRespawner` |
| `CraneGrabSequence` | `Controller`、`Carriage`、`LiftAssembly`、`Claw`、`GripAssist`、`HomePosition`、`DropPosition` |
| `CraneClawController` | 左右の爪Transform、ローカル回転軸 |
| `PrizeGripAssist` | `GripAnchor`、左右の `ClawContactSensor` |
| `PrizeRespawner` | Prefabルートを基準にしたCoordinateSpace、複数のRespawnPosition |
| `Prize` | `PrizeRespawner` と非Kinematic Rigidbody |
| `KeyboardCraneInputAdapter` | `CraneController` |

移動速度、可動域、下降距離、各速度、爪の角度、把持補助、滑り確率、復活範囲・時間は各ComponentのInspectorから調整できます。Scene固有の名前検索はランタイムでは行いません。

### 4. 景品を追加・差し替えする

`Prize.prefab` を複製するか、任意のGameObjectへ `Prize`、Collider、Rigidbodyを追加します。
Rigidbodyは非Kinematicにし、極端に大きい質量や反発を避けてください。`Prize.respawner` を割り当て、
景品数と同数以上のRespawnPositionを用意すると重なりを避けやすくなります。形状変更後は、爪先端の
Colliderと景品が初期状態で重ならず、閉じたときだけ左右センサーへ接触することを確認してください。

### 5. cluster版を接続する

cluster固有処理は `Cluster/` 内のJavaScriptだけにあります。

1. `MovementSpace` をScriptable Itemにし、`CraneClusterController.js` を割り当てます。
2. 各操作ボタンを個別のItemにし、対応する `CraneButtonLeft/Right/Forward/Back/Grab/Reset.js` を割り当てます。
3. 各景品をMovable Itemにし、`CraneClusterPrize.js` を割り当てます。
4. クレーンItem、ボタンItem、景品Itemを兄弟階層に置き、Itemの子へ別Itemを入れないでください。
5. 爪先の `LeftGripSensor` と `RightGripSensor` にはOverlap Detector Shapeを設定します。

ボタンは半径3m以内へ `mct-crane-command` を送り、クレーンItemが状態遷移を一元管理します。
把持時は左右両センサーが同じItemを検出した場合だけ `mct-crane-prize-route` を景品へ送り、景品側Adapterが
上昇・排出口移動に追従します。共通C#はcluster名前空間を参照しないため、Creator KitがないUnity環境でもコンパイルできます。

cluster JavaScriptはItem内の `Carriage`、`LiftAssembly`、`claw_armL`、`claw_armL.001`、
`LeftGripSensor`、`RightGripSensor`、`GripAnchor` を参照します。cluster用Prefab階層を手動変更する場合は、
これらの子Object名もスクリプト先頭の定数と合わせて変更してください。

### 6. 動作確認

Play Modeで次を順に確認します。

1. 移動入力中だけ動き、X/Zの可動域を超えない。
2. Grabを1回押すと、下降、閉じる、上昇、排出口移動、開く、Home復帰の順で動く。
3. シーケンス中にGrabを連打しても多重実行されない。
4. 左右の爪が同じ景品へ接触した場合に持ち上がり、失敗時も景品が吹き飛ばない。
5. 排出口、範囲外、Y下限、寿命、Resetの各条件で景品が復活し、速度が残らない。
6. Consoleにコンパイルエラー、NullReferenceException、MissingReferenceExceptionがない。

## 座標とPrefab

- 1 Unity Unit = 約1mを想定します。
- `CraneGameRoot` のScaleは `(1,1,1)` を維持してください。
- 移動範囲、HomePosition、DropPosition、RespawnPositionはPrefab内Transformです。
- 別Sceneへ移す場合は `CraneGame.prefab` を配置するだけでEditor用入力が動作します。

## 初期調整値

- 共通C#: 手動可動域 X `-0.95..0.95`、Z `-0.40..0.40`
- cluster Adapter: 手動可動域 X `-0.95..0.95`、Z `-0.22..0.22`
- cluster Adapter: Drop位置 `(0.95, -0.22)`、ボタン命令範囲 `3m`
- 下降距離: 共通C# `1.15`、cluster Adapter `1.72`
- 把持補助の滑り確率: 毎秒 `0.04`
- 景品寿命: `90秒`、復活遅延: `1.2秒`、Y下限: `-1.5`

clusterの値はScriptable ItemのInspector項目ではなく、各JavaScript先頭の定数です。Prefabのスケールや
モデル形状を変更した場合は、共通C#のInspector値とcluster Adapterの定数を同じ実寸になるよう個別に調整してください。
