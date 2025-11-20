/*
 * PadAwan-Force Macro Pad - Arduino Version
 * ESP32-S3 FeatherS3 Implementation
 * 
 * Features:
 * - USB HID Keyboard & Consumer Control
 * - 6 Buttons
 * - 2 Rotary Encoders with Press
 * - SSD1306 OLED Display
 * - SD Card Configuration Storage
 * - Serial Communication with Desktop App
 */

#include <USB.h>
#include <USBHIDKeyboard.h>
#include <USBHIDConsumerControl.h>
#include "tusb.h"  // TinyUSB header for direct HID access
#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>
#include <SD.h>
#include <SPI.h>
#include <ArduinoJson.h>
#include <RotaryEncoder.h>
#include <string.h>  // For memset
#include "KeyboardLayoutWinCH.h"

// ===== DEBUG SETTINGS =====
// Set to 1 to enable Serial debug output (visible in Serial Monitor)
// Set to 0 to disable debug output (only app communication)
#define DEBUG_SERIAL 1

#if DEBUG_SERIAL
  #define DEBUG_PRINT(x) Serial.print(x)
  #define DEBUG_PRINTLN(x) Serial.println(x)
#else
  #define DEBUG_PRINT(x)
  #define DEBUG_PRINTLN(x)
#endif

// ===== PIN DEFINITIONS =====
// Buttons: 1->IO14, 2->IO18, 3->IO5, 4->IO17, 5->IO6, 6->IO12
#define BUTTON_1_PIN 14
#define BUTTON_2_PIN 18
#define BUTTON_3_PIN 5
#define BUTTON_4_PIN 17
#define BUTTON_5_PIN 6
#define BUTTON_6_PIN 12

// Rotary A: A->IO10, B->IO11, Press->IO7
#define ROTARY_A_PIN_A 10
#define ROTARY_A_PIN_B 11
#define ROTARY_A_BUTTON_PIN 7

// Rotary B: A->IO1, B->IO3, Press->IO33
#define ROTARY_B_PIN_A 1
#define ROTARY_B_PIN_B 3
#define ROTARY_B_BUTTON_PIN 33

// SD Card: CS->IO38
#define SD_CS_PIN 38

// Battery: ADC Pin IO2 (ADC1 CH1)
#define BATTERY_ADC_PIN 2

// Display: I2C, Address 0x3C
#define SCREEN_WIDTH 128
#define SCREEN_HEIGHT 32
#define OLED_ADDRESS 0x3C

// ===== GLOBAL OBJECTS =====
USBHIDKeyboard Keyboard;
USBHIDConsumerControl ConsumerControl;
Adafruit_SSD1306 display(SCREEN_WIDTH, SCREEN_HEIGHT, &Wire, -1);
RotaryEncoder rotaryA(ROTARY_A_PIN_A, ROTARY_A_PIN_B, RotaryEncoder::LatchMode::FOUR3);
RotaryEncoder rotaryB(ROTARY_B_PIN_A, ROTARY_B_PIN_B, RotaryEncoder::LatchMode::FOUR3);
KeyboardLayoutWinCH keyboardLayout(&Keyboard);

// ===== CONFIGURATION =====
String configFilePath = "/macropad_config.json";
int currentLayer = 1;
int maxLayers = 1;
int maxButtons = 6;
String displayMode = "layer";  // "off", "layer", "battery", "time"
bool displayEnabled = true;
String systemTime = "";
String systemDate = "";

// Firmware version
const String FIRMWARE_VERSION = "1.0.0";

// ===== STATE VARIABLES =====
bool sdAvailable = false;
bool uploading = false;
String jsonBuffer = "";
int rotaryAPosition = 0;
int rotaryBPosition = 0;
int rotaryALastPosition = 0;
int rotaryBLastPosition = 0;

// Button states (for debouncing)
bool buttonStates[6] = {false, false, false, false, false, false};
unsigned long buttonLastPress[6] = {0, 0, 0, 0, 0, 0};
bool rotaryAButtonState = false;
bool rotaryBButtonState = false;
unsigned long rotaryAButtonLastPress = 0;
unsigned long rotaryBButtonLastPress = 0;

const unsigned long DEBOUNCE_DELAY = 50; // 50ms debounce

// ===== BUTTON CONFIGURATION STRUCTURE =====
struct ButtonConfig {
  bool enabled;
  String action;  // "Type Text", "Special Key", "Key combo", "Layer Switch", "None"
  String key;     // Key value or text
};

struct KnobConfig {
  String ccwAction;   // CCW rotation action
  String cwAction;    // CW rotation action
  String pressAction; // Press action
  String ccwKey;     // CCW key value
  String cwKey;      // CW key value
  String pressKey;   // Press key value
};

// ===== SETUP =====
void setup() {
  Serial.begin(115200);
  delay(100); // Small delay for serial to initialize
  
  DEBUG_PRINTLN("PADAWAN: Starting...");
  
  // Initialize USB HID
  Keyboard.begin();
  ConsumerControl.begin();
  USB.begin();
  delay(500); // Give USB time to initialize
  
  DEBUG_PRINTLN("PADAWAN: USB HID initialized");
  
  // Initialize I2C for display
  Wire.begin();
  
  // Initialize display
  if (!display.begin(SSD1306_SWITCHCAPVCC, OLED_ADDRESS)) {
    DEBUG_PRINTLN("PADAWAN: Display initialization failed!");
  } else {
    DEBUG_PRINTLN("PADAWAN: Display initialized");
    display.clearDisplay();
    display.setTextSize(2);
    display.setTextColor(SSD1306_WHITE);
    display.setCursor(10, 10);
    display.println("Starting...");
    display.display();
  }
  
  // Initialize SD card
  SPI.begin();
  if (!SD.begin(SD_CS_PIN)) {
    DEBUG_PRINTLN("PADAWAN: SD card initialization failed!");
    sdAvailable = false;
  } else {
    DEBUG_PRINTLN("PADAWAN: SD card initialized");
    sdAvailable = true;
  }
  
  // Initialize button pins
  pinMode(BUTTON_1_PIN, INPUT_PULLUP);
  pinMode(BUTTON_2_PIN, INPUT_PULLUP);
  pinMode(BUTTON_3_PIN, INPUT_PULLUP);
  pinMode(BUTTON_4_PIN, INPUT_PULLUP);
  pinMode(BUTTON_5_PIN, INPUT_PULLUP);
  pinMode(BUTTON_6_PIN, INPUT_PULLUP);
  
  // Initialize rotary encoder button pins
  pinMode(ROTARY_A_BUTTON_PIN, INPUT_PULLUP);
  pinMode(ROTARY_B_BUTTON_PIN, INPUT_PULLUP);
  
  // Load configuration
  if (sdAvailable) {
    loadConfiguration();
  } else {
    DEBUG_PRINTLN("PADAWAN: ERROR - SD card required!");
    updateDisplay("SD Error!");
  }
  
  // Initialize rotary encoders
  rotaryA.setPosition(0);
  rotaryB.setPosition(0);
  
  DEBUG_PRINTLN("PADAWAN: Ready!");
  updateDisplayMode();
}

// ===== MAIN LOOP =====
void loop() {
  // Handle serial communication
  handleSerial();
  
  // Update rotary encoders
  rotaryA.tick();
  rotaryB.tick();
  
  // Check rotary encoder rotation
  int newPosA = rotaryA.getPosition();
  if (newPosA != rotaryALastPosition) {
    // Swap direction mapping to fix CCW/CW being switched
    if (newPosA > rotaryALastPosition) {
      handleRotaryRotation("A", "ccw");  // Swapped
    } else {
      handleRotaryRotation("A", "cw");   // Swapped
    }
    rotaryALastPosition = newPosA;
  }
  
  int newPosB = rotaryB.getPosition();
  if (newPosB != rotaryBLastPosition) {
    // Swap direction mapping to fix CCW/CW being switched
    if (newPosB > rotaryBLastPosition) {
      handleRotaryRotation("B", "ccw");  // Swapped
    } else {
      handleRotaryRotation("B", "cw");   // Swapped
    }
    rotaryBLastPosition = newPosB;
  }
  
  // Check buttons
  checkButtons();
  
  // Check rotary encoder buttons
  checkRotaryButtons();
  
  // Update display periodically
  static unsigned long lastDisplayUpdate = 0;
  if (millis() - lastDisplayUpdate > 1000) {
    if (!uploading) {
      updateDisplayMode();
    }
    lastDisplayUpdate = millis();
  }
  
  delay(1); // Small delay to prevent overwhelming
}

// ===== SERIAL COMMUNICATION =====
void handleSerial() {
  if (Serial.available() > 0) {
    String line = Serial.readStringUntil('\n');
    line.trim();
    
    DEBUG_PRINTLN("USB received: " + line);
    
    // Handle commands
    if (line == "PING") {
      Serial.println("PONG");
      return;
    }
    
    if (line == "DOWNLOAD_CONFIG") {
      downloadConfig(false); // false = send "CONFIG:"
      return;
    }
    
    if (line == "GET_CURRENT_CONFIG") {
      downloadConfig(true); // true = send "CURRENT_CONFIG:"
      return;
    }
    
    if (line == "UPLOAD_LAYER_CONFIG") {
      Serial.println("READY_FOR_LAYER_CONFIG");
      return;
    }
    
    if (line == "BATTERY_STATUS") {
      String batteryResponse = getBatteryStatus();
      Serial.println(batteryResponse);
      return;
    }
    
    if (line == "GET_VERSION") {
      Serial.println("VERSION:" + FIRMWARE_VERSION);
      return;
    }
    
    if (line.startsWith("SET_DISPLAY_MODE:")) {
      String modeStr = line.substring(17);
      int commaPos = modeStr.indexOf(',');
      if (commaPos > 0) {
        displayMode = modeStr.substring(0, commaPos);
        displayEnabled = (modeStr.substring(commaPos + 1) == "true");
      } else {
        displayMode = modeStr;
        displayEnabled = true;
      }
      updateDisplayMode();
      Serial.println("DISPLAY_MODE_SET");
      return;
    }
    
    if (line.startsWith("SET_TIME:")) {
      systemTime = line.substring(9);
      if (displayMode == "time") {
        updateDisplay(systemTime);
      }
      Serial.println("TIME_SET");
      return;
    }
    
    if (line == "BEGIN_JSON") {
      uploading = true;
      jsonBuffer = "";
      updateDisplay("Receiving...");
      return;
    }
    
    if (line == "END_JSON") {
      uploading = false;
      saveConfiguration(jsonBuffer);
      updateDisplay("Done!");
      delay(1000);
      updateDisplayMode();
      Serial.println("UPLOAD_OK");
      return;
    }
    
    if (uploading) {
      if (jsonBuffer.length() > 0) {
        jsonBuffer += "\n";
      }
      jsonBuffer += line;
    }
  }
}

// ===== CONFIGURATION MANAGEMENT =====
void loadConfiguration() {
  if (!sdAvailable) {
    DEBUG_PRINTLN("PADAWAN: SD card not available");
    return;
  }
  
  File file = SD.open(configFilePath, FILE_READ);
  if (!file) {
    DEBUG_PRINTLN("PADAWAN: Config file not found");
    return;
  }
  
  String jsonString = "";
  while (file.available()) {
    jsonString += (char)file.read();
  }
  file.close();
  
  DynamicJsonDocument doc(16384); // 16KB should be enough
  DeserializationError error = deserializeJson(doc, jsonString);
  
  if (error) {
    DEBUG_PRINTLN("PADAWAN: JSON parse error: " + String(error.c_str()));
    return;
  }
  
  // Update display settings
  if (doc.containsKey("display")) {
    JsonObject displayObj = doc["display"];
    if (displayObj.containsKey("mode")) {
      displayMode = displayObj["mode"].as<String>();
    }
    if (displayObj.containsKey("enabled")) {
      displayEnabled = displayObj["enabled"].as<bool>();
    }
  }
  
  // Update system time
  if (doc.containsKey("systemTime")) {
    JsonObject timeObj = doc["systemTime"];
    if (timeObj.containsKey("currentTime")) {
      systemTime = timeObj["currentTime"].as<String>();
    }
    if (timeObj.containsKey("currentDate")) {
      systemDate = timeObj["currentDate"].as<String>();
    }
  }
  
  // Update limits
  if (doc.containsKey("limits")) {
    JsonObject limits = doc["limits"];
    if (limits.containsKey("maxLayers")) {
      maxLayers = limits["maxLayers"];
    }
    if (limits.containsKey("maxButtons")) {
      maxButtons = limits["maxButtons"];
    }
  }
  
  // Update current layer
  if (doc.containsKey("currentLayer")) {
    currentLayer = doc["currentLayer"];
  }
  
  // Count layers from layers array
  if (doc.containsKey("layers")) {
    JsonArray layers = doc["layers"];
    maxLayers = layers.size();
  }
  
  DEBUG_PRINTLN("PADAWAN: Configuration loaded");
  DEBUG_PRINTLN("  Layers: " + String(maxLayers));
  DEBUG_PRINTLN("  Buttons: " + String(maxButtons));
  DEBUG_PRINTLN("  Display Mode: " + displayMode);
  DEBUG_PRINTLN("  Current Layer: " + String(currentLayer));
}

void saveConfiguration(String jsonString) {
  if (!sdAvailable) {
    DEBUG_PRINTLN("PADAWAN: SD card not available");
    return;
  }
  
  DynamicJsonDocument doc(16384);
  DeserializationError error = deserializeJson(doc, jsonString);
  
  if (error) {
    DEBUG_PRINTLN("PADAWAN: JSON parse error: " + String(error.c_str()));
    Serial.println("UPLOAD_FAIL: " + String(error.c_str()));
    return;
  }
  
  // Update display settings from new config
  if (doc.containsKey("display")) {
    JsonObject displayObj = doc["display"];
    if (displayObj.containsKey("mode")) {
      displayMode = displayObj["mode"].as<String>();
    }
    if (displayObj.containsKey("enabled")) {
      displayEnabled = displayObj["enabled"].as<bool>();
    }
  }
  
  // Update system time
  if (doc.containsKey("systemTime")) {
    JsonObject timeObj = doc["systemTime"];
    if (timeObj.containsKey("currentTime")) {
      systemTime = timeObj["currentTime"].as<String>();
    }
    if (timeObj.containsKey("currentDate")) {
      systemDate = timeObj["currentDate"].as<String>();
    }
  }
  
  // Update limits
  if (doc.containsKey("limits")) {
    JsonObject limits = doc["limits"];
    if (limits.containsKey("maxLayers")) {
      maxLayers = limits["maxLayers"];
    }
    if (limits.containsKey("maxButtons")) {
      maxButtons = limits["maxButtons"];
    }
  }
  
  // Update current layer
  if (doc.containsKey("currentLayer")) {
    currentLayer = doc["currentLayer"];
  }
  
  // Count layers from layers array
  if (doc.containsKey("layers")) {
    JsonArray layers = doc["layers"];
    maxLayers = layers.size();
  }
  
  // Save to file
  File file = SD.open(configFilePath, FILE_WRITE);
  if (!file) {
    DEBUG_PRINTLN("PADAWAN: Failed to open config file for writing");
    Serial.println("UPLOAD_FAIL: File write error");
    return;
  }
  
  serializeJson(doc, file);
  file.close();
  
  DEBUG_PRINTLN("PADAWAN: Configuration saved");
}

void downloadConfig(bool useCurrentConfigPrefix) {
  if (!sdAvailable) {
    Serial.println("DOWNLOAD_ERROR: SD card not available");
    return;
  }
  
  File file = SD.open(configFilePath, FILE_READ);
  if (!file) {
    Serial.println("DOWNLOAD_ERROR: Config file not found");
    return;
  }
  
  if (useCurrentConfigPrefix) {
    Serial.print("CURRENT_CONFIG:");
  } else {
    Serial.print("CONFIG:");
  }
  
  while (file.available()) {
    Serial.write(file.read());
  }
  Serial.println();
  file.close();
}

// ===== BATTERY STATUS =====
String getBatteryStatus() {
  // Read battery voltage from ADC pin IO2 (ADC1 CH1)
  // ESP32-S3 ADC is 12-bit (0-4095), but we need to account for attenuation
  // The voltage divider on FeatherS3 delivers ~1.1V to ADC at max capacity
  
  int adcValue = analogRead(BATTERY_ADC_PIN);
  
  // Formula from feathers3.py: voltage = (adc_value / 5371)
  // This gives us the battery voltage (approximately 4.2V max for 1S LiPo)
  float voltage = (float)adcValue / 5371.0;
  
  // Calculate percentage: (voltage * 100 - 320)
  // Clamp to valid range (0-100)
  int percentage = (int)(voltage * 100.0 - 320.0);
  if (percentage < 0) percentage = 0;
  if (percentage > 100) percentage = 100;
  
  // Determine status
  String status = "unknown";
  if (percentage >= 80) {
    status = "good";
  } else if (percentage >= 50) {
    status = "ok";
  } else if (percentage >= 20) {
    status = "low";
  } else {
    status = "critical";
  }
  
  // Format: BATTERY:percentage,voltage,status
  // Voltage in format like "4.20" (2 decimal places)
  char voltageStr[10];
  dtostrf(voltage, 4, 2, voltageStr);
  
  return "BATTERY:" + String(percentage) + "," + String(voltageStr) + "," + status;
}

// ===== BUTTON HANDLING =====
void checkButtons() {
  int buttonPins[6] = {BUTTON_1_PIN, BUTTON_2_PIN, BUTTON_3_PIN, 
                       BUTTON_4_PIN, BUTTON_5_PIN, BUTTON_6_PIN};
  
  for (int i = 0; i < 6; i++) {
    bool currentState = !digitalRead(buttonPins[i]); // Inverted because of pull-up
    
    if (currentState && !buttonStates[i]) {
      // Button pressed (with debounce)
      if (millis() - buttonLastPress[i] > DEBOUNCE_DELAY) {
        buttonStates[i] = true;
        buttonLastPress[i] = millis();
        handleButtonPress(i + 1); // Button IDs are 1-based
      }
    } else if (!currentState && buttonStates[i]) {
      // Button released
      buttonStates[i] = false;
    }
  }
}

void handleButtonPress(int buttonId) {
  DEBUG_PRINTLN("Button " + String(buttonId) + " pressed");
  
  if (!sdAvailable) {
    return;
  }
  
  // Load configuration and get button action
  File file = SD.open(configFilePath, FILE_READ);
  if (!file) {
    return;
  }
  
  String jsonString = "";
  while (file.available()) {
    jsonString += (char)file.read();
  }
  file.close();
  
  DynamicJsonDocument doc(16384);
  DeserializationError error = deserializeJson(doc, jsonString);
  
  if (error) {
    DEBUG_PRINTLN("PADAWAN: JSON parse error in button handler");
    return;
  }
  
  // Find current layer
  if (!doc.containsKey("layers")) {
    return;
  }
  
  JsonArray layers = doc["layers"];
  int layerIndex = currentLayer - 1;
  
  if (layerIndex < 0 || layerIndex >= layers.size()) {
    return;
  }
  
  JsonObject layer = layers[layerIndex];
  if (!layer.containsKey("buttons")) {
    return;
  }
  
  JsonObject buttons = layer["buttons"];
  String buttonKey = String(buttonId);
  
  if (!buttons.containsKey(buttonKey)) {
    return;
  }
  
  JsonObject buttonConfig = buttons[buttonKey];
  bool enabled = buttonConfig.containsKey("enabled") ? buttonConfig["enabled"].as<bool>() : true;
  String action = buttonConfig.containsKey("action") ? buttonConfig["action"].as<String>() : "None";
  String keyValue = buttonConfig.containsKey("key") ? buttonConfig["key"].as<String>() : "";
  
  if (!enabled || action == "None") {
    return;
  }
  
  executeButtonAction(action, keyValue);
}

void executeButtonAction(String action, String keyValue) {
  DEBUG_PRINTLN("Executing button action: " + action + " - " + keyValue);
  DEBUG_PRINTLN("KeyValue length: " + String(keyValue.length()));
  DEBUG_PRINTLN("KeyValue bytes: ");
  for (int i = 0; i < keyValue.length(); i++) {
    DEBUG_PRINT(String((int)keyValue[i]) + " ");
  }
  DEBUG_PRINTLN("");
  
  if (action == "Type Text") {
    // NUR für echten Text - NIE für Keycodes, Modifier, oder Sondertasten!
    DEBUG_PRINTLN("Writing text: '" + keyValue + "'");
    DEBUG_PRINTLN("Text length: " + String(keyValue.length()));
    
    // Verwende Keyboard.write() direkt für ASCII-Zeichen
    // keyboardLayout.write() scheint nicht zu funktionieren, daher direkter Ansatz
    for (unsigned int i = 0; i < keyValue.length(); i++) {
      char c = keyValue[i];
      DEBUG_PRINTLN("Sending char: '" + String(c) + "' (ASCII: " + String((int)c) + ")");
      Keyboard.write(c);
      delay(20); // Small delay between characters
    }
  } else if (action == "Special Key") {
    if (keyValue.length() > 0) {
      uint8_t keycode = getKeycode(keyValue);
      if (keycode != 0) {
        DEBUG_PRINTLN("Button pressing special key: " + keyValue + " (code: 0x" + String(keycode, HEX) + ")");
        
        // Modifier keys need special handling - they're sent as modifier bits, not keycodes
        if (keycode == 0xE3) {
          // Windows Key (Left GUI)
          uint8_t hidReport[8] = {0, 0, 0, 0, 0, 0, 0, 0};
          hidReport[0] = 0x08; // Left GUI modifier bit
          tud_hid_report(1, hidReport, 8);
          delay(100);
          memset(hidReport, 0, 8);
          tud_hid_report(1, hidReport, 8);
          delay(20);
          DEBUG_PRINTLN("Windows Key sent via TinyUSB (Report ID 1)");
        } else if (keycode == 0xE0) {
          // Left Control
          uint8_t hidReport[8] = {0, 0, 0, 0, 0, 0, 0, 0};
          hidReport[0] = 0x01; // Left Control modifier bit
          tud_hid_report(1, hidReport, 8);
          delay(100);
          memset(hidReport, 0, 8);
          tud_hid_report(1, hidReport, 8);
          delay(20);
          DEBUG_PRINTLN("Left Control sent via TinyUSB (Report ID 1)");
        } else if (keycode == 0xE1) {
          // Left Shift
          uint8_t hidReport[8] = {0, 0, 0, 0, 0, 0, 0, 0};
          hidReport[0] = 0x02; // Left Shift modifier bit
          tud_hid_report(1, hidReport, 8);
          delay(100);
          memset(hidReport, 0, 8);
          tud_hid_report(1, hidReport, 8);
          delay(20);
          DEBUG_PRINTLN("Left Shift sent via TinyUSB (Report ID 1)");
        } else if (keycode == 0xE2) {
          // Left Alt
          uint8_t hidReport[8] = {0, 0, 0, 0, 0, 0, 0, 0};
          hidReport[0] = 0x04; // Left Alt modifier bit
          tud_hid_report(1, hidReport, 8);
          delay(100);
          memset(hidReport, 0, 8);
          tud_hid_report(1, hidReport, 8);
          delay(20);
          DEBUG_PRINTLN("Left Alt sent via TinyUSB (Report ID 1)");
        } else if (keycode == 0xE6) {
          // Right Alt (AltGr)
          uint8_t hidReport[8] = {0, 0, 0, 0, 0, 0, 0, 0};
          hidReport[0] = 0x40; // Right Alt modifier bit
          tud_hid_report(1, hidReport, 8);
          delay(100);
          memset(hidReport, 0, 8);
          tud_hid_report(1, hidReport, 8);
          delay(20);
          DEBUG_PRINTLN("Right Alt (AltGr) sent via TinyUSB (Report ID 1)");
        } else {
          // Alle anderen Keys über TinyUSB direkt senden (Keyboard.press() funktioniert nicht zuverlässig auf ESP32-S3)
          uint8_t hidReport[8] = {0, 0, 0, 0, 0, 0, 0, 0};
          hidReport[2] = keycode; // Erster Keycode-Slot (Byte 1 ist Reserved)
          tud_hid_report(1, hidReport, 8); // Report ID 1 für Keyboard
          // Arrow keys need longer delay
          delay((keycode == 0x52 || keycode == 0x51) ? 100 : 50);
          // Release
          memset(hidReport, 0, 8);
          tud_hid_report(1, hidReport, 8);
          delay(20);
          DEBUG_PRINTLN("Special key sent via TinyUSB (keycode: 0x" + String(keycode, HEX) + ", Report ID 1)");
        }
      } else {
        DEBUG_PRINTLN("Button: Unknown special key: " + keyValue);
      }
    } else {
      DEBUG_PRINTLN("Button: Special Key action but no key value provided");
    }
  } else if (action == "Key combo") {
    if (keyValue.length() > 0) {
      executeKeyCombo(keyValue);
    } else {
      DEBUG_PRINTLN("Button: Key combo action but no combo string provided");
    }
  } else if (action == "Volume Control") {
    uint16_t consumerCode = getConsumerCode(keyValue);
    if (consumerCode != 0) {
      ConsumerControl.press(consumerCode);
      ConsumerControl.release();
    }
  } else if (action == "Layer Switch") {
    currentLayer++;
    if (currentLayer > maxLayers) {
      currentLayer = 1;
    }
    updateDisplayMode();
    DEBUG_PRINTLN("Switched to layer " + String(currentLayer));
  }
}

// ===== ROTARY ENCODER HANDLING =====
void checkRotaryButtons() {
  bool rotaryAState = !digitalRead(ROTARY_A_BUTTON_PIN);
  bool rotaryBState = !digitalRead(ROTARY_B_BUTTON_PIN);
  
  if (rotaryAState && !rotaryAButtonState) {
    if (millis() - rotaryAButtonLastPress > DEBOUNCE_DELAY) {
      rotaryAButtonState = true;
      rotaryAButtonLastPress = millis();
      handleRotaryPress("A");
    }
  } else if (!rotaryAState) {
    rotaryAButtonState = false;
  }
  
  if (rotaryBState && !rotaryBButtonState) {
    if (millis() - rotaryBButtonLastPress > DEBOUNCE_DELAY) {
      rotaryBButtonState = true;
      rotaryBButtonLastPress = millis();
      handleRotaryPress("B");
    }
  } else if (!rotaryBState) {
    rotaryBButtonState = false;
  }
}

void handleRotaryPress(String knobLetter) {
  DEBUG_PRINTLN("Rotary " + knobLetter + " - Press");
  
  if (!sdAvailable) {
    return;
  }
  
  // Load configuration
  File file = SD.open(configFilePath, FILE_READ);
  if (!file) {
    return;
  }
  
  String jsonString = "";
  while (file.available()) {
    jsonString += (char)file.read();
  }
  file.close();
  
  DynamicJsonDocument doc(16384);
  DeserializationError error = deserializeJson(doc, jsonString);
  
  if (error) {
    return;
  }
  
  // Find current layer
  if (!doc.containsKey("layers")) {
    return;
  }
  
  JsonArray layers = doc["layers"];
  int layerIndex = currentLayer - 1;
  
  if (layerIndex < 0 || layerIndex >= layers.size()) {
    return;
  }
  
  JsonObject layer = layers[layerIndex];
  if (!layer.containsKey("knobs")) {
    return;
  }
  
  JsonObject knobs = layer["knobs"];
  if (!knobs.containsKey(knobLetter)) {
    return;
  }
  
  JsonObject knobConfig = knobs[knobLetter];
  String pressAction = knobConfig.containsKey("pressAction") ? knobConfig["pressAction"].as<String>() : "None";
  
  // Handle pressKey - it might be null in JSON, so check for both null and empty string
  String pressKey = "";
  if (knobConfig.containsKey("pressKey")) {
    if (knobConfig["pressKey"].is<const char*>()) {
      pressKey = knobConfig["pressKey"].as<String>();
      pressKey.trim(); // Remove whitespace
    } else if (knobConfig["pressKey"].isNull()) {
      pressKey = "";
    } else if (knobConfig["pressKey"].is<String>()) {
      pressKey = knobConfig["pressKey"].as<String>();
      pressKey.trim(); // Remove whitespace
    }
  }
  
  DEBUG_PRINTLN("Rotary press - Action: '" + pressAction + "', Key: '" + pressKey + "' (length: " + String(pressKey.length()) + ")");
  executeKnobAction(pressAction, pressKey);
}

void handleRotaryRotation(String knobLetter, String direction) {
  DEBUG_PRINTLN("Rotary " + knobLetter + " - " + direction);
  
  if (!sdAvailable) {
    return;
  }
  
  // Load configuration
  File file = SD.open(configFilePath, FILE_READ);
  if (!file) {
    return;
  }
  
  String jsonString = "";
  while (file.available()) {
    jsonString += (char)file.read();
  }
  file.close();
  
  DynamicJsonDocument doc(16384);
  DeserializationError error = deserializeJson(doc, jsonString);
  
  if (error) {
    return;
  }
  
  // Find current layer
  if (!doc.containsKey("layers")) {
    return;
  }
  
  JsonArray layers = doc["layers"];
  int layerIndex = currentLayer - 1;
  
  if (layerIndex < 0 || layerIndex >= layers.size()) {
    return;
  }
  
  JsonObject layer = layers[layerIndex];
  if (!layer.containsKey("knobs")) {
    return;
  }
  
  JsonObject knobs = layer["knobs"];
  if (!knobs.containsKey(knobLetter)) {
    return;
  }
  
  JsonObject knobConfig = knobs[knobLetter];
  String action = direction == "cw" 
    ? (knobConfig.containsKey("cwAction") ? knobConfig["cwAction"].as<String>() : "None")
    : (knobConfig.containsKey("ccwAction") ? knobConfig["ccwAction"].as<String>() : "None");
  
  // Get the key value for this direction - handle null values properly
  String keyValue = "";
  if (direction == "cw") {
    if (knobConfig.containsKey("cwKey")) {
      if (knobConfig["cwKey"].is<const char*>()) {
        keyValue = knobConfig["cwKey"].as<String>();
      } else if (knobConfig["cwKey"].isNull()) {
        keyValue = "";
      }
    }
  } else {
    if (knobConfig.containsKey("ccwKey")) {
      if (knobConfig["ccwKey"].is<const char*>()) {
        keyValue = knobConfig["ccwKey"].as<String>();
      } else if (knobConfig["ccwKey"].isNull()) {
        keyValue = "";
      }
    }
  }
  
  DEBUG_PRINTLN("Rotary rotation - Direction: " + direction + ", Action: " + action + ", Key: '" + keyValue + "'");
  executeKnobAction(action, keyValue);
}

void executeKnobAction(String action, String keyValue) {
  if (action == "None" || action.length() == 0) {
    DEBUG_PRINTLN("executeKnobAction: Action is None or empty, returning");
    return;
  }
  
  DEBUG_PRINTLN("Executing knob action: '" + action + "', keyValue: '" + keyValue + "' (length: " + String(keyValue.length()) + ")");
  
  if (action == "Increase Volume") {
    ConsumerControl.press(0xE9); // VOLUME_INCREMENT
    ConsumerControl.release();
  } else if (action == "Decrease Volume") {
    ConsumerControl.press(0xEA); // VOLUME_DECREMENT
    ConsumerControl.release();
  } else if (action == "Scroll Up") {
    // UP_ARROW = 0x52 (from CircuitPython keycode_win_de.py)
    // Verwende TinyUSB direkt, da Keyboard.press() auf ESP32-S3 Keycodes falsch interpretieren kann
    uint8_t keycode = 0x52; // UP_ARROW
    uint8_t hidReport[8] = {0, 0, 0, 0, 0, 0, 0, 0};
    hidReport[2] = keycode; // Erster Keycode-Slot (Byte 1 ist Reserved)
    tud_hid_report(1, hidReport, 8); // Report ID 1 für Keyboard
    delay(50);
    // Release
    memset(hidReport, 0, 8);
    tud_hid_report(1, hidReport, 8);
    delay(20);
    DEBUG_PRINTLN("Scroll Up executed via TinyUSB (keycode: 0x52, Report ID 1)");
  } else if (action == "Scroll Down") {
    // DOWN_ARROW = 0x51 (from CircuitPython keycode_win_de.py)
    // Verwende TinyUSB direkt, da Keyboard.press() auf ESP32-S3 Keycodes falsch interpretieren kann
    uint8_t keycode = 0x51; // DOWN_ARROW
    uint8_t hidReport[8] = {0, 0, 0, 0, 0, 0, 0, 0};
    hidReport[2] = keycode; // Erster Keycode-Slot (Byte 1 ist Reserved)
    tud_hid_report(1, hidReport, 8); // Report ID 1 für Keyboard
    delay(50);
    // Release
    memset(hidReport, 0, 8);
    tud_hid_report(1, hidReport, 8);
    delay(20);
    DEBUG_PRINTLN("Scroll Down executed via TinyUSB (keycode: 0x51, Report ID 1)");
  } else if (action == "Layer Switch" || action == "Switch Layer") {
    currentLayer++;
    if (currentLayer > maxLayers) {
      currentLayer = 1;
    }
    updateDisplayMode();
    DEBUG_PRINTLN("Switched to layer " + String(currentLayer));
  } else if (action == "Type Text") {
    // NUR für echten Text - NIE für Keycodes, Modifier, oder Sondertasten!
    DEBUG_PRINTLN("Type Text action detected, keyValue length: " + String(keyValue.length()));
    if (keyValue.length() > 0) {
      DEBUG_PRINTLN("Typing text: '" + keyValue + "' (length: " + String(keyValue.length()) + ")");
      // Verwende Keyboard.write() direkt für ASCII-Zeichen
      for (unsigned int i = 0; i < keyValue.length(); i++) {
        char c = keyValue[i];
        DEBUG_PRINTLN("Sending char: '" + String(c) + "' (ASCII: " + String((int)c) + ")");
        Keyboard.write(c);
        delay(20); // Small delay between characters
      }
      DEBUG_PRINTLN("Type Text execution completed");
    } else {
      DEBUG_PRINTLN("Type Text action but no text provided (keyValue is empty)");
    }
  } else if (action == "Special Key") {
    if (keyValue.length() > 0) {
      uint8_t keycode = getKeycode(keyValue);
      if (keycode != 0) {
        DEBUG_PRINTLN("Knob pressing special key: " + keyValue + " (code: 0x" + String(keycode, HEX) + ")");
        
        // Modifier keys need special handling - they're sent as modifier bits, not keycodes
        if (keycode == 0xE3) {
          // Windows Key (Left GUI)
          uint8_t hidReport[8] = {0, 0, 0, 0, 0, 0, 0, 0};
          hidReport[0] = 0x08; // Left GUI modifier bit
          tud_hid_report(1, hidReport, 8);
          delay(100);
          memset(hidReport, 0, 8);
          tud_hid_report(1, hidReport, 8);
          delay(20);
          DEBUG_PRINTLN("Windows Key sent via TinyUSB (Report ID 1)");
        } else if (keycode == 0xE0) {
          // Left Control
          uint8_t hidReport[8] = {0, 0, 0, 0, 0, 0, 0, 0};
          hidReport[0] = 0x01; // Left Control modifier bit
          tud_hid_report(1, hidReport, 8);
          delay(100);
          memset(hidReport, 0, 8);
          tud_hid_report(1, hidReport, 8);
          delay(20);
          DEBUG_PRINTLN("Left Control sent via TinyUSB (Report ID 1)");
        } else if (keycode == 0xE1) {
          // Left Shift
          uint8_t hidReport[8] = {0, 0, 0, 0, 0, 0, 0, 0};
          hidReport[0] = 0x02; // Left Shift modifier bit
          tud_hid_report(1, hidReport, 8);
          delay(100);
          memset(hidReport, 0, 8);
          tud_hid_report(1, hidReport, 8);
          delay(20);
          DEBUG_PRINTLN("Left Shift sent via TinyUSB (Report ID 1)");
        } else if (keycode == 0xE2) {
          // Left Alt
          uint8_t hidReport[8] = {0, 0, 0, 0, 0, 0, 0, 0};
          hidReport[0] = 0x04; // Left Alt modifier bit
          tud_hid_report(1, hidReport, 8);
          delay(100);
          memset(hidReport, 0, 8);
          tud_hid_report(1, hidReport, 8);
          delay(20);
          DEBUG_PRINTLN("Left Alt sent via TinyUSB (Report ID 1)");
        } else if (keycode == 0xE6) {
          // Right Alt (AltGr)
          uint8_t hidReport[8] = {0, 0, 0, 0, 0, 0, 0, 0};
          hidReport[0] = 0x40; // Right Alt modifier bit
          tud_hid_report(1, hidReport, 8);
          delay(100);
          memset(hidReport, 0, 8);
          tud_hid_report(1, hidReport, 8);
          delay(20);
          DEBUG_PRINTLN("Right Alt (AltGr) sent via TinyUSB (Report ID 1)");
        } else {
          // Alle anderen Keys über TinyUSB direkt senden (Keyboard.press() funktioniert nicht zuverlässig auf ESP32-S3)
          uint8_t hidReport[8] = {0, 0, 0, 0, 0, 0, 0, 0};
          hidReport[2] = keycode; // Erster Keycode-Slot (Byte 1 ist Reserved)
          tud_hid_report(1, hidReport, 8); // Report ID 1 für Keyboard
          // Arrow keys need longer delay
          delay((keycode == 0x52 || keycode == 0x51) ? 100 : 50);
          // Release
          memset(hidReport, 0, 8);
          tud_hid_report(1, hidReport, 8);
          delay(20);
          DEBUG_PRINTLN("Special key sent via TinyUSB (keycode: 0x" + String(keycode, HEX) + ", Report ID 1)");
        }
      } else {
        DEBUG_PRINTLN("Knob: Unknown special key: " + keyValue);
      }
    } else {
      DEBUG_PRINTLN("Knob: Special Key action but no key value provided");
    }
  } else if (action == "Key combo") {
    if (keyValue.length() > 0) {
      executeKeyCombo(keyValue);
    } else {
      DEBUG_PRINTLN("Knob: Key combo action but no combo string provided");
    }
  }
}

// ===== KEY COMBO EXECUTION =====
void executeKeyCombo(String comboString) {
  DEBUG_PRINTLN("Parsing key combo: " + comboString);
  
  // Parse combo like "Ctrl+C" or "Alt+Tab"
  int plusPos = comboString.indexOf('+');
  if (plusPos < 0) {
    DEBUG_PRINTLN("No '+' found in combo string");
    return;
  }
  
  String modifierStr = comboString.substring(0, plusPos);
  modifierStr.trim();
  modifierStr.toUpperCase();
  
  String keyStr = comboString.substring(plusPos + 1);
  keyStr.trim();
  keyStr.toUpperCase();
  
  DEBUG_PRINTLN("Modifier: '" + modifierStr + "', Key: '" + keyStr + "'");
  
  uint8_t modifier = 0;
  if (modifierStr == "CTRL" || modifierStr == "CONTROL") {
    modifier = 0x01; // Left Control
  } else if (modifierStr == "SHIFT") {
    modifier = 0x02; // Left Shift
  } else if (modifierStr == "ALT") {
    modifier = 0x04; // Left Alt
  } else if (modifierStr == "WIN" || modifierStr == "WINDOWS" || modifierStr == "WINDOWS KEY") {
    modifier = 0x08; // Left GUI (bit 3)
  }
  
  DEBUG_PRINTLN("Parsed modifier: 0x" + String(modifier, HEX) + " for '" + modifierStr + "'");
  
  uint8_t keycode = getKeycode(keyStr);
  
  DEBUG_PRINTLN("Modifier code: " + String(modifier) + ", Keycode: " + String(keycode));
  
  if (modifier != 0 && keycode != 0) {
    DEBUG_PRINTLN("Executing key combo - modifier: 0x" + String(modifier, HEX) + ", keycode: 0x" + String(keycode, HEX));
    
    // Modifier müssen im Modifier-Byte gesetzt werden, nicht als normale Keycodes
    // Verwende TinyUSB direkt für korrekte HID-Report-Struktur
    // Report ID 1 ist normalerweise für Keyboard Reports
    if (tud_hid_ready()) {
      uint8_t hidReport[8] = {0, 0, 0, 0, 0, 0, 0, 0};
      hidReport[0] = modifier; // Modifier-Byte (bit 0 = Left Ctrl, bit 1 = Left Shift, bit 2 = Left Alt, bit 3 = Left GUI)
      hidReport[2] = keycode;  // Erster Keycode-Slot (Byte 1 ist Reserved)
      tud_hid_report(1, hidReport, 8); // Report ID 1 für Keyboard
      delay(100); // Länger halten für zuverlässigere Erkennung
      // Release
      memset(hidReport, 0, 8);
      tud_hid_report(1, hidReport, 8);
      delay(20);
      DEBUG_PRINTLN("Key combo executed via TinyUSB (Report ID 1)");
    } else {
      // Fallback: Verwende USBHIDKeyboard Library mit korrekter Modifier-Behandlung
      // Die Library unterstützt Modifier über press() mit Modifier-Keycodes
      // Aber wir müssen es manuell machen, da press() nur einen Keycode akzeptiert
      DEBUG_PRINTLN("TinyUSB not ready, trying alternative method...");
      
      // Alternative: Verwende Keyboard.press() mit Modifier als erstes Argument
      // Aber USBHIDKeyboard unterstützt das nicht direkt, daher müssen wir
      // die HID Reports manuell senden, auch wenn tud_hid_ready() false ist
      
      // Versuche es trotzdem mit tud_hid_report, auch wenn ready() false ist
      uint8_t hidReport[8] = {0, 0, 0, 0, 0, 0, 0, 0};
      hidReport[0] = modifier;
      hidReport[2] = keycode;
      tud_hid_report(1, hidReport, 8);
      delay(100);
      memset(hidReport, 0, 8);
      tud_hid_report(1, hidReport, 8);
      delay(20);
      DEBUG_PRINTLN("Key combo executed via TinyUSB (forced, Report ID 1)");
    }
  } else {
    DEBUG_PRINTLN("Key combo failed - modifier or keycode is 0");
    DEBUG_PRINTLN("  Modifier: " + String(modifier) + ", Keycode: " + String(keycode));
    if (modifier == 0) {
      DEBUG_PRINTLN("  ERROR: Modifier not recognized: '" + modifierStr + "'");
    }
    if (keycode == 0) {
      DEBUG_PRINTLN("  ERROR: Keycode not found for: '" + keyStr + "'");
    }
  }
}

// ===== KEYCODE MAPPING =====
uint8_t getKeycode(String key) {
  key.toUpperCase();
  
  // Letters
  if (key.length() == 1 && key[0] >= 'A' && key[0] <= 'Z') {
    return 0x04 + (key[0] - 'A'); // A=0x04, B=0x05, etc.
  }
  
  // Numbers
  if (key == "0") return 0x27;
  if (key == "1") return 0x1E;
  if (key == "2") return 0x1F;
  if (key == "3") return 0x20;
  if (key == "4") return 0x21;
  if (key == "5") return 0x22;
  if (key == "6") return 0x23;
  if (key == "7") return 0x24;
  if (key == "8") return 0x25;
  if (key == "9") return 0x26;
  
  // Special keys
  if (key == "ESCAPE" || key == "ESC") return 0x29;
  if (key == "ENTER") return 0x28;
  if (key == "TAB") return 0x2B;
  if (key == "SPACE" || key == "SPACEBAR") return 0x2C;
  if (key == "BACKSPACE") return 0x2A;
  if (key == "DELETE" || key == "DEL") return 0x4C;
  if (key == "HOME") return 0x4A;
  if (key == "END") return 0x4D;
  if (key == "PAGE UP" || key == "PAGEUP") return 0x4B;
  if (key == "PAGE DOWN" || key == "PAGEDOWN") return 0x4E;
  if (key == "ARROW UP" || key == "UP" || key == "UP_ARROW") return 0x52;
  if (key == "ARROW DOWN" || key == "DOWN" || key == "DOWN_ARROW") return 0x51;
  if (key == "ARROW LEFT" || key == "LEFT" || key == "LEFT_ARROW") return 0x50;
  if (key == "ARROW RIGHT" || key == "RIGHT" || key == "RIGHT_ARROW") return 0x4F;
  if (key == "F1") return 0x3A;
  if (key == "F2") return 0x3B;
  if (key == "F3") return 0x3C;
  if (key == "F4") return 0x3D;
  if (key == "F5") return 0x3E;
  if (key == "F6") return 0x3F;
  if (key == "F7") return 0x40;
  if (key == "F8") return 0x41;
  if (key == "F9") return 0x42;
  if (key == "F10") return 0x43;
  if (key == "F11") return 0x44;
  if (key == "F12") return 0x45;
  if (key == "WINDOWS KEY" || key == "WINDOWS" || key == "WIN") return 0xE3; // Left GUI
  if (key == "MENU KEY" || key == "MENU" || key == "APPLICATION") return 0x65;
  
  // Modifier keys (these need special handling - they're not regular keycodes)
  if (key == "CTRL" || key == "CONTROL" || key == "LEFT CTRL" || key == "LEFT CONTROL") return 0xE0; // Left Control (special marker)
  if (key == "SHIFT" || key == "LEFT SHIFT") return 0xE1; // Left Shift (special marker)
  if (key == "ALT" || key == "LEFT ALT") return 0xE2; // Left Alt (special marker)
  if (key == "ALT GR" || key == "ALTGR" || key == "RIGHT ALT") return 0xE6; // Right Alt (AltGr)
  
  return 0;
}

uint16_t getConsumerCode(String control) {
  control.toUpperCase();
  
  if (control == "VOLUME UP") return 0xE9;
  if (control == "VOLUME DOWN") return 0xEA;
  if (control == "MUTE") return 0xE2;
  if (control == "PLAY/PAUSE" || control == "PLAY_PAUSE") return 0xCD;
  if (control == "NEXT TRACK" || control == "NEXT") return 0xB5;
  if (control == "PREVIOUS TRACK" || control == "PREVIOUS" || control == "PREV") return 0xB6;
  if (control == "STOP") return 0xB7;
  
  return 0;
}

// ===== DISPLAY FUNCTIONS =====
void updateDisplay(String message) {
  if (!displayEnabled) {
    return;
  }
  
  display.clearDisplay();
  display.setTextSize(2);
  display.setTextColor(SSD1306_WHITE);
  display.setCursor(10, 10);
  display.println(message);
  display.display();
}

void updateDisplayMode() {
  if (!displayEnabled || displayMode == "off") {
    display.clearDisplay();
    display.display();
    return;
  }
  
  if (displayMode == "layer") {
    updateDisplay("Layer: " + String(currentLayer));
  } else if (displayMode == "battery") {
    // Get battery percentage
    int adcValue = analogRead(BATTERY_ADC_PIN);
    float voltage = (float)adcValue / 5371.0;
    int percentage = (int)(voltage * 100.0 - 320.0);
    if (percentage < 0) percentage = 0;
    if (percentage > 100) percentage = 100;
    updateDisplay(String(percentage) + "%");
  } else if (displayMode == "time") {
    if (systemTime.length() > 0) {
      updateDisplay(systemTime);
    } else {
      updateDisplay("Time?");
    }
  }
}