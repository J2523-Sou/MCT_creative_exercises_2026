# cluster 用 JavaScript 版

`throw.cs` と `ball.cs` を cluster Creator Kit の Scriptable Item 向け JavaScript に変換したものです。

## ファイル

- `throw.js`: 発射用アイテムに設定します。掴んで「使う」、または掴めない配置アイテムを左クリックして「使う」と、ボールを生成して前方へ飛ばします。
- `ball.js`: ボールのテンプレートに設定します。生成から10秒後に自動削除し、掴んで「使う」を押したあと手放すと前方へ撃力を加えます。

## Creator Kit 側の設定

1. ボール用の World Item Template を作り、ID を `ball` にします。
2. そのボールに `Scriptable Item` を追加し、`ball.js` を設定します。
3. ボールに `Grabbable Item`、Collider、物理挙動する `Movable Item` を追加します。`Use Item Trigger` は追加しないでください。付いていると `ball.js` の `$.onUse` は呼ばれません。
4. 発射用アイテムに `Scriptable Item` を追加し、`throw.js` を設定します。
5. 掴んで撃つ場合は、発射用アイテムに `Grabbable Item` と Collider を追加します。`Use Item Trigger` は追加しないでください。付いていると `throw.js` の `$.onUse` は呼ばれません。
6. 配置したまま左クリックで撃つ場合は、発射用アイテムを掴めない状態にして Collider を追加します。この場合は `$.onInteract` で発射します。

`throw.js` 内の `BALL_TEMPLATE_ID`、`SHOT_SPEED`、`MUZZLE_OFFSET`、`MAX_SHOT_COUNT` は用途に合わせて調整できます。
