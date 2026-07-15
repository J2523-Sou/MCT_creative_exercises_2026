// cluster専用の景品物理Adapterです。共通C#のPrize/Respawnerから分離しています。
const MESSAGE_COMMAND = "mct-crane-command";
const MESSAGE_ROUTE = "mct-crane-prize-route";
const MINIMUM_Y = -1.5;
const MAXIMUM_LIFETIME = 90;
const RESPAWN_DELAY = 1.2;
const SLIP_CHANCE_PER_SECOND = 0.04;
const MAXIMUM_HORIZONTAL_DISTANCE = 2.8;

function copyVector(value) {
  return new Vector3(value.x, value.y, value.z);
}

function moveTowards(current, target, maxDelta) {
  const difference = target.clone().sub(current);
  const distance = difference.length();
  if (distance <= maxDelta || distance <= 0.0001) return target.clone();
  return current.clone().add(difference.multiplyScalar(maxDelta / distance));
}

function respawn() {
  $.setPosition(copyVector($.state.respawnPosition));
  $.setRotation($.state.respawnRotation);
  $.velocity = new Vector3(0, 0, 0);
  $.angularVelocity = new Vector3(0, 0, 0);
  $.state.phase = "free";
  $.state.age = 0;
  $.state.releaseTimer = -1;
  $.log("cluster prize respawned");
}

function releasePrize(acquired) {
  $.state.phase = "free";
  $.velocity = new Vector3(0, -0.05, 0);
  $.state.releaseTimer = acquired ? RESPAWN_DELAY : -1;
}

$.onStart(() => {
  $.state.respawnPosition = copyVector($.getPosition());
  $.state.respawnRotation = $.getRotation();
  $.state.phase = "free";
  $.state.age = 0;
  $.state.releaseTimer = -1;
});

$.onReceive((messageType, arg) => {
  if (messageType === MESSAGE_COMMAND && arg === "reset") {
    respawn();
    return;
  }
  if (messageType !== MESSAGE_ROUTE || $.state.phase !== "free") return;
  if (arg === null || typeof arg !== "object") return;
  if (arg.capture === undefined || arg.lifted === undefined || arg.drop === undefined) return;

  $.state.capture = copyVector(arg.capture);
  $.state.lifted = copyVector(arg.lifted);
  $.state.drop = copyVector(arg.drop);
  $.state.liftDuration = Math.max(0.1, arg.liftDuration || 1.7);
  $.state.travelDuration = Math.max(0.1, arg.travelDuration || 1.0);
  $.state.releaseDelayDuration = Math.max(0.1, arg.releaseDelay || 0.65);
  $.state.timer = 0;
  // The crane has already confirmed contact on both claw-tip sensors. Begin
  // lifting immediately so the prize and arm use the same movement timeline.
  $.state.phase = "lifting";
  $.velocity = new Vector3(0, 0, 0);
  $.angularVelocity = new Vector3(0, 0, 0);
  $.log("cluster prize captured");
});

$.onUpdate(deltaTime => {
  const phase = $.state.phase;
  if (phase === "free") {
    $.state.age = ($.state.age || 0) + deltaTime;
    if ($.state.releaseTimer >= 0) {
      $.state.releaseTimer -= deltaTime;
      if ($.state.releaseTimer <= 0) {
        respawn();
        return;
      }
    }
    const position = $.getPosition();
    const horizontal = new Vector3(
      position.x - $.state.respawnPosition.x,
      0,
      position.z - $.state.respawnPosition.z
    ).length();
    if (position.y < MINIMUM_Y || horizontal > MAXIMUM_HORIZONTAL_DISTANCE ||
        $.state.age >= MAXIMUM_LIFETIME) respawn();
    return;
  }

  if (Math.random() < SLIP_CHANCE_PER_SECOND * deltaTime) {
    $.log("cluster prize slipped");
    releasePrize(false);
    return;
  }

  $.state.timer = ($.state.timer || 0) + deltaTime;
  if (phase === "lifting") {
    const speed = $.state.capture.clone().sub($.state.lifted).length() / $.state.liftDuration;
    const next = moveTowards(copyVector($.getPosition()), $.state.lifted, speed * deltaTime);
    $.setPosition(next);
    if (next.clone().sub($.state.lifted).length() < 0.001) {
      $.state.phase = "travel";
      $.state.timer = 0;
    }
    return;
  }

  if (phase === "travel") {
    const speed = $.state.lifted.clone().sub($.state.drop).length() / $.state.travelDuration;
    const next = moveTowards(copyVector($.getPosition()), $.state.drop, speed * deltaTime);
    $.setPosition(next);
    if (next.clone().sub($.state.drop).length() < 0.001) {
      $.state.phase = "releaseWait";
      $.state.timer = 0;
    }
    return;
  }

  if (phase === "releaseWait" && $.state.timer >= $.state.releaseDelayDuration) {
    $.log("cluster prize released at drop");
    releasePrize(true);
  }
});
