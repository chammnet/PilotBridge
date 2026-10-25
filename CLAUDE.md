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

## Phase 1: probe (next step)
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
