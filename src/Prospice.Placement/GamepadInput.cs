using Rewired;
using UnityEngine;
namespace Prospice.Placement;

internal readonly record struct PadFrame(bool Connected, float LT, Vector2 Stick, float RT, bool Cancel, bool Menu);
internal sealed class GamepadInput
{
    int selected = -1;
    public void Reset() => selected = -1;
    public PadFrame Read(Player player, bool active)
    {
        if (!ReInput.isReady || player == null || player.controllers.joystickCount == 0) return default;
        // The generated zero-argument overload aliases a generic MethodInfo and fails AOT.
        // The explicit ControllerType overload has its own verified native binding.
        var last = player.controllers.GetLastActiveController(ControllerType.Joystick);
        if (last != null)
        {
            double time = last.GetLastTimeActive();
            var keyboard = player.controllers.GetLastActiveController(ControllerType.Keyboard);
            var mouse = player.controllers.GetLastActiveController(ControllerType.Mouse);
            if ((keyboard != null && keyboard.GetLastTimeActive() > time) || (mouse != null && mouse.GetLastTimeActive() > time)) return default;
        }
        var joysticks = player.controllers.Joysticks;
        for (int i = 0; i < player.controllers.joystickCount; i++)
        {
            var joystick = joysticks[i];
            if (active && joystick.id != selected) continue;
            if (!active && last != null && joystick.id != last.id) continue;
            // Rewired.et is this build's obfuscated IGamepadTemplate; ez supplies its real GUID.
            var raw = joystick.GetTemplate(ez.typeGuid);
            var pad = raw?.TryCast<et>();
            if (pad == null) continue;
            selected = joystick.id;
            return new(true, pad.leftTrigger.value, pad.leftStick.value, pad.rightTrigger.value,
                pad.b.justPressed, pad.start.justPressed || pad.back.justPressed);
        }
        return default;
    }
}
