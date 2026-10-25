# Pilot Pro -> Virtual Gamepad Bridge (Roadcraft crane control)

## Goal
Use the XPPen Pilot Pro editing console (joystick + 3 dials + buttons) as an analog
crane controller in the game Roadcraft (Steam). The device is not seen by Windows as
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

## Architecture (planned)
1. READ: Python + `hidapi` (pip install hidapi). Enumerate HID devices, find the
   Pilot Pro by VID/PID, open the right interface (it likely exposes several:
   keyboard-emulation, consumer control, vendor-defined). Dump raw reports.
2. EMIT: ViGEmBus driver + `vgamepad` Python package -> virtual Xbox 360 pad.
   Xbox 360 rather than vJoy/DirectInput, because Roadcraft's generic joystick/axis
   support is poor (axis inputs reportedly ignored in custom binding; game is
   built around the 360 controller). ViGEmBus is officially retired but still works.
3. BRIDGE: loop reading reports -> scale -> set virtual stick axes / buttons.
   Optional later: HidHide to hide the physical device from the game.

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
      dir: 01 / 02. Mostly 02 = CCW, 01 = CW, but two runs disagreed ->
      UNCONFIRMED, retest with one direction only.
  - Button: 02 F3 03 <01 down|00 up> b4 b5 b6 b7 ...   one bit per control:
      b5: btn1=10 btn2=20 btn3=80 btn4=40 btn5=01 btn6=02 btn7=08 btn8=04
      b6: d-pad up=02 down=08 left=01 right=04
      b7: top knob press=01
      b4: big knob press=20, big knob center button=10,
          top touch up=01 down=02 left=08 right=04 (touch is twitchy; less sure)
    (Side button numbers refer to Craig's labeled diagram.)
- With the XPPen app running, each touch press is followed by four
  02 B4 05 01 02 xx reports -> likely replies to the app driving haptics.
  Not seen with the app closed.
- UNTESTED: stick reports after raw mode. Do not assume unchanged until seen.
- Top small knob is hard to turn without nudging the stick -> configurable
  deadzone required; optionally suppress small stick input while a knob turns.

## Phase 1: probe (done for the joystick)
- Use a USB-C cable (not Bluetooth) for probing.
- Write `probe.py`:
  - list all HID interfaces (VID, PID, usage page, usage, interface number, path)
  - open the Pilot Pro interfaces (vendor-defined usage page 0xFF00+ is the prime
    suspect) and print each report as hex, only when it changes
- Run the probe in three states and compare:
  a) XPPen app fully closed (check tray + services)
  b) XPPen app open, normal
  c) XPPen app open, on the calibration screen
- If proportional data only shows up in (c): capture the host->device traffic with
  USBPcap + Wireshark while opening calibration, find the mode-switch
  (likely a feature report or output report), and replay it from Python.
- Watch for: the XPPen driver holding the device exclusively; the stick-circle
  motion bleeding into the large dial (reported crosstalk).

## Phase 2+: mapping (later)
- Roadcraft cranes use two sticks; the Pilot Pro has one. Plan: joystick -> one
  virtual stick; dials (relative encoders) -> other axes as rate control
  (spin speed = axis value, decaying back to center when idle).
- Deadzone, response curve, and a hotkey to toggle the bridge on/off.
- Config file for mappings; tray app or console app TBD.
