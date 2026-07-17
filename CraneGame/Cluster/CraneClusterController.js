// cluster専用の実行Adapterです。共通C#へcluster APIを混入させないため分離しています。
const MESSAGE_COMMAND = "mct-crane-command";
const CARRIAGE = "Carriage";
const LIFT = "LiftAssembly";
const LEFT_CLAW = "claw_armL";
const RIGHT_CLAW = "claw_armL.001";
const LEFT_SENSOR = "LeftGripSensor";
const RIGHT_SENSOR = "RightGripSensor";
const GRIP_ANCHOR = "GripAnchor";
const MESSAGE_PRIZE_ROUTE = "mct-crane-prize-route";

const MOVE_SPEED = 1.5;
const MIN_X = -1.7;
const MAX_X = 1.7;
const MIN_Z = -0.9;
const MAX_Z = 0.9;
const LOWER_DISTANCE = 3.58;
const LOWER_SPEED = 0.8;
const LIFT_SPEED = 0.9;
const TRAVEL_SPEED = 1.25;
const CLAW_DURATION = 0.65;
const CONTACT_SETTLE_DURATION = 0.15;
const RELEASE_WAIT = 0.45;
const DROP_X = 1.7;
const DROP_Z = -0.9;

function copyVector(value) {
  return new Vector3(value.x, value.y, value.z);
}

function moveTowards(current, target, maxDelta) {
  const difference = target.clone().sub(current);
  const distance = difference.length();
  if (distance <= maxDelta || distance <= 0.0001) return target.clone();
  return current.clone().add(difference.multiplyScalar(maxDelta / distance));
}

function setClawAmount(amount) {
  const left = $.subNode(LEFT_CLAW);
  const right = $.subNode(RIGHT_CLAW);
  // 上側ヒンジを基準にアーム全体を回す。親座標X軸に対して逆方向へ
  // 回すことで、子の先端リンクを含めた左右一式が対称に閉じる。
  const leftDelta = new Quaternion().setFromEulerAngles(new Vector3(-18 * amount, 0, 0));
  const rightDelta = new Quaternion().setFromEulerAngles(new Vector3(18 * amount, 0, 0));
  left.setRotation(leftDelta.multiply($.state.leftOpenRotation.clone()));
  right.setRotation(rightDelta.multiply($.state.rightOpenRotation.clone()));
}

$.onStart(() => {
  const carriage = $.subNode(CARRIAGE);
  const lift = $.subNode(LIFT);
  const left = $.subNode(LEFT_CLAW);
  const right = $.subNode(RIGHT_CLAW);
  $.state.home = copyVector(carriage.getPosition());
  $.state.lifted = copyVector(lift.getPosition());
  $.state.leftOpenRotation = left.getRotation().clone();
  $.state.rightOpenRotation = right.getRotation().clone();
  $.state.phase = "idle";
  $.state.clawTimer = 0;
  $.state.releaseTimer = 0;
  $.state.moveCommand = null;
  // The prefab already contains the authored open pose. Reapplying an item-local
  // rotation here can disturb the FBX hierarchy while its non-uniform scale settles.
  $.log("CraneClusterController ready");
});

function setMoveCommand(command, isDown) {
  if ($.state.phase !== "idle") return;
  if (!isDown) {
    if ($.state.moveCommand === command) $.state.moveCommand = null;
    return;
  }
  $.state.moveCommand = command;
}

function toggleMoveCommand(command) {
  if ($.state.phase !== "idle") return;
  $.state.moveCommand = $.state.moveCommand === command ? null : command;
}

function updateManualMove(deltaTime) {
  const command = $.state.moveCommand;
  if (command === null || command === undefined) return;
  const carriage = $.subNode(CARRIAGE);
  const position = copyVector(carriage.getPosition());
  if (command === "left") position.x -= MOVE_SPEED * deltaTime;
  if (command === "right") position.x += MOVE_SPEED * deltaTime;
  if (command === "forward") position.z += MOVE_SPEED * deltaTime;
  if (command === "back") position.z -= MOVE_SPEED * deltaTime;
  const beforeClampX = position.x;
  const beforeClampZ = position.z;
  position.x = Math.max(MIN_X, Math.min(MAX_X, position.x));
  position.z = Math.max(MIN_Z, Math.min(MAX_Z, position.z));
  carriage.setPosition(position);
  // Stop an outward command at a boundary. The next press, including the
  // opposite direction, always starts from a neutral state.
  if (position.x !== beforeClampX || position.z !== beforeClampZ) {
    $.state.moveCommand = null;
  }
}

function resetCrane() {
  $.subNode(CARRIAGE).setPosition(copyVector($.state.home));
  $.subNode(LIFT).setPosition(copyVector($.state.lifted));
  setClawAmount(0);
  $.state.clawTimer = 0;
  $.state.releaseTimer = 0;
  $.state.moveCommand = null;
  $.state.phase = "idle";
  $.log("crane reset to home");
}

function requestPrizeCarry() {
  const grip = $.subNode(GRIP_ANCHOR).getGlobalPosition();
  const carriagePosition = $.subNode(CARRIAGE).getPosition();
  const rootRotation = $.getRotation();
  const liftOffset = new Vector3(0, LOWER_DISTANCE, 0).applyQuaternion(rootRotation);
  const dropOffset = new Vector3(
    DROP_X - carriagePosition.x,
    0,
    DROP_Z - carriagePosition.z
  ).applyQuaternion(rootRotation);
  const lifted = grip.clone().add(liftOffset);
  const drop = lifted.clone().add(dropOffset);
  const travelDuration = Math.max(0.1, dropOffset.length() / TRAVEL_SPEED);

  // Only capture an Item reported by both physical claw-tip sensors.
  const leftContacts = {};
  const rightContacts = {};
  for (const overlap of $.getOverlaps()) {
    const item = overlap.handle;
    if (item === null || item.type !== "item" || item.id === $.id) continue;
    if (overlap.selfNode.name === LEFT_SENSOR) leftContacts[item.id] = item;
    if (overlap.selfNode.name === RIGHT_SENSOR) rightContacts[item.id] = item;
  }
  for (const id in leftContacts) {
    if (rightContacts[id] === undefined) continue;
    const item = leftContacts[id];
    try {
      item.send(MESSAGE_PRIZE_ROUTE, {
        capture: grip,
        lifted: lifted,
        drop: drop,
        liftDuration: LOWER_DISTANCE / LIFT_SPEED,
        travelDuration: travelDuration,
        releaseDelay: CLAW_DURATION,
      });
      return;
    } catch (e) {
      $.log("prize route send failed: " + e);
    }
  }
  $.log("cluster grip miss: no Item touching both claws");
}

$.onReceive((messageType, command) => {
  if (messageType !== MESSAGE_COMMAND) return;
  $.log("crane command received: " + command);
  if (command !== null && typeof command === "object") {
    if (typeof command.command === "string" && typeof command.isDown === "boolean") {
      setMoveCommand(command.command, command.isDown);
    }
    return;
  }
  if (typeof command !== "string") return;
  if (command === "stop") {
    $.state.moveCommand = null;
    return;
  }
  if (command === "reset") {
    resetCrane();
    return;
  }
  if (command === "grab") {
    if ($.state.phase === "idle") {
      $.state.moveCommand = null;
      $.state.phase = "lowering";
    }
    return;
  }
  toggleMoveCommand(command);
});

$.onUpdate(deltaTime => {
  const phase = $.state.phase;
  if (phase === "idle") {
    updateManualMove(deltaTime);
    return;
  }

  const carriage = $.subNode(CARRIAGE);
  const lift = $.subNode(LIFT);

  if (phase === "lowering") {
    const target = copyVector($.state.lifted).add(new Vector3(0, -LOWER_DISTANCE, 0));
    const next = moveTowards(copyVector(lift.getPosition()), target, LOWER_SPEED * deltaTime);
    lift.setPosition(next);
    if (next.clone().sub(target).length() < 0.001) {
      $.state.phase = "closing";
      $.state.clawTimer = 0;
    }
    return;
  }

  if (phase === "closing") {
    const timer = ($.state.clawTimer || 0) + deltaTime;
    $.state.clawTimer = timer;
    setClawAmount(Math.min(1, timer / CLAW_DURATION));
    if (timer >= CLAW_DURATION) {
      setClawAmount(1);
      $.state.phase = "contactSettle";
      $.state.contactTimer = 0;
    }
    return;
  }

  if (phase === "contactSettle") {
    $.state.contactTimer = ($.state.contactTimer || 0) + deltaTime;
    if ($.state.contactTimer >= CONTACT_SETTLE_DURATION) {
      requestPrizeCarry();
      $.state.phase = "lifting";
    }
    return;
  }

  if (phase === "lifting") {
    const target = copyVector($.state.lifted);
    const next = moveTowards(copyVector(lift.getPosition()), target, LIFT_SPEED * deltaTime);
    lift.setPosition(next);
    if (next.clone().sub(target).length() < 0.001) $.state.phase = "drop";
    return;
  }

  if (phase === "drop") {
    const current = copyVector(carriage.getPosition());
    const target = new Vector3(DROP_X, current.y, DROP_Z);
    const next = moveTowards(current, target, TRAVEL_SPEED * deltaTime);
    carriage.setPosition(next);
    if (next.clone().sub(target).length() < 0.001) {
      $.state.phase = "opening";
      $.state.clawTimer = 0;
    }
    return;
  }

  if (phase === "opening") {
    const timer = ($.state.clawTimer || 0) + deltaTime;
    $.state.clawTimer = timer;
    setClawAmount(1 - Math.min(1, timer / CLAW_DURATION));
    if (timer >= CLAW_DURATION) {
      // Interpolation error must not leave the arms between open and closed poses.
      setClawAmount(0);
      $.state.phase = "releaseWait";
      $.state.releaseTimer = 0;
    }
    return;
  }

  if (phase === "releaseWait") {
    $.state.releaseTimer = ($.state.releaseTimer || 0) + deltaTime;
    if ($.state.releaseTimer >= RELEASE_WAIT) {
      $.subNode(LIFT).setPosition(copyVector($.state.lifted));
      setClawAmount(0);
      $.state.phase = "home";
    }
    return;
  }

  if (phase === "home") {
    const current = copyVector(carriage.getPosition());
    const target = copyVector($.state.home);
    const next = moveTowards(current, target, TRAVEL_SPEED * deltaTime);
    carriage.setPosition(next);
    if (next.clone().sub(target).length() < 0.001) {
      carriage.setPosition(copyVector($.state.home));
      lift.setPosition(copyVector($.state.lifted));
      setClawAmount(0);
      $.state.phase = "idle";
      $.log("crane returned home");
    }
  }
});
