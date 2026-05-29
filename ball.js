// cluster Creator Kit の Scriptable Item に設定するボール用スクリプトです。
// 元の ball.cs の「Spaceで前方に力を加える」と、throw.cs の「10秒後に削除」をまとめています。

// 生成されたボールを自動で消すまでの秒数です。
const LIFE_TIME_SECONDS = 10;

// 手に持って「使う」動作をしたときに加える撃力です。
// 元の ball.cs の AddForce(... * 50, ForceMode.Impulse) に相当します。
const USE_IMPULSE = 50;

$.onStart(() => {
  // 経過時間は $.state に保存します。
  $.state.age = 0;
  $.state.destroyRequested = false;
});

$.onUpdate(deltaTime => {
  if ($.state.destroyRequested) return;

  const currentAge = $.state.age === undefined ? 0 : $.state.age;
  const age = currentAge + deltaTime;
  $.state.age = age;

  if (age < LIFE_TIME_SECONDS) return;

  $.state.destroyRequested = true;

  // createItem で生成されたアイテム、またはクラフトアイテムなら削除できます。
  // ワールドに直接配置した通常アイテムでは削除できないため、失敗時はログだけ出します。
  try {
    $.destroy();
  } catch (e) {
    $.log("このボールは削除できません: " + e);
  }
});

$.onUse((isDown, player) => {
  // cluster では Space キー入力を直接拾わず、掴んだアイテムの「使う」動作で代用します。
  if (!isDown) return;

  const forward = new Vector3(0, 0, 1).applyQuaternion($.getRotation()).normalize();

  // このアイテム自身に撃力を加えます。
  // Movable Item かつ物理挙動する設定でない場合、この力は反映されません。
  $.addImpulsiveForce(forward.multiplyScalar(USE_IMPULSE));
});
