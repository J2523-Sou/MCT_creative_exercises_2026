const MESSAGE = "mct-crane-command";
const COMMAND_RADIUS = 10;
$.onInteract(() => {
  for (const item of $.getItemsNear($.getPosition(), COMMAND_RADIUS)) item.send(MESSAGE, "reset");
  $.log("crane button send: reset");
});
