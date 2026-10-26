# PilotBridge

Turns the XPPen Pilot Pro editing console (joystick, three knobs, buttons, d-pad) into a fully configurable virtual Xbox 360 controller. The first target is analog crane control in Roadcraft, where turning a knob faster moves the crane faster, the same way a gamepad stick does.

Windows doesn't see the Pilot Pro as a game controller. PilotBridge reads its raw HID input and drives a virtual Xbox 360 pad through ViGEmBus. No custom kernel driver, and the XPPen app isn't needed (close it while using this).

## Status

Early and untested in-game. The device protocol is worked out, and the mapping engine is unit-tested. The Roadcraft profile is a starting point that still needs tuning.

## Requirements

- Windows 10/11 x64
- [ViGEmBus](https://github.com/nefarius/ViGEmBus/releases) 1.22 (`winget install ViGEm.ViGEmBus`)
- Pilot Pro connected over USB
- To build: .NET 10 SDK

## Run

```
dotnet build PilotBridge.slnx
PilotBridge.App\bin\Debug\net10.0\PilotBridge.App.exe --monitor
```

- **Profile:** `profiles\roadcraft.jsonc` is used by default. Pass another profile's path to use that instead.
- **`--monitor`:** shows the virtual pad's live state.
- **Stop:** Ctrl+C.

## Profiles

A profile is a JSONC file (JSON that allows `//` comments) that maps:

- the **joystick** to any stick axis or axes, with dead zone, response curve and invert;
- every **button**, d-pad direction, knob press and touch direction to any gamepad button;
- each **knob** to an axis or to buttons:
  - **`Rate`**: turning speed drives the axis, which glides back to center when you stop turning;
  - **`Position`**: each tick moves the axis a step and it stays there;
  - **`Buttons`**: each tick taps a button.

Every setting, its default and what it does are listed in [settings-help.jsonc](PilotBridge.Core/settings-help.jsonc). See [profiles/roadcraft.jsonc](profiles/roadcraft.jsonc) for an example profile.

## Repository layout

- `PilotBridge.Core`: protocol parser and mapping engine (no device needed; unit-tested)
- `PilotBridge.App`: the console app (HidSharp + ViGEm)
- `PilotBridge.Core.Tests`: xUnit tests
- `profiles`: game profiles
- `tools/probe`: the Python HID probe used to work out the protocol
- [CLAUDE.md](CLAUDE.md): design notes and the reverse-engineered protocol

## License

Licensed under [AGPL-3.0](LICENSE). Use it, fork it, learn from it - if you redistribute a modified version, the same license applies to your version too.

No affiliation with XPPen or the makers of Roadcraft.
