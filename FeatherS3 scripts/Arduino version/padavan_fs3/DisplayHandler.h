/*
 * DisplayHandler.h
 * Header-only library for SSD1306 OLED display management
 * Wraps Adafruit_GFX and Adafruit_SSD1306 for faster compilation
 */

#ifndef DISPLAY_HANDLER_H
#define DISPLAY_HANDLER_H

#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>

class DisplayHandler {
private:
  Adafruit_SSD1306* display;
  uint8_t oledAddress;
  bool isAvailable;
  bool isEnabled;
  
public:
  DisplayHandler(int width, int height, uint8_t address) 
    : oledAddress(address), isAvailable(false), isEnabled(true) {
    display = new Adafruit_SSD1306(width, height, &Wire, -1);
  }
  
  ~DisplayHandler() {
    if (display) {
      delete display;
    }
  }
  
  // Initialize the display
  bool begin() {
    if (!display->begin(SSD1306_SWITCHCAPVCC, oledAddress)) {
      isAvailable = false;
      return false;
    }
    isAvailable = true;
    return true;
  }
  
  // Check if display is available
  bool available() const {
    return isAvailable;
  }
  
  // Set enabled state
  void setEnabled(bool enabled) {
    isEnabled = enabled;
  }
  
  // Get enabled state
  bool getEnabled() const {
    return isEnabled;
  }
  
  // Check I2C connection (with timeout protection)
  bool checkConnection() {
    if (!isAvailable) return false;
    // Use requestFrom with timeout instead of beginTransmission/endTransmission
    // This is more reliable and respects Wire timeout
    Wire.requestFrom(oledAddress, (uint8_t)1, (uint8_t)true); // Request 1 byte, stop after
    if (Wire.available() == 0) {
      // No response - display might be disconnected
      isAvailable = false;
      return false;
    }
    Wire.read(); // Read the byte (we don't need it, just checking connection)
    return true;
  }
  
  // Direct access methods (bypass connection check for initialization)
  void clearDisplay() {
    if (isAvailable && isEnabled) {
      display->clearDisplay();
    }
  }
  
  void setTextSizeDirect(uint8_t size) {
    if (isAvailable && isEnabled) {
      display->setTextSize(size);
    }
  }
  
  void setTextColorDirect(uint16_t color) {
    if (isAvailable && isEnabled) {
      display->setTextColor(color);
    }
  }
  
  void setCursorDirect(int16_t x, int16_t y) {
    if (isAvailable && isEnabled) {
      display->setCursor(x, y);
    }
  }
  
  void printlnDirect(const String& text) {
    if (isAvailable && isEnabled) {
      display->println(text);
    }
  }
  
  void displayUpdate() {
    if (isAvailable && isEnabled) {
      display->display();
    }
  }
  
  // Clear display
  void clear() {
    if (isAvailable && isEnabled) {
      display->clearDisplay();
    }
  }
  
  // Set text size
  void setTextSize(uint8_t size) {
    if (isAvailable && isEnabled) {
      display->setTextSize(size);
    }
  }
  
  // Set text color
  void setTextColor(uint16_t color) {
    if (isAvailable && isEnabled) {
      display->setTextColor(color);
    }
  }
  
  // Set cursor position
  void setCursor(int16_t x, int16_t y) {
    if (isAvailable && isEnabled) {
      display->setCursor(x, y);
    }
  }
  
  // Print text
  void print(const String& text) {
    if (isAvailable && isEnabled) {
      display->print(text);
    }
  }
  
  // Print line
  void println(const String& text) {
    if (isAvailable && isEnabled) {
      display->println(text);
    }
  }
  
  // Update display (send buffer to screen)
  void update() {
    if (isAvailable && isEnabled) {
      display->display();
    }
  }
  
  // Display a message (complete operation: clear, set text, display)
  void showMessage(const String& message) {
    if (!isEnabled || !isAvailable) {
      return;
    }
    
    // Check I2C connection
    if (!checkConnection()) {
      return;
    }
    
    display->clearDisplay();
    display->setTextSize(2);
    display->setTextColor(SSD1306_WHITE);
    display->setCursor(10, 10);
    display->println(message);
    display->display();
  }
  
  // Clear and update display
  void clearAndUpdate() {
    if (isAvailable && isEnabled) {
      display->clearDisplay();
      display->display();
    }
  }
  
  // Get the underlying display object (for advanced operations if needed)
  Adafruit_SSD1306* getDisplay() {
    return display;
  }
};

#endif // DISPLAY_HANDLER_H

