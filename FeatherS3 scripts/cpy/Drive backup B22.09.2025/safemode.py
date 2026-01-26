import supervisor
import microcontroller

# SafeModeReason als Enum
reason = supervisor.runtime.safe_mode_reason

# Hilfsfunktion: Enum-Wert zu String
def reason_to_str(r):
    try:
        return supervisor.SafeModeReason.string[r]
    except Exception:
        return str(r)

try:
    with open("/safemode_log.txt", "a") as f:
        f.write("Safe mode triggered!\n")
        f.write("Reason: {}\n".format(reason_to_str(reason)))
        f.write("CPU reset cause: {}\n".format(microcontroller.cpu.reset_reason))
        f.write("------------------------\n")
except Exception as e:
    print("Could not write log:", e)
    print("Safe mode reason:", reason)
