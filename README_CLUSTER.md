# cluster 用 JavaScript 版

`throw.cs` と `ball.cs` を cluster Creator Kit の Scriptable Item 向け JavaScript に変換したものです。

## ファイル

- `throw.js`: 発射用アイテムに設定します。掴んで「使う」とボールを1発生成して前方へ飛ばし、インタラクトで再装填します。
- `ball.js`: ボールのテンプレートに設定します。生成から10秒後に自動削除し、掴んで「使う」と前方へ撃力を加えます。

## Creator Kit 側の設定

1. ボール用の World Item Template を作り、ID を `ball` にします。
2. そのボールに `Scriptable Item` を追加し、`ball.js` を設定します。
3. ボールに `Movable Item` と物理衝突用の設定を追加します。
4. 発射用アイテムに `Scriptable Item` を追加し、`throw.js` を設定します。
5. 発射用アイテムで `onUse` を使うため、必要に応じて `Grabbable Item` を追加します。
6. 再装填に `onInteract` を使うため、必要に応じてインタラクト可能な設定を追加します。

`throw.js` 内の `BALL_TEMPLATE_ID`、`SHOT_SPEED`、`MUZZLE_OFFSET`、`MAX_SHOT_COUNT` は用途に合わせて調整できます。
