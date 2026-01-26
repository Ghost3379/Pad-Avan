import microcontroller
import supervisor

print(supervisor.runtime.safe_mode_reason)

if supervisor.runtime.safe_mode_reason == supervisor.SafeModeReason.BROWNOUT:
    microcontroller.reset()    # Reset and start over.
    
# Otherwise, do nothing. The safe mode reason will be printed in the
# console, and nothing will run.