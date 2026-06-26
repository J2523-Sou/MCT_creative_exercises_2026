# ボウリングプログラム

cluster Creator Kit の Scriptable Item で動かすボウリング用スクリプト一式です。ボールの発射、ボールの自動削除、ピンの場外判定、場外ピン数の表示、ピンの一括復帰を扱います。

## ファイル構成

- `throw.js`
  - 発射用アイテムに設定します。
  - 掴んで「使う」、または配置物を左クリック/タップ/VRトリガーで「使う」と、ボールを生成して前方へ飛ばします。
- `ball.js`
  - ボールの World Item Template に設定します。
  - 生成から一定時間後に自動削除します。
  - 掴んで「使う」操作をしたあと、手放すと前方へ飛びます。
- `BowlingPin.js`
  - 各ピンに設定します。
  - 初期位置と回転を記録し、一定の高さ以下に落ちたら場外ピンとして扱います。
  - 管理用アイテムからのメッセージで初期位置へ復帰します。
- `BowlingPinManager.js`
  - ピン群の近くに置く管理用アイテムに設定します。
  - 周囲のピンへ問い合わせ、場外ピン数の集計、Text View 更新、一括復帰、自動復帰を行います。

各 `.js.meta` は Unity の管理用ファイルなので、対応する `.js` とセットで残します。

## Creator Kit 側の設定

1. ボール用の World Item Template を作り、ID を `ball` にします。
2. ボールに `Scriptable Item` を追加し、`ball.js` を設定します。
3. ボールに `Grabbable Item`、Collider、物理挙動する `Movable Item` を追加します。
4. 発射用アイテムに `Scriptable Item` を追加し、`throw.js` を設定します。
5. 掴んで撃つ場合は、発射用アイテムに `Grabbable Item` と Collider を追加します。
6. 配置したまま撃つ場合は、発射用アイテムを掴めない状態にして Collider を追加します。
7. 各ピンに `Scriptable Item` を追加し、`BowlingPin.js` を設定します。
8. 各ピンに Collider と物理挙動する `Movable Item` を追加します。
9. ピン群の近くに管理用アイテムを置き、`Scriptable Item` に `BowlingPinManager.js` を設定します。
10. 場外ピン数を表示する場合は、管理用アイテムの子に `ScoreText` という名前の GameObject を作り、Text View を追加します。

`Use Item Trigger` を追加すると Scriptable Item の `$.onUse` が呼ばれない場合があります。発射用アイテムやボールでは、基本的に `Use Item Trigger` を付けない構成を想定しています。

## 調整する主な値

- `throw.js`
  - `BALL_TEMPLATE_ID`: ボールの World Item Template ID
  - `SHOT_SPEED`: ボール発射時の撃力
  - `MUZZLE_OFFSET`: 発射位置の前方オフセット
  - `MAX_SHOT_COUNT`: 掴んで撃つ場合の装填数
- `ball.js`
  - `LIFE_TIME_SECONDS`: 生成されたボールを削除するまでの秒数
  - `USE_IMPULSE`: ボールを掴んで使った時の撃力
  - `LAUNCH_SPEED`: 手放した後に設定する初速
- `BowlingPin.js`
  - `DESPAWN_HEIGHT`: ピンを場外扱いにする高さ
  - `HIDE_AT_DESPAWN_HEIGHT`: 場外後にピンを非表示にするか
- `BowlingPinManager.js`
  - `PIN_SEARCH_RADIUS`: 管理用アイテムがピンを探す半径
  - `REPORT_INTERVAL_SECONDS`: 場外数を集計する間隔
  - `EXPECTED_PIN_COUNT`: 自動復帰判定に使う想定ピン数
  - `AUTO_RESPAWN_DELAY_SECONDS`: 全ピン場外後に自動復帰するまでの秒数

## 使い方

発射用アイテムを使うとボールが前方へ発射されます。ボールがピンに当たり、ピンが `DESPAWN_HEIGHT` 以下に落ちると場外ピンとしてカウントされます。

管理用アイテムを左クリック/タップ/VRトリガーで「使う」と、周囲のピンへ復帰メッセージを送り、ピンを記録済みの初期位置へ戻します。

`ScoreText` を設定している場合は、`場外ピン: 現在数 / 総数` の形式で場外数が表示されます。表示されない場合は、`ScoreText` が管理用アイテムの子になっていること、名前が完全に `ScoreText` であること、Text View が付いていることを確認してください。
