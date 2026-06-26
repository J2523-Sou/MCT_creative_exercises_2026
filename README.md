# ボウリングプログラム概要

Unity/cluster 上でボールを発射し、ピンを倒して場外数を数え、必要に応じてピンを初期位置へ戻すためのボウリング用プログラムです。

## できること

- 発射用アイテムからボールを生成して前方へ飛ばす
- 生成したボールを一定時間後に自動削除する
- ボールを掴んで「使う」操作をしたあと、手放したタイミングでボールを飛ばす
- ピンが一定の高さ以下に落ちたら「場外ピン」としてカウントする
- 場外ピン数を画面または Text View に表示する
- 管理用アイテムの操作でピンをまとめて初期位置へ戻す
- 必要に応じて現在のピン配置を復帰位置として記録し直す
- 全ピン場外後の自動復帰を設定できる

## 主なファイル

### Unity C# 版

- `throw.cs`
  - マウス左クリックでボールを生成し、前方へ力を加える発射用スクリプトです。
  - `R` キーで弾数を再装填します。
  - 生成したボールは 10 秒後に削除されます。
- `ball.cs`
  - ボールに力を加える動作確認用スクリプトです。
  - Space キーでボール自身の前方へ力を加えます。
- `BowlingPin.cs`
  - 各ピンに付けるスクリプトです。
  - 初期位置と回転を記録し、場外高さまで落ちたら管理側へ通知します。
  - 復帰時は位置、回転、速度、角速度をリセットします。
- `BowlingPinManager.cs`
  - ピン全体を管理するスクリプトです。
  - 場外ピン数を集計し、画面表示と一括復帰を行います。

### cluster JavaScript 版

- `throw.js`
  - cluster Creator Kit の Scriptable Item に設定する発射用スクリプトです。
  - 掴んで「使う」、または配置物を左クリック/タップ/VRトリガーで「使う」とボールを発射します。
- `ball.js`
  - ボール用 Scriptable Item です。
  - 生成後 10 秒で削除し、掴んで「使う」操作をしたあと手放すとボールを飛ばします。
- `BowlingPin.js`
  - 各ピン用 Scriptable Item です。
  - 場外判定、非表示化、復帰位置の記録、復帰処理を担当します。
- `BowlingPinManager.js`
  - ピン管理用 Scriptable Item です。
  - 周囲のピンへメッセージを送り、場外数の集計、Text View 更新、一括復帰、自動復帰を行います。

## 基本的な構成

1. ボール用の Prefab または World Item Template を用意します。
2. 発射用アイテムに `throw.cs` または `throw.js` を設定します。
3. ボールに Rigidbody/Movable Item、Collider、ボール用スクリプトを設定します。
4. 各ピンに Collider、物理挙動、ピン用スクリプトを設定します。
5. ピンの近くに管理用アイテムを置き、`BowlingPinManager.cs` または `BowlingPinManager.js` を設定します。
6. cluster 版で場外数を表示する場合は、管理用アイテムの子に `ScoreText` という名前の GameObject を作り、Text View を追加します。

## cluster 版の注意点

- `throw.js` の `BALL_TEMPLATE_ID` は、Creator Kit の World Item Template List に登録したボールの ID と一致させます。
- `Use Item Trigger` を付けると Scriptable Item の `$.onUse` が呼ばれない場合があるため、発射用アイテムやボールには付けない想定です。
- ピン管理は近くのアイテムへメッセージを送って行うため、`PIN_SEARCH_RADIUS` の範囲内にピンを配置します。
- Text View は Renderer を持つ Plane などには直接付けず、管理用アイテムの子オブジェクト `ScoreText` に付けます。

## 調整しやすい値

- ボール発射速度: `SHOT_SPEED` / `shotSpeed`
- 発射位置の前方オフセット: `MUZZLE_OFFSET`
- ボールの寿命: `LIFE_TIME_SECONDS`
- ピンを場外扱いにする高さ: `DESPAWN_HEIGHT` / `despawnHeight`
- ピン探索半径: `PIN_SEARCH_RADIUS`
- 自動復帰までの秒数: `AUTO_RESPAWN_DELAY_SECONDS`
- 想定ピン数: `EXPECTED_PIN_COUNT`

## ワールド内での使い方

発射用アイテムを使うとボールが前方へ発射されます。ボールがピンに当たり、ピンが場外判定高さより下に落ちると場外ピンとして数えられます。管理用アイテムを使うと、ピンは記録済みの初期位置へ戻ります。

cluster 版では、管理用アイテムの子に `ScoreText` を用意しておくと、`場外ピン: 現在数 / 総数` の形式で状況を表示できます。
