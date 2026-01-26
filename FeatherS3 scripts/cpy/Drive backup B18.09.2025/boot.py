"""Boot configuration for Feather S3"""
import usb_cdc

print("BOOT.PY: Starting USB configuration...")

# Disable everything first to be safe
usb_cdc.disable()
print("BOOT.PY: All USB CDC devices disabled")

# Enable only the data channel, disable console to save endpoints
usb_cdc.enable(console=False, data=True)
print("BOOT.PY: USB CDC data enabled, console disabled")

# Quick verification
if usb_cdc.data:
    print("BOOT.PY: SUCCESS - USB data channel available")
    print(f"BOOT.PY: Data channel connected: {usb_cdc.data.connected}")
else:
    print("BOOT.PY: FAILURE - USB data channel NOT available")

print("BOOT.PY: Complete")
