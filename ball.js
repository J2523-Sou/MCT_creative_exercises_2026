// cluster Creator Kit の Scriptable Item に設定するボール用スクリプトです。
// 元の ball.cs の「Spaceで前方に力を加える」と、throw.cs の「10秒後に削除」をまとめています。

// 生成されたボールを自動で消すまでの秒数です。
const LIFE_TIME_SECONDS = 10;

// 手に持って「使う」動作をしたときに加える撃力です。
// 元の ball.cs の AddForce(... * 50, ForceMode.Impulse) に相当します。
const USE_IMPULSE = 15;

// 手放したあとに設定する初速です。撃力が効かない設定でも動作確認しやすくするため併用します。
const LAUNCH_SPEED = 3;

// Grab解除直後は物理操作が無視されることがあるため、少し待ってから発射します。
const LAUNCH_DELAY_SECONDS = 0.1;

// 発射方向に少し上向きを足します。0にするとプレイヤーの正面方向そのままになります。
const LAUNCH_UPWARD_BIAS = -0.15;

$.onStart(() => {
  // 経過時間は $.state に保存します。
  $.state.age = 0;
  $.state.destroyRequested = false;
  $.state.isGrabbed = false;
  $.state.pendingUseImpulse = false;
  $.state.launchDelay = -1;
  $.log("ball.js started");
});

function getItemForward() {
  return new Vector3(0, 0, 1).applyQuaternion($.getRotation()).normalize();
}

function getPlayerForward(player) {
  if (player === null || player === undefined) return null;

  const rotation = player.getRotation();
  if (rotation === null) return null;

  const forward = new Vector3(0, 0, 1).applyQuaternion(rotation);
  forward.y += LAUNCH_UPWARD_BIAS;
  return forward.normalize();
}

function getLaunchDirection(player) {
  const playerForward = getPlayerForward(player);
  return playerForward === null ? getItemForward() : playerForward;
}

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

$.onPhysicsUpdate(deltaTime => {
  if ($.state.launchDelay === undefined || $.state.launchDelay < 0) return;

  const launchDelay = $.state.launchDelay - deltaTime;
  $.state.launchDelay = launchDelay;
  if (launchDelay > 0) return;

  $.state.launchDelay = -1;

  const direction = $.state.impulseDirection === undefined
    ? getItemForward()
    : $.state.impulseDirection;

  try {
    $.velocity = direction.clone().multiplyScalar(LAUNCH_SPEED);
    $.addImpulsiveForce(direction.clone().multiplyScalar(USE_IMPULSE));
    $.log("ボールを飛ばしました。");
  } catch (e) {
    $.log("ボールを飛ばせませんでした: " + e);
  }
});

$.onUse((isDown, player) => {
  // cluster では Space キー入力を直接拾わず、掴んだアイテムの「使う」動作で代用します。
  $.log(isDown ? "使う down" : "使う up");
  if (!isDown) return;

  const forward = getLaunchDirection(player);

  // Grab中は物理操作が見た目に反映されないことがあるため、手放した瞬間に撃力を加えます。
  $.state.pendingUseImpulse = true;
  $.state.impulseDirection = forward;
  $.log("使う入力を受け取りました。手放すとボールを飛ばします。");
});

$.onGrab((isGrab, isLeftHand, player) => {
  $.state.isGrabbed = isGrab;
  $.log((isGrab ? "grabbed: " : "released: ") + (isLeftHand ? "left" : "right"));

  if (isGrab) return;

  if (!$.state.pendingUseImpulse) return;

  const direction = getLaunchDirection(player);

  $.state.pendingUseImpulse = false;
  $.state.impulseDirection = direction;
  $.state.launchDelay = LAUNCH_DELAY_SECONDS;
  $.log("手放しました。少し待ってからボールを飛ばします。");
});
