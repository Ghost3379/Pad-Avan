import supervisor
import microcontroller
import time

# SafeModeReason als Enum
reason = supervisor.runtime.safe_mode_reason

# Hilfsfunktion: Enum-Wert zu String
def reason_to_str(r):
    try:
        return supervisor.SafeModeReason.string[r]
    except Exception:
        return str(r)

# Check if it's a brownout - if so, restart immediately
if reason == supervisor.SafeModeReason.BROWNOUT:
    # Brownout detected - restart immediately
    supervisor.reload()
else:
    # For other reasons, continue running and print error repeatedly
    while True:
        print("=" * 50)
        print("SAFEMODE.PY: Safe mode detected!")
        print(f"Reason: {reason_to_str(reason)}")
        print(f"CPU reset cause: {microcontroller.cpu.reset_reason}")
        print(f"Timestamp: {time.time()}")
        print("=" * 50)
        time.sleep(5)  # Wait 5 seconds between prints
