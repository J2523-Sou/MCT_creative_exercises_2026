// cluster Creator Kit の Scriptable Item に設定するボウリングピン用スクリプトです。
// 元の BowlingPin.cs の「落下高さでカウント」「初期位置へ復帰」を cluster JS 向けに置き換えています。

// この高さ以下に落ちたら場外ピンとして扱います。
const DESPAWN_HEIGHT = -2.0;

// 落下後に見えなくする場合は true にします。物理判定までは消せないため、ピンは落下地点に残ります。
const HIDE_AT_DESPAWN_HEIGHT = true;

// Manager との通信用メッセージ名です。BowlingPinManager.js 側と同じ値にしてください。
const MESSAGE_PIN_RESET = "bowling-pin-reset";
const MESSAGE_PIN_REPORT_REQUEST = "bowling-pin-report-request";
const MESSAGE_PIN_REPORT = "bowling-pin-report";
const MESSAGE_PIN_CAPTURE_POSE = "bowling-pin-capture-pose";

$.onStart(() => {
  captureRespawnPose();
  $.state.countedAsDespawned = false;
  $.state.hidden = false;
});

function captureRespawnPose() {
  $.state.respawnPosition = $.getPosition();
  $.state.respawnRotation = $.getRotation();
}

function stopPhysics() {
  try {
    $.velocity = new Vector3(0, 0, 0);
    $.angularVelocity = new Vector3(0, 0, 0);
  } catch (e) {
    $.log("ピンの速度をリセットできませんでした: " + e);
  }
}

function setHidden(hidden) {
  if ($.state.hidden === hidden) return;

  $.state.hidden = hidden;

  try {
    if (hidden) {
      $.setVisiblePlayers([]);
    } else {
      $.clearVisiblePlayers();
    }
  } catch (e) {
    $.log("ピンの表示状態を変更できませんでした: " + e);
  }
}

function respawn() {
  const position = $.state.respawnPosition;
  const rotation = $.state.respawnRotation;

  if (position === undefined || rotation === undefined) {
    captureRespawnPose();
  }

  $.state.countedAsDespawned = false;
  setHidden(false);

  try {
    $.setPosition($.state.respawnPosition);
    $.setRotation($.state.respawnRotation);
    stopPhysics();
  } catch (e) {
    $.log("ピンを復帰できませんでした: " + e);
  }
}

function reportToManager(manager, cycle) {
  if (manager === null || manager === undefined || manager.type !== "item") return;

  manager.send(MESSAGE_PIN_REPORT, {
    id: $.id,
    cycle: cycle,
    despawned: $.state.countedAsDespawned === true,
  });
}

$.onUpdate(() => {
  if ($.state.countedAsDespawned === true) return;

  const position = $.getPosition();
  if (position.y > DESPAWN_HEIGHT) return;

  $.state.countedAsDespawned = true;
  $.log("ピンが場外高さに到達しました。");

  if (HIDE_AT_DESPAWN_HEIGHT) {
    setHidden(true);
  }
});

$.onReceive((messageType, arg, sender) => {
  switch (messageType) {
    case MESSAGE_PIN_RESET:
      respawn();
      break;

    case MESSAGE_PIN_CAPTURE_POSE:
      captureRespawnPose();
      $.log("ピンの復帰位置を記録しました。");
      break;

    case MESSAGE_PIN_REPORT_REQUEST:
      reportToManager(sender, arg);
      break;
  }
});

$.onInteract(() => {
  respawn();
});
