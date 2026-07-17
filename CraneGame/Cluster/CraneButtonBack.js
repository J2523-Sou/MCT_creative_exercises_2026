const MESSAGE = "mct-crane-command";
const COMMAND_RADIUS = 10;
const COMMAND = "back";
function send(value) {
  for (const item of $.getItemsNear($.getPosition(), COMMAND_RADIUS)) item.send(MESSAGE, value);
  $.log("crane button send: " + COMMAND);
}
$.onInteract(() => {
  send(COMMAND);
});
$.onUse(isDown => send({ command: COMMAND, isDown: isDown }));
