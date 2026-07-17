const MESSAGE = "mct-crane-command";
// The control deck is scaled independently from the controller Item. Keep the
// search radius large enough to cover the complete cabinet after resizing.
const COMMAND_RADIUS = 10;
const COMMAND = "left";
function send(value) {
  for (const item of $.getItemsNear($.getPosition(), COMMAND_RADIUS)) item.send(MESSAGE, value);
  $.log("crane button send: " + COMMAND);
}
$.onInteract(() => {
  // Interact has no release callback, so repeated presses toggle movement.
  send(COMMAND);
});
$.onUse(isDown => send({ command: COMMAND, isDown: isDown }));
