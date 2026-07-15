const MESSAGE = "mct-crane-command";
const COMMAND_RADIUS = 3;
$.onInteract(() => {
  for (const item of $.getItemsNear($.getPosition(), COMMAND_RADIUS)) item.send(MESSAGE, "forward");
});
