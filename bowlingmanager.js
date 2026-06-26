// cluster Creator Kit の Scriptable Item に設定するボウリングピン管理用スクリプトです。
// 周囲の BowlingPin.js にメッセージを送り、場外ピン数の集計と一括復帰を行います。

// Manager からこの半径内にあるアイテムへ問い合わせます。
const PIN_SEARCH_RADIUS = 200;

// 場外ピン数を集計する間隔です。ピン10本なら 1.5 秒程度で送信制限に余裕があります。
const REPORT_INTERVAL_SECONDS = 1.5;

// Text View が付いた子オブジェクトの名前です。
const SCORE_TEXT_SUB_NODE_NAME = "ScoreText";

// ScoreText には、この表示形式で場外ピン数を表示します。
const DESPAWNED_COUNT_TEXT_FORMAT = "場外ピン: {0} / {1}";

// Text View の文字サイズです。Text View 側の Scale と掛け合わせて実際の大きさが決まります。
const SCORE_TEXT_SIZE = 1.0;

// true にすると、Text View 更新の成否をログに出します。表示されない時の切り分け用です。
const DEBUG_SCORE_TEXT = true;

// 期待するピン数です。0 の場合は自動復帰判定には使いません。
const EXPECTED_PIN_COUNT = 10;

// 全ピン場外後に自動復帰するまでの秒数です。負数なら自動復帰しません。
const AUTO_RESPAWN_DELAY_SECONDS = -1;

// true にすると、Manager を掴んだタイミングで現在のピン配置を復帰位置として記録し直します。
const CAPTURE_POSE_ON_GRAB = false;

// BowlingPin.js 側と同じ値にしてください。
const MESSAGE_PIN_RESET = "bowling-pin-reset";a
const MESSAGE_PIN_REPORT_REQUEST = "bowling-pin-report-request";
const MESSAGE_PIN_REPORT = "bowling-pin-report";
const MESSAGE_PIN_CAPTURE_POSE = "bowling-pin-capture-pose";

$.onStart(() => {
  $.state.reportTimer = 0;
  $.state.reportCycle = 0;
  $.state.pinReports = {};
  $.state.despawnedPinCount = 0;
  $.state.reportedPinCount = 0;
  $.state.scheduledRespawn = -1;

  if (DEBUG_SCORE_TEXT) {
    $.log("BowlingPinManager.js started. ScoreText を初期化します。");
  }

  updateDespawnedCountText(0, 0);
});

function getNearbyItems() {
  return $.getItemsNear($.getPosition(), PIN_SEARCH_RADIUS);
}

function sendToNearbyPins(messageType, arg) {
  const items = getNearbyItems();
  let sentCount = 0;

  for (const item of items) {
    if (item.id === $.id) continue;

    try {
      item.send(messageType, arg);
      sentCount += 1;
    } catch (e) {
      $.log("ピンへの送信に失敗しました: " + e);
    }
  }

  return sentCount;
}

function requestReports() {
  const cycle = ($.state.reportCycle === undefined ? 0 : $.state.reportCycle) + 1;
  $.state.reportCycle = cycle;
  $.state.pinReports = {};

  sendToNearbyPins(MESSAGE_PIN_REPORT_REQUEST, cycle);
}

function formatDespawnedCountText(count, total) {
  return DESPAWNED_COUNT_TEXT_FORMAT
    .replace("{0}", String(count))
    .replace("{1}", String(total));
}

function updateDespawnedCountText(count, total) {
  const text = formatDespawnedCountText(count, total);

  try {
    const scoreText = $.subNode(SCORE_TEXT_SUB_NODE_NAME);

    scoreText.setEnabled(true);
    scoreText.setText(text);
    scoreText.setTextSize(SCORE_TEXT_SIZE);
    scoreText.setTextColor(1, 1, 1, 1);

    if (DEBUG_SCORE_TEXT) {
      $.log(SCORE_TEXT_SUB_NODE_NAME + " Text View 更新: " + text);
    }
  } catch (e) {
    if ($.state.setTextUnavailableLogged === true) return;

    $.state.setTextUnavailableLogged = true;
    $.log(SCORE_TEXT_SUB_NODE_NAME + " の Text View を更新できません: " + e + " / 表示予定: " + text);
  }
}

function getReportValue(report, key) {
  if (report === true) return key === "despawned";
  if (report === null || typeof report !== "object") return false;

  return report[key] === true;
}

function updatePinCounts() {
  const reports = $.state.pinReports === undefined ? {} : $.state.pinReports;
  let despawnedCount = 0;
  let total = 0;

  for (const id in reports) {
    if (!Object.prototype.hasOwnProperty.call(reports, id)) continue;

    total += 1;

    const despawned = getReportValue(reports[id], "despawned");

    if (despawned) {
      despawnedCount += 1;
    }
  }

  const previousCount = $.state.despawnedPinCount === undefined ? 0 : $.state.despawnedPinCount;
  const previousTotal = $.state.reportedPinCount === undefined ? 0 : $.state.reportedPinCount;
  $.state.despawnedPinCount = despawnedCount;
  $.state.reportedPinCount = total;

  if (despawnedCount !== previousCount || total !== previousTotal) {
    $.log("場外ピン数: " + despawnedCount + " / " + total);
    updateDespawnedCountText(despawnedCount, total);
  }

  if (
    AUTO_RESPAWN_DELAY_SECONDS >= 0 &&
    EXPECTED_PIN_COUNT > 0 &&
    despawnedCount >= EXPECTED_PIN_COUNT &&
    $.state.scheduledRespawn < 0
  ) {
    $.state.scheduledRespawn = AUTO_RESPAWN_DELAY_SECONDS;
    $.log("全ピン場外。復帰を予約しました。");
  }
}

function respawnAllPins() {
  $.state.pinReports = {};
  $.state.despawnedPinCount = 0;
  $.state.reportedPinCount = 0;
  $.state.scheduledRespawn = -1;
  updateDespawnedCountText(0, 0);

  const sentCount = sendToNearbyPins(MESSAGE_PIN_RESET, null);
  $.log("ピン復帰メッセージを送信しました: " + sentCount);
}

function captureAllPinRespawnPoses() {
  const sentCount = sendToNearbyPins(MESSAGE_PIN_CAPTURE_POSE, null);
  $.log("ピンの復帰位置記録メッセージを送信しました: " + sentCount);
}

$.onUpdate(deltaTime => {
  const reportTimer = ($.state.reportTimer === undefined ? 0 : $.state.reportTimer) + deltaTime;

  if (reportTimer >= REPORT_INTERVAL_SECONDS) {
    $.state.reportTimer = 0;
    requestReports();
  } else {
    $.state.reportTimer = reportTimer;
  }

  if ($.state.scheduledRespawn === undefined || $.state.scheduledRespawn < 0) return;

  const scheduledRespawn = $.state.scheduledRespawn - deltaTime;
  $.state.scheduledRespawn = scheduledRespawn;

  if (scheduledRespawn <= 0) {
    respawnAllPins();
  }
});

$.onReceive((messageType, arg) => {
  if (messageType !== MESSAGE_PIN_REPORT) return;
  if (arg === null || typeof arg !== "object") return;
  if (arg.cycle !== $.state.reportCycle) return;
  if (typeof arg.id !== "string") return;

  const reports = $.state.pinReports === undefined ? {} : $.state.pinReports;
  reports[arg.id] = {
    despawned: arg.despawned === true,
  };
  $.state.pinReports = reports;

  updatePinCounts();
});

$.onInteract(() => {
  respawnAllPins();
});

$.onUse(isDown => {
  if (!isDown) return;

  respawnAllPins();
});

$.onGrab(isGrab => {
  if (!isGrab || !CAPTURE_POSE_ON_GRAB) return;

  // Manager を持ち直した時点を、ピンの初期配置として記録し直します。
  captureAllPinRespawnPoses();
});
