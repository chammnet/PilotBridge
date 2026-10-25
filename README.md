# pilot-to-joystick

A user-mode bridge that turns the XPPen Pilot Pro editing console (joystick, dials, buttons) into a virtual Xbox 360 controller, so it can be used as an analog crane controller in Roadcraft.

Windows doesn't see the Pilot Pro as a game controller. This project reads its raw HID input with Python (`hidapi`) and drives a virtual gamepad through ViGEmBus (`vgamepad`). No kernel driver.

## Status

Early: Phase 1 (probing the device's HID reports) hasn't started yet. See [CLAUDE.md](CLAUDE.md) for the design notes and plan.

## License

Licensed under [AGPL-3.0](LICENSE). Use it, fork it, learn from it - if you redistribute a modified version, the same license applies to your version too.

No affiliation with XPPen or the makers of Roadcraft.
