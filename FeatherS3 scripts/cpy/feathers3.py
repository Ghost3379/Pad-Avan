"""
Feather S3 Helper Functions
Contains all helper functions for battery monitoring, charging detection, and other utilities
"""

import board
import analogio
import digitalio
import time

# Setup the BATTERY voltage sense pin
vbat_voltage = analogio.AnalogIn(board.BATTERY)

# Setup the VBUS sense pin
vbus_sense = digitalio.DigitalInOut(board.VBUS_SENSE)
vbus_sense.direction = digitalio.Direction.INPUT

def get_battery_voltage():
    """Get the approximate battery voltage."""
    # I don't really understand what CP is doing under the hood here for the ADC range & calibration,
    # but the onboard voltage divider for VBAT sense is setup to deliver 1.1V to the ADC based on it's
    # default factory configuration.
    # This forumla should show the nominal 4.2V max capacity (approximately) when 5V is present and the
    # VBAT is in charge state for a 1S LiPo battery with a max capacity of 4.2V
    global vbat_voltage
    return (vbat_voltage.value / 5371)

def get_vbus_present():
    """Detect if VBUS (5V) power source is present"""
    global vbus_sense
    return vbus_sense.value

def get_battery_percent():
    """Get battery percentage (0-100)"""
    battery_percent = int(get_battery_voltage() * 100 - 320)
    # Clamp to valid range
    if battery_percent < 0:
        battery_percent = 0
    elif battery_percent > 100:
        battery_percent = 100
    return battery_percent

def get_battery_status():
    """Get comprehensive battery status including voltage, percentage, and charging state"""
    voltage = get_battery_voltage()
    percentage = get_battery_percent()
    is_charging = get_vbus_present()
    
    return {
        "voltage": round(voltage, 2),
        "percentage": percentage,
        "is_charging": is_charging,
        "status": "Charging" if is_charging else "Not Charging"
    }

def format_battery_info():
    """Format battery info for display"""
    status = get_battery_status()
    return f"BAT: {status['percentage']}% ({status['voltage']}V) - {status['status']}"

def is_low_battery():
    """Check if battery is low (< 20%)"""
    return get_battery_percent() < 20

def get_system_info():
    """Get general system information"""
    import microcontroller
    
    return {
        "board": board.board_id,
        "cpu_freq": microcontroller.cpu.frequency,
        "temperature": microcontroller.cpu.temperature,
        "battery": get_battery_status()
    } 