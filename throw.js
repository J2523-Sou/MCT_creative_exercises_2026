// cluster Creator Kit の Scriptable Item に設定する発射用スクリプトです。
// 元の throw.cs の「クリックで1発発射、Rキーで装填」を、
// cluster では「使う(onUse)で発射、インタラクト(onInteract)で装填」に置き換えています。

// World Item Template List に登録したボールのIDに合わせて変更してください。
const BALL_TEMPLATE_ID = "ball";

// ボールに与える撃力です。Unity版の shotSpeed に相当します。
const SHOT_SPEED = 50;

// 発射位置を、このアイテムの正面へ少しずらします。
// 0のままだと発射元アイテムやプレイヤーに接触しやすい場合があります。
const MUZZLE_OFFSET = 0.5;

// 1回の装填で撃てる弾数です。Unity版は R キーで shotCount = 1 に戻していました。
const MAX_SHOT_COUNT = 1;

$.onStart(() => {
  // cluster のコールバック間では通常の変数が保持されない前提なので $.state を使います。
  $.state.shotCount = MAX_SHOT_COUNT;
});

$.onUse((isDown, player) => {
  // 「使う」ボタンを押した瞬間だけ処理します。離した瞬間(isDown=false)では何もしません。
  if (!isDown) return;

  const shotCount = $.state.shotCount === undefined ? MAX_SHOT_COUNT : $.state.shotCount;
  if (shotCount <= 0) {
    $.log("弾がありません。インタラクトで再装填してください。");
    return;
  }

  $.state.shotCount = shotCount - 1;

  // アイテムのローカルZ軸正方向を、ワールド座標の発射方向に変換します。
  const rotation = $.getRotation();
  const forward = new Vector3(0, 0, 1).applyQuaternion(rotation).normalize();

  // 発射元と重ならないよう、少し前方に生成します。
  const spawnPosition = $.getPosition().add(forward.clone().multiplyScalar(MUZZLE_OFFSET));

  // cluster では生成対象を WorldItemTemplateId で指定します。
  // Creator Kit の World Item Template List に BALL_TEMPLATE_ID と同じIDを登録してください。
  const ballTemplateId = new WorldItemTemplateId(BALL_TEMPLATE_ID);
  const ball = $.createItem(ballTemplateId, spawnPosition, rotation);

  // 生成したボールに、Unity版の Rigidbody.AddForce 相当の撃力を加えます。
  // ボール側には Movable Item と物理挙動する設定が必要です。
  ball.addImpulsiveForce(forward.multiplyScalar(SHOT_SPEED));

  $.log("発射しました。");
});

$.onInteract(player => {
  // Unity版の R キー相当です。発射用アイテムをインタラクトすると1発再装填します。
  $.state.shotCount = MAX_SHOT_COUNT;
  $.log("再装填しました。");
});
