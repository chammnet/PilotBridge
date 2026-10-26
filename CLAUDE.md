# Pilot Pro -> Virtual Gamepad Bridge (PilotBridge)

## Goal
Use the XPPen Pilot Pro editing console (joystick + 3 dials + buttons) as a fully
configurable game controller. First target: analog crane control in Roadcraft (Steam). The device is not seen by Windows as
a game controller. Build a user-mode bridge (NOT a kernel driver) that reads the
Pilot Pro's raw HID input and drives a virtual Xbox 360 controller.

## Owner context
- Craig: ~30 yrs IT, deeply fluent in PowerShell and Windows admin, Linux/FreeBSD CLI.
  New to HID / USB reverse engineering. Explain HID concepts briefly when they come up.
- Prefers: non-destructive defaults, small testable steps, honest uncertainty.
- Editor: VS Code with Gremlins Tracker. Keep source files plain ASCII
  (no em dashes, smart quotes, or other fancy Unicode).
- Repo: github.com/chammnet/pilot-to-joystick (public, AGPL-3.0), local clone at
  N:\github\chammnet\pilot-to-joystick. Public repo: no secrets or personal data in commits.

## Architecture (C# / .NET 10, decided 2026-10-25)
Python was for discovering the protocol; the product is C# so it ships as a
self-contained exe (no Python/.NET install for users) with a real Windows GUI later.
- PilotBridge.slnx, flat layout like LessonRecorder:
  - PilotBridge.Core: device-independent and unit-tested. PilotPro.cs (protocol
    constants + report parser), Profile.cs (JSON profile model), MappingEngine.cs,
    StickMapper.cs, KnobAxis.cs (knob -> axis / buttons).
  - PilotBridge.App: console exe. HidSharp opens the vendor interface (if2), sends
    raw mode, reader thread timestamps reports; main loop (~580 Hz with
    Thread.Sleep(1)) runs MappingEngine.Update and drives a virtual Xbox 360 pad
    via Nefarius.ViGEm.Client. Flags: [profile.json] --monitor --seconds N --list.
  - PilotBridge.Core.Tests: xUnit.
- profiles/*.json: one per game, copied next to the exe. Comments allowed.
- tools/probe/probe.py: the Python HID probe (hidapi), kept as a diagnostic tool.
  Run with the repo-root .venv: .venv\Scripts\python.exe tools\probe\probe.py
- Output: Xbox 360 pad via ViGEmBus 1.22 (winget ViGEm.ViGEmBus). Roadcraft's
  generic joystick support is poor; it's built around the 360 pad. ViGEmBus is
  retired upstream but works. Users must install it once (kernel driver).
- Library licenses all permissive (HidSharp Apache-2.0, ViGEm client MIT,
  ViGEmBus BSD-3) - keep it that way (see owner's commercial-option note).

## Mapping model
- Stick: radial deadzone, curve exponent, antiDeadzone; X and Y each go to any
  axis (or None) with invert. Several sources on one axis add, then clamp.
- Axes: LeftX LeftY RightX RightY LeftTrigger RightTrigger, plus Triggers
  (one bipolar axis: + = RT, - = LT).
- Buttons/d-pad/knob presses/touch -> any gamepad button.
- Knobs: Rate | Position | Buttons.
  - Rate: speed = 1 / (average of last AverageIntervals tick intervals), so steady
    turning gives a steady value at any speed (no per-tick sawtooth). When a tick
    is overdue, speed = min(speed, 1/time-since-last-tick) -> glides down; below
    MinTicksPerSecond -> 0. First tick of a run = MinTicksPerSecond (or nothing
    with IgnoreSingleTicks). A reversal starts a new run. Output = shaped
    speed/FullScale, then slew-limited: RampUp while growing, RampDown while
    shrinking; a sign reversal returns to 0 at RampDown first.
  - Position: each tick +/- StepPerTick, stays (triggers clamp at 0..1).
  - Buttons: each tick = one tap (hold PulseMs, release PulseMs), queue max 10.
- antiDeadzone exists because games ignore small stick values; tune in-game.

## Not done yet / ideas
- 8-way digital stick mode with threshold (Craig wants it).
- Crosstalk filter tied to stick deflection (currently only IgnoreSingleTicks).
- Device unplug/replug: app exits; should wait and re-send raw mode.
- Top touch knob: Craig thinks it is designed for DOUBLE TAP (single taps were
  unreliable). Probe double taps to see what they report.
- Hotkey to toggle the bridge; tray app; GUI profile editor.
- Roadcraft profile is a STARTER, untested in-game.

## What we know so far
- Marketing and most app settings describe the joystick as 4/8-way digital.
- BUT the XPPen app has a joystick CALIBRATION screen where the cursor moves around
  a target with very fine precision -> the device almost certainly sends
  proportional X/Y data to the host.
- Open question: is proportional data in every report, or only after the XPPen
  app/driver sends an enable/mode-switch command (e.g. when calibration opens)?
  ANSWERED 2026-10-25: proportional data is sent with the XPPen app fully closed.
  No mode switch needed.

## Device facts (from probe, USB-C, XPPen app closed, 2026-10-25)
- VID 0x28BD ("Hanvon ugee"), PID 0x0203, product "Pilot Pro".
- HID interfaces: if0 = 2x Mouse (01/02) + Keyboard (01/06); if1 = Digitizer Pen
  (0D/02); if2 = Vendor-defined (FF0A/01).
- All open with hidapi, but Windows refuses reads on mouse, keyboard AND the pen
  digitizer (OS-reserved). Only if2 (vendor) is readable -> the bridge reads if2.
- Joystick report on if2, 12 bytes:
    02 F3 01 01  XL XH  YL YH  00 00 00 00
  bytes 0-3 constant header (02 = report ID); X = int16 LE bytes 4-5,
  Y = int16 LE bytes 6-7. Range about -512..+511, round gate (max radius ~513).
  +X = right, +Y = up (same sign convention as XInput, no inversion needed).
  ~200 reports/s while moving; on release jumps straight to exactly 0,0.
- Every ~10.3 s on if2: 02 F2 01 64 01 50 00 00 00 00 00 00. Linux hid-uclogic
  treats subreport 0xF2 as battery, so 0x64 is probably battery = 100%.
- Without raw mode, knobs send only 1-2 reports per 10 ticks on if2 and buttons
  send nothing there (the XPPen app is what turns them into keystrokes).

## Raw mode (decided 2026-10-25: the bridge owns ALL input, XPPen app closed)
Reason: mixing keyboard (XPPen app keystrokes) and gamepad input would confuse
Roadcraft's input-mode switching.
- Send output report on if2: 02 B0 04 + 17 x 00 (report ID 0x02 + 19 bytes, the
  size the if2 descriptor declares). Same magic Linux hid-uclogic sends to UGEE v2
  / XP-Pen tablets (uclogic_ugee_v2_probe_arr). Device acks with input report
  02 B1 04. `probe.py --init` does this. Assumed to reset on unplug (unverified).
- if2 descriptor: Input ID 02 = 11 bytes, Output ID 02 = 19 bytes. No features.
- Works whether or not the XPPen app is running (app does not open exclusively).
- After raw mode, every knob tick and every button arrives on if2:
  - Knob:   02 F3 02 <dir> <id> 01 ...   one report per tick.
      id: 01 = big knob, 04 = outer ring, 02 = top small knob
      dir: 01 = CW, 02 = CCW (confirmed: CW-only test on big + top knob).
  - Button: 02 F3 03 <01 down|00 up> b4 b5 b6 b7 ...   one bit per control:
      b5: btn1=10 btn2=20 btn3=80 btn4=40 btn5=01 btn6=02 btn7=08 btn8=04
      b6: d-pad up=02 down=08 left=01 right=04
      b7: top knob press=01
      b4: big knob press=20, big knob center button=10,
          top touch up=01 down=02 left=08 right=04 (from the clean run)
    (Side button numbers refer to Craig's labeled diagram.)
- 02 B4 05 01 02 xx reports, four at a time (xx cycles 01 02 08 04 = the touch
  direction bits). Seen after touch presses with the XPPen app running; NOT seen
  in the probe runs with the app closed; but under PilotBridge.App (app closed,
  device untouched) they arrive about once a second. Not haptics. Meaning
  unknown; the parser returns UnknownReport and the engine ignores them.
- Stick reports are unchanged in raw mode (confirmed 2026-10-25).
- Crosstalk goes both ways:
  - turning the top small knob nudges the stick -> configurable stick deadzone;
  - moving the stick produced 2 stray single big-knob ticks (dir 01) -> filter
    isolated knob ticks while the stick is deflected (configurable).

## Haptics (open question)
- The device buzzes on its own (firmware) for knob ticks and top touches, with
  the XPPen app closed. All buzzes feel identical.
- Unknown whether the host can trigger a buzz. Idea: map game rumble (vgamepad
  X360 notification callback: large/small motor) to buzz pulses.
- To investigate: if the XPPen app has a haptic setting/test, capture its
  host->device traffic with USBPcap and replay ONLY those exact commands.
  Never fuzz unknown vendor commands (could write device flash).

## History
- Phase 1 (2026-10-25): Python probe found the vendor interface, the stick
  format, then the raw-mode switch, knob and button layouts.
- Phase 2 (2026-10-25): C# solution, mapping engine, starter Roadcraft profile.
- Later: GUI that edits profiles, with our OWN SVG drawing of the device (drawn
  from the shape/proportions of XPPen's render, not traced; no XPPen logo).
  Mock up layouts for Craig to compare before building. UI must be DPI-correct
  (4K @ 150%).
