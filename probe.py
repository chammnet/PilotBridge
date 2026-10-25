"""Phase 1 probe for the XPPen Pilot Pro.

Lists the device's HID interfaces and dumps raw input reports from every
interface that can be opened, printing a report only when it differs from
the previous one on that interface. A marker line under each report points
at the bytes that changed, which is how axes and buttons show themselves.

Usage:
    python probe.py --list              list interfaces and exit
    python probe.py                     dump reports until Ctrl+C
    python probe.py --seconds 20        dump for 20 seconds
    python probe.py --log logs\a.txt    also write output to a file

Read-only: this never sends anything to the device.
"""

import argparse
import os
import sys
import time

import hid

XPPEN_VID = 0x28BD
PILOT_PRO_PID = 0x0203

# Names for the usage pages/usages this device is known to expose, so the
# output says "Mouse" instead of making you look up 0x01/0x02.
USAGE_NAMES = {
    (0x01, 0x02): "Generic Desktop: Mouse",
    (0x01, 0x04): "Generic Desktop: Joystick",
    (0x01, 0x05): "Generic Desktop: Gamepad",
    (0x01, 0x06): "Generic Desktop: Keyboard",
    (0x0C, 0x01): "Consumer Control",
    (0x0D, 0x01): "Digitizer: Digitizer",
    (0x0D, 0x02): "Digitizer: Pen",
}


def usage_name(page, usage):
    if (page, usage) in USAGE_NAMES:
        return USAGE_NAMES[(page, usage)]
    if page >= 0xFF00:
        return "Vendor-defined"
    return "Unknown"


def find_interfaces(vid, pid):
    """Return the device's HID interfaces, one entry per path.

    hidapi on Windows can list the same path more than once (one entry per
    top-level collection seen through it); keep the first of each.
    """
    seen = set()
    result = []
    for d in hid.enumerate(vid, pid):
        if d["path"] in seen:
            continue
        seen.add(d["path"])
        result.append(d)
    result.sort(key=lambda d: (d["interface_number"], d["usage_page"], d["usage"]))
    return result


def label(d):
    return "if%d %04X/%04X %s" % (
        d["interface_number"], d["usage_page"], d["usage"],
        usage_name(d["usage_page"], d["usage"]))


def change_marker(old, new):
    """'^^' under each byte of new that differs from old (or is new)."""
    marks = []
    for i, b in enumerate(new):
        changed = old is None or i >= len(old) or old[i] != b
        marks.append("^^" if changed else "  ")
    return " ".join(marks).rstrip()


class Output:
    """Prints to the console and, optionally, a log file."""

    def __init__(self, log_path):
        self.log = None
        if log_path:
            os.makedirs(os.path.dirname(os.path.abspath(log_path)), exist_ok=True)
            self.log = open(log_path, "a", encoding="ascii")

    def line(self, text=""):
        print(text, flush=True)
        if self.log:
            self.log.write(text + "\n")
            self.log.flush()

    def close(self):
        if self.log:
            self.log.close()


def list_interfaces(interfaces, out):
    out.line("Found %d interface(s) for %04X:%04X:" % (
        len(interfaces), XPPEN_VID, PILOT_PRO_PID))
    for n, d in enumerate(interfaces):
        out.line("  [%d] %s" % (n, label(d)))
        out.line("      product: %s / %s" % (d["manufacturer_string"], d["product_string"]))
        out.line("      path:    %s" % d["path"].decode(errors="replace"))


def open_interfaces(interfaces, out):
    """Open each interface non-blocking; report (not raise) failures."""
    opened = []
    for d in interfaces:
        dev = hid.device()
        try:
            dev.open_path(d["path"])
            dev.set_nonblocking(1)
        except (OSError, IOError) as e:
            # Expected for keyboard/mouse collections: Windows owns them
            # exclusively. Also what an exclusive XPPen driver would look like.
            out.line("  could not open %s: %s" % (label(d), e))
            continue
        out.line("  opened %s" % label(d))
        opened.append({"info": d, "dev": dev, "last": None, "count": 0})
    return opened


def dump(opened, seconds, out):
    start = time.monotonic()
    out.line()
    out.line("Reading. Move the joystick, turn the dials, press buttons. Ctrl+C to stop.")
    out.line("Format: seconds  interface  report bytes (hex), changed bytes marked below.")
    out.line()
    try:
        while seconds is None or time.monotonic() - start < seconds:
            got_any = False
            for o in list(opened):
                try:
                    data = o["dev"].read(1024)
                except (OSError, IOError) as e:
                    # Windows lets these open but refuses reads on collections
                    # it consumes itself (mouse, keyboard, pen digitizer).
                    out.line("read failed on %s: %s (dropping it; expected for"
                             " mouse/keyboard/pen, Windows reserves those)"
                             % (label(o["info"]), e))
                    opened.remove(o)
                    continue
                if not data:
                    continue
                got_any = True
                o["count"] += 1
                if data == o["last"]:
                    continue
                t = time.monotonic() - start
                hexed = " ".join("%02X" % b for b in data)
                out.line("%8.3f  if%d  %s" % (t, o["info"]["interface_number"], hexed))
                out.line("%8s  %3s  %s" % ("", "", change_marker(o["last"], data)))
                o["last"] = data
            if not opened:
                out.line("No interfaces left to read.")
                break
            if not got_any:
                time.sleep(0.001)
    except KeyboardInterrupt:
        pass
    out.line()
    out.line("Reports received per interface (including unchanged repeats):")
    for o in opened:
        out.line("  %s: %d" % (label(o["info"]), o["count"]))


def main():
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument("--list", action="store_true", help="list interfaces and exit")
    p.add_argument("--seconds", type=float, help="stop after this many seconds")
    p.add_argument("--log", help="also append output to this file")
    args = p.parse_args()

    out = Output(args.log)
    try:
        interfaces = find_interfaces(XPPEN_VID, PILOT_PRO_PID)
        if not interfaces:
            out.line("Pilot Pro (%04X:%04X) not found. Plugged in over USB-C?" % (
                XPPEN_VID, PILOT_PRO_PID))
            return 1
        list_interfaces(interfaces, out)
        if args.list:
            return 0
        out.line()
        out.line("Opening interfaces:")
        opened = open_interfaces(interfaces, out)
        if not opened:
            out.line("Nothing could be opened.")
            return 1
        dump(opened, args.seconds, out)
        for o in opened:
            o["dev"].close()
        return 0
    finally:
        out.close()


if __name__ == "__main__":
    sys.exit(main())
