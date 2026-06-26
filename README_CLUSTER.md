# cluster 用 JavaScript 版

`throw.cs`、`ball.cs`、`BowlingPin.cs`、`BowlingPinManager.cs` を cluster Creator Kit の Scriptable Item 向け JavaScript に変換したものです。

## ファイル

- `throw.js`: 発射用アイテムに設定します。掴んで「使う」、または掴めない配置アイテムを左クリックして「使う」と、ボールを生成して前方へ飛ばします。
- `ball.js`: ボールのテンプレートに設定します。生成から10秒後に自動削除し、掴んで「使う」を押したあと手放すと前方へ撃力を加えます。
- `BowlingPin.js`: 各ピンに設定します。初期位置を記録し、一定の高さ以下に落ちたら場外ピンとして扱います。
- `BowlingPinManager.js`: ピン群の近くに置く管理用アイテムに設定します。周囲のピンへ問い合わせ、場外ピン数の表示と一括復帰を行います。

## Creator Kit 側の設定

1. ボール用の World Item Template を作り、ID を `ball` にします。
2. そのボールに `Scriptable Item` を追加し、`ball.js` を設定します。
3. ボールに `Grabbable Item`、Collider、物理挙動する `Movable Item` を追加します。`Use Item Trigger` は追加しないでください。付いていると `ball.js` の `$.onUse` は呼ばれません。
4. 発射用アイテムに `Scriptable Item` を追加し、`throw.js` を設定します。
5. 掴んで撃つ場合は、発射用アイテムに `Grabbable Item` と Collider を追加します。`Use Item Trigger` は追加しないでください。付いていると `throw.js` の `$.onUse` は呼ばれません。
6. 配置したまま左クリックで撃つ場合は、発射用アイテムを掴めない状態にして Collider を追加します。この場合は `$.onInteract` で発射します。

`throw.js` 内の `BALL_TEMPLATE_ID`、`SHOT_SPEED`、`MUZZLE_OFFSET`、`MAX_SHOT_COUNT` は用途に合わせて調整できます。

## ボウリングピンの設定

1. 各ピンに `Scriptable Item` を追加し、`BowlingPin.js` を設定します。
2. 各ピンに Collider と物理挙動する `Movable Item` を追加します。
3. ピン群の近くに管理用アイテムを置き、`Scriptable Item` に `BowlingPinManager.js` を設定します。
4. 管理用アイテムの子に Renderer を持たない通常の GameObject を作り、名前を `ScoreText` にして `Text View` を追加します。子の `ScoreText` には `Item` コンポーネントを付けません。
5. 管理用アイテムを左クリック/タップ/VRトリガーで「使う」と、周囲のピンに復帰メッセージを送ります。
6. 現在のピン配置を復帰位置として記録し直したい場合は、`BowlingPinManager.js` の `CAPTURE_POSE_ON_GRAB` を `true` にして管理用アイテムを掴みます。

`BowlingPin.js` の `DESPAWN_HEIGHT`、`BowlingPinManager.js` の `PIN_SEARCH_RADIUS`、`REPORT_INTERVAL_SECONDS`、`EXPECTED_PIN_COUNT` は配置に合わせて調整してください。`AUTO_RESPAWN_DELAY_SECONDS` を 0 以上にすると、全ピン場外後に自動復帰できます。

## Text View の使い方

場外ピン数は、管理用アイテムの子オブジェクト `ScoreText` に付けた Text View に表示します。cluster のスクリプトでは `$.subNode("ScoreText").setText(...)` で子オブジェクトのText Viewを更新します。

1. 管理用アイテムを作ります。この親だけに `Item` コンポーネントを付けます。
2. 管理用アイテムに `Item` と `Scriptable Item` を追加し、`Scriptable Item` に `BowlingPinManager.js` を設定します。
3. 管理用アイテムの子として、Unity の `Create Empty` などで通常の GameObject を作ります。ここでは cluster の `Item` コンポーネントを追加しません。
4. 子GameObjectの名前を `ScoreText` にします。名前が違うとスクリプトから見つけられません。
5. `ScoreText` に `Text View` を追加します。
6. `ScoreText` がプレイヤーから見える位置と向きになるよう、ローカル位置と回転を調整します。
7. 管理用アイテムをピン群の近くに配置します。ピンが `PIN_SEARCH_RADIUS` 内に入っている必要があります。
8. ワールドを再生すると、スクリプトが `ScoreText` の Text View の文字列を更新します。

表示内容は `BowlingPinManager.js` の `DESPAWNED_COUNT_TEXT_FORMAT` で変更できます。初期値は `場外ピン: {0} / {1}` で、`{0}` が場外ピン数、`{1}` が管理用アイテムから見つかったピン総数です。文字サイズは `SCORE_TEXT_SIZE` で変更できます。

`ScoreText` がない場合でも、場外ピン数はログに出ます。この場合、スクリプトは最初に1回だけ「ScoreText の Text View を更新できません」とログを出します。

### 何も描画されない場合

まず Text View 自体が見える状態かを確認します。

1. `ScoreText` の `Text View` コンポーネントの `Text` に `TEST` と直接入力します。
2. Unity上またはclusterのプレビューで `TEST` が見えるか確認します。
3. 見えない場合は、スクリプトではなく Text View の位置、回転、Scale、Size、Color、または Plane への埋まり込みが原因です。
4. `ScoreText` を Plane の前に少し出し、Scale を `(1, 1, 1)` に戻し、Text View の Size を `1` 前後、Color を白や黒など背景と違う色にします。
5. `TEST` が見えるようになってから、`BowlingPinManager.js` で更新されるか確認します。

スクリプトが `ScoreText` を更新できているかは、clusterのスクリプトログで確認できます。`BowlingPinManager.js started. ScoreText を初期化します。` と `ScoreText Text View 更新: 場外ピン: 0 / 0` が出ていれば、スクリプトはText View更新まで到達しています。

`ScoreText の Text View を更新できません` が出る場合は、子GameObject名が完全に `ScoreText` になっているか、`ScoreText` が管理用アイテムの子階層に入っているか、`ScoreText` に `Text View` が付いているかを確認してください。

### Plane をスコアボード背景にする場合

Plane など Renderer を持つオブジェクトには Text View を追加できません。スコアボードにしたい場合は、背景と文字を別オブジェクトに分けます。

1. Plane を作り、スコアボードの背景として配置します。
2. Plane とは別に管理用アイテムを作り、親にだけ `Item` と `Scriptable Item` を追加して、`Scriptable Item` に `BowlingPinManager.js` を設定します。
3. 管理用アイテムの子に通常の GameObject として `ScoreText` を作り、`Item` は付けずに `Text View` だけを追加します。
4. `ScoreText` を Plane の少し手前に配置して、Text View が Plane に埋まらないようにします。
5. 管理用アイテムがピン群の近くにない場合は、`BowlingPinManager.js` の `PIN_SEARCH_RADIUS` を広げます。

この構成では、Plane は見た目だけを担当し、管理用アイテムが場外ピン数の集計、子オブジェクト `ScoreText` が表示を担当します。

## ワールド内での使い方

### ボールを発射する

発射用アイテムを掴める設定にしている場合は、発射用アイテムを掴んで「使う」を押すとボールを1発発射します。1回撃つと弾数がなくなるため、もう一度撃つには発射用アイテムを持ち直してください。

発射用アイテムを掴めない配置物にしている場合は、発射用アイテムを左クリック/タップ/VRトリガーで「使う」と、そのたびにボールを発射します。

生成されたボールは `ball.js` により、10秒後に自動で削除されます。ボール自体を掴んで「使う」を押し、そのあと手放すと、プレイヤーの向いている方向へボールを飛ばせます。

### ピンを場外に出して復帰する

ピンが `BowlingPin.js` の `DESPAWN_HEIGHT` 以下に落ちると、場外ピンとしてカウントされます。管理用アイテムに Text View が付いている場合は、「場外ピン: 本数 / 総数」と表示されます。`HIDE_AT_DESPAWN_HEIGHT` が `true` の場合、場外に出たピンは見えなくなります。

管理用アイテムを左クリック/タップ/VRトリガーで「使う」と、`PIN_SEARCH_RADIUS` 内のピンへ復帰メッセージを送り、各ピンを記録済みの初期位置へ戻します。

### 復帰位置を記録し直す

ピンを並べ直したあと、その配置を新しい復帰位置にしたい場合は、`BowlingPinManager.js` の `CAPTURE_POSE_ON_GRAB` を `true` に変更してから管理用アイテムを掴んでください。周囲のピンが現在位置と現在回転を復帰位置として記録します。

記録し直したあとは、誤って復帰位置を上書きしないように `CAPTURE_POSE_ON_GRAB` を `false` に戻すことをおすすめします。

### 自動復帰を使う

全ピンが場外に出たあと自動で復帰させたい場合は、`BowlingPinManager.js` の `AUTO_RESPAWN_DELAY_SECONDS` を 0 以上にします。例えば `3` にすると、`EXPECTED_PIN_COUNT` 本のピンが場外に出てから3秒後に復帰します。

ピンの本数が10本ではない場合は、`EXPECTED_PIN_COUNT` を実際の本数に合わせてください。
