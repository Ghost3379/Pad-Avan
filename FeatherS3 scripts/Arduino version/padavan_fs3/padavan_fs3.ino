/*
 * PadAwan-Force Macro Pad - Arduino Version
 * ESP32-S3 FeatherS3 Implementation
 * 
 * Serial communication, SD flashing, display, and buttons
 */

#include <USB.h>
#include <USBHIDKeyboard.h>
#include <USBHIDConsumerControl.h>
#include <SD.h>
#include <SPI.h>
#include <ArduinoJson.h>
#include <UMS3.h>
#include <string.h>
#include <Wire.h>
#include <RotaryEncoder.h>
#include "DisplayHandler.h"
#include "KeyboardLayoutWinCH.h"

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

// Display: I2C, Address 0x3C
#define SCREEN_WIDTH 128
#define SCREEN_HEIGHT 32
#define OLED_ADDRESS 0x3C

// ===== GLOBAL OBJECTS =====
USBHIDKeyboard Keyboard;
USBHIDConsumerControl ConsumerControl;
KeyboardLayoutWinCH keyboardLayout(&Keyboard);
RotaryEncoder rotaryA(ROTARY_A_PIN_A, ROTARY_A_PIN_B, RotaryEncoder::LatchMode::FOUR3);
RotaryEncoder rotaryB(ROTARY_B_PIN_A, ROTARY_B_PIN_B, RotaryEncoder::LatchMode::FOUR3);
UMS3 ums3;
DisplayHandler display(SCREEN_WIDTH, SCREEN_HEIGHT, OLED_ADDRESS);

// ===== CONFIGURATION =====
const char* generalConfigFile = "/general.json";
const char* layerConfigPrefix = "/layer"; // layer1.json, layer2.json, etc.
int currentLayer = 1;
int maxLayers = 1;
int maxButtons = 6;
String displayMode = "layer";
bool displayEnabled = true;
String systemTime = "";
String systemDate = "";

// Store current layer config in RAM
String currentLayerConfig = "";

const String FIRMWARE_VERSION = "1.2";

// ===== STATE VARIABLES =====
bool sdAvailable = false;
bool displayAvailable = false; // Track if display initialized successfully
bool uploading = false;
bool generalConfigLoaded = false; // Track if general config is loaded
bool hostHandshakeDone = false; // Track if PING/PONG handshake completed
String jsonBuffer = "";
String generalConfig = ""; // Store general config in RAM
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

// ===== FORWARD DECLARATIONS =====
void initializeSD();
void loadGeneralConfig();
void parseGeneralConfig();
void loadLayerConfig(int layerNumber);
void saveConfiguration(String jsonString);
void saveCurrentLayer();
String getLayerFileName(int layerNumber);
void downloadConfig(bool useCurrentConfigPrefix);
String getBatteryStatus();
void handleSerial();
void updateDisplay(String message);
void updateDisplayMode();
void checkButtons();
void checkRotaryButtons();
void handleButtonPress(int buttonNumber);
void handleRotaryPress(String knobLetter);
void handleRotaryRotation(String knobLetter, String direction);
void executeButtonAction(String action, String keyValue);
void executeKnobAction(String action, String keyValue);
void executeKeyCombo(String comboString);
uint8_t getKeycode(String key);
uint16_t getConsumerCode(String control);

// ===== SETUP =====
void setup() {
  // For ESP32-S3 with "CDC on Boot" enabled in Arduino IDE:
  // The USB stack (including CDC) is already initialized by the bootloader
  // We do NOT need to call USB.begin() - it would reinitialize and break CDC
  // We just need to:
  // 1. Initialize Serial to use the existing CDC
  // 2. Add HID devices to the existing USB stack
  
  Serial.begin(115200);
  
  // Wait for Serial to be ready (important for native USB)
  // Give it time to enumerate - up to 2 seconds (usually ready in ~100-500ms)
  unsigned long startTime = millis();
  while (!Serial && (millis() - startTime < 2000)) {
    delay(10);
  }
  
  // Small delay to ensure USB CDC is fully enumerated
  delay(200);
  
  // Clear any pending data to ensure clean communication
  while (Serial.available() > 0) {
    Serial.read();
  }
  
  // Initialize USB HID - this adds HID devices to the existing USB stack
  // The USB stack already supports CDC, and HID is added alongside it
  // NO USB.begin() needed - it would interfere with the bootloader-initialized USB stack
  Keyboard.begin();
  ConsumerControl.begin();
  delay(200); // Give USB stack time to add HID descriptors
  
  // Initialize I2C for display
  Wire.begin();
  Wire.setClock(100000); // 100kHz for stability
  Wire.setTimeout(100); // 100ms timeout to prevent hangs
  
  // Initialize UMS3 board peripherals (call this after Wire.begin())
  ums3.begin();
  
  // Initialize display
  if (!display.begin()) {
    // Don't print to Serial - keep protocol clean
    displayAvailable = false;
  } else {
    // Don't print to Serial - keep protocol clean
    displayAvailable = true;
    display.clearDisplay();
    display.setTextSizeDirect(2);
    display.setTextColorDirect(SSD1306_WHITE);
    display.setCursorDirect(10, 10);
    display.printlnDirect("Boot...");
    display.displayUpdate();
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
  
  // Initialize SD card
  initializeSD();
  
  // Load general configuration
  if (sdAvailable) {
    loadGeneralConfig();
  }
  
  // Load current layer configuration
  if (sdAvailable) {
    loadLayerConfig(currentLayer);
  }
  
  // Update display after initialization
  if (displayAvailable) {
    updateDisplayMode();
  }
  
  // Initialize rotary encoders
  rotaryA.setPosition(0);
  rotaryB.setPosition(0);
  
  // Read initial positions after a few ticks to ensure encoder is stable
  delay(10);
  rotaryA.tick();
  rotaryB.tick();
  rotaryALastPosition = rotaryA.getPosition();
  rotaryBLastPosition = rotaryB.getPosition();
  
  // Don't send READY here - wait for PING/PONG handshake first
  // Serial stays completely silent until host sends PING
}

// ===== MAIN LOOP =====
void loop() {
  yield(); // Feed watchdog
  
  // Keep Serial alive - flush periodically to ensure data is sent
  if (Serial && Serial.available() == 0) {
    // Only flush when not reading to avoid interfering with incoming data
    static unsigned long lastFlush = 0;
    if (millis() - lastFlush > 100) {
      Serial.flush();
      lastFlush = millis();
    }
  }
  
  // Parse general config on first loop iteration (after setup completes)
  if (!generalConfigLoaded && generalConfig.length() > 0) {
    parseGeneralConfig();
    generalConfigLoaded = true;
    yield();
  }
  
  // Handle serial communication
  handleSerial();
  
  // Update rotary encoders
  rotaryA.tick();
  rotaryB.tick();
  
  // Check rotary encoder rotation
  int newPosA = rotaryA.getPosition();
  if (newPosA != rotaryALastPosition) {
    // Swap direction: if position increases, it's actually CCW (counter-clockwise)
    // This fixes the mirrored behavior
    if (newPosA > rotaryALastPosition) {
      handleRotaryRotation("A", "ccw");
    } else {
      handleRotaryRotation("A", "cw");
    }
    rotaryALastPosition = newPosA;
  }
  
  int newPosB = rotaryB.getPosition();
  if (newPosB != rotaryBLastPosition) {
    // Swap direction: if position increases, it's actually CCW (counter-clockwise)
    // This fixes the mirrored behavior
    if (newPosB > rotaryBLastPosition) {
      handleRotaryRotation("B", "ccw");
    } else {
      handleRotaryRotation("B", "cw");
    }
    rotaryBLastPosition = newPosB;
  }
  
  // Check buttons
  checkButtons();
  
  // Check rotary encoder buttons
  checkRotaryButtons();
  
  // Update display periodically (every 2 seconds)
  static unsigned long lastDisplayUpdate = 0;
  static String lastDisplayContent = ""; // Track last displayed content to avoid unnecessary updates
  if (millis() - lastDisplayUpdate > 2000) {
    if (displayEnabled && displayAvailable) {
      String currentContent = "";
      if (displayMode == "layer") {
        currentContent = "Layer: " + String(currentLayer);
      } else if (displayMode == "battery") {
        String batteryResponse = getBatteryStatus();
        int commaPos = batteryResponse.indexOf(',');
        if (commaPos > 0) {
          currentContent = "Bat: " + batteryResponse.substring(8, commaPos) + "%";
        }
      } else if (displayMode == "time") {
        currentContent = systemTime.length() > 0 ? systemTime : "Time?";
      }
      
      // Only update display if content changed
      if (currentContent != lastDisplayContent) {
        updateDisplayMode();
        lastDisplayContent = currentContent;
      }
    }
    lastDisplayUpdate = millis();
  }
  
  delay(1);
}

// ===== SERIAL COMMUNICATION =====
void handleSerial() {
  if (Serial.available() <= 0) {
    return;
  }
  
  String line = Serial.readStringUntil('\n');
  line.trim();
  
  if (!line.length()) {
    return;
  }
  
  // PING/PONG handshake - must be first command
  if (line == "PING") {
    Serial.println("PONG");
    Serial.flush();
    if (!hostHandshakeDone) {
      hostHandshakeDone = true;
      Serial.println("READY");
      Serial.flush();
    }
    return;
  }
  
  // Ignore all commands before handshake
  if (!hostHandshakeDone) {
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


// ===== SD CARD FUNCTIONS =====
void initializeSD() {
  // Initialize SPI
  SPI.begin();
  pinMode(SD_CS_PIN, OUTPUT);
  digitalWrite(SD_CS_PIN, HIGH);
    
  // Initialize SD card
  if (!SD.begin(SD_CS_PIN)) {
    sdAvailable = false;
    // Don't print to Serial - keep protocol clean
  } else {
    sdAvailable = true;
    // Don't print to Serial - keep protocol clean
  }
}

// ===== CONFIGURATION MANAGEMENT =====
String getLayerFileName(int layerNumber) {
  return String(layerConfigPrefix) + String(layerNumber) + ".json";
}

void loadGeneralConfig() {
  if (!sdAvailable) {
    return;
  }
  
  String jsonString;
  File file = SD.open(generalConfigFile, FILE_READ);
  if (!file) {
    return;
  }
  
  jsonString = "";
  while (file.available()) {
    jsonString += (char)file.read();
  }
  file.close();
  
  generalConfig = jsonString;
}

void parseGeneralConfig() {
  if (generalConfig.length() == 0) {
    return;
  }
  
  size_t docSize = 1024; // Small buffer for general config
  DynamicJsonDocument doc(docSize);
  
  yield();
  
  DeserializationError error = deserializeJson(doc, generalConfig);
  
  yield();
  
  if (error) {
    return;
  }
  
  yield();
  
  // Parse general settings
  if (doc.containsKey("display")) {
    JsonObject displayObj = doc["display"];
    if (displayObj.containsKey("mode")) {
      displayMode = displayObj["mode"].as<String>();
    }
    if (displayObj.containsKey("enabled")) {
      displayEnabled = displayObj["enabled"].as<bool>();
    }
  }
  
  yield();
  
  if (doc.containsKey("systemTime")) {
    JsonObject timeObj = doc["systemTime"];
    if (timeObj.containsKey("currentTime")) {
      systemTime = timeObj["currentTime"].as<String>();
    }
    if (timeObj.containsKey("currentDate")) {
      systemDate = timeObj["currentDate"].as<String>();
    }
  }
  
  yield();
  
  if (doc.containsKey("limits")) {
    JsonObject limits = doc["limits"];
    if (limits.containsKey("maxLayers")) {
      maxLayers = limits["maxLayers"];
    }
    if (limits.containsKey("maxButtons")) {
      maxButtons = limits["maxButtons"];
    }
  }
  
  yield();
  
  if (doc.containsKey("currentLayer")) {
    currentLayer = doc["currentLayer"];
  }
  
  yield();
  
  // Update display settings
  if (doc.containsKey("display")) {
    display.setEnabled(displayEnabled);
  }
  
  // Update display mode after config is loaded
  if (displayAvailable) {
    updateDisplayMode();
  }
}

void loadLayerConfig(int layerNumber) {
  if (!sdAvailable) {
    return;
  }
  
  String fileName = getLayerFileName(layerNumber);
  File file = SD.open(fileName.c_str(), FILE_READ);
  if (!file) {
    currentLayerConfig = "";
    return;
  }
  
  currentLayerConfig = "";
  while (file.available()) {
    currentLayerConfig += (char)file.read();
  }
  file.close();
}

void saveConfiguration(String jsonString) {
  if (!sdAvailable) {
    Serial.println("UPLOAD_FAIL: SD card not available");
    return;
  }
  
  yield(); // Feed watchdog before parsing
  
  // Use smaller buffer if possible, but allow larger for upload
  size_t docSize = (jsonString.length() < 4096) ? 4096 : ((jsonString.length() < 8192) ? 8192 : 16384);
  DynamicJsonDocument doc(docSize);
  
  yield();
  
  DeserializationError error = deserializeJson(doc, jsonString);
  
  yield(); // Feed watchdog after parsing
  
  if (error) {
    Serial.println("UPLOAD_FAIL: " + String(error.c_str()));
    return;
  }
  
  yield();
  
  // Delete all old layer files first
  for (int i = 1; i <= 10; i++) { // Max 10 layers
    String fileName = getLayerFileName(i);
    if (SD.exists(fileName.c_str())) {
      SD.remove(fileName.c_str());
    }
    if (i % 3 == 0) { // Yield every 3 files
      yield();
    }
  }
  
  yield();
  
  // Save general config
  if (doc.containsKey("display") || doc.containsKey("systemTime") || doc.containsKey("limits") || doc.containsKey("currentLayer")) {
    DynamicJsonDocument generalDoc(1024);
    
    yield();
    
    if (doc.containsKey("display")) {
      generalDoc["display"] = doc["display"];
    }
    if (doc.containsKey("systemTime")) {
      generalDoc["systemTime"] = doc["systemTime"];
    }
    if (doc.containsKey("limits")) {
      generalDoc["limits"] = doc["limits"];
    }
    if (doc.containsKey("currentLayer")) {
      generalDoc["currentLayer"] = doc["currentLayer"];
    }
    
    yield();
    
    String generalJson;
    serializeJson(generalDoc, generalJson);
    
    yield();
    
    if (SD.exists(generalConfigFile)) {
      SD.remove(generalConfigFile);
    }
    
    File generalFile = SD.open(generalConfigFile, FILE_WRITE);
    if (generalFile) {
      generalFile.print(generalJson);
      generalFile.close();
      generalConfig = generalJson;
    }
    
    yield();
  }
  
  // Update settings from new config (minimal parsing)
  if (doc.containsKey("display")) {
    JsonObject displayObj = doc["display"];
    if (displayObj.containsKey("mode")) {
      displayMode = displayObj["mode"].as<String>();
    }
    if (displayObj.containsKey("enabled")) {
      displayEnabled = displayObj["enabled"].as<bool>();
    }
  }
  
  yield();
  
  if (doc.containsKey("systemTime")) {
    JsonObject timeObj = doc["systemTime"];
    if (timeObj.containsKey("currentTime")) {
      systemTime = timeObj["currentTime"].as<String>();
    }
    if (timeObj.containsKey("currentDate")) {
      systemDate = timeObj["currentDate"].as<String>();
    }
  }
  
  yield();
  
  if (doc.containsKey("limits")) {
    JsonObject limits = doc["limits"];
    if (limits.containsKey("maxLayers")) {
      maxLayers = limits["maxLayers"];
    }
    if (limits.containsKey("maxButtons")) {
      maxButtons = limits["maxButtons"];
    }
  }
  
  yield();
  
  if (doc.containsKey("currentLayer")) {
    currentLayer = doc["currentLayer"];
  }
  
  yield();
  
  // Save each layer to separate file
  if (doc.containsKey("layers")) {
    JsonArray layers = doc["layers"];
    maxLayers = layers.size();
    
    yield();
    
    for (int i = 0; i < layers.size(); i++) {
      JsonObject layer = layers[i];
      
      yield();
      
      String layerJson;
      serializeJson(layer, layerJson);
      
      yield();
      
      String fileName = getLayerFileName(i + 1);
      if (SD.exists(fileName.c_str())) {
        SD.remove(fileName.c_str());
      }
      
      yield();
  
      File layerFile = SD.open(fileName.c_str(), FILE_WRITE);
      if (layerFile) {
        layerFile.print(layerJson);
        layerFile.close();
      }

      yield(); // Yield after each layer
    }
    
    yield();
  
    // Reload current layer config
    loadLayerConfig(currentLayer);
  }
  
  yield();
}

void saveCurrentLayer() {
  if (!sdAvailable) {
    return;
  }
  
  yield();
  
  // Load existing general config
  String jsonString;
  File file = SD.open(generalConfigFile, FILE_READ);
  if (file) {
    jsonString = "";
    while (file.available()) {
      jsonString += (char)file.read();
    }
    file.close();
  } else {
    // If file doesn't exist, create a minimal config
    jsonString = "{}";
  }
  
  yield();
  
  // Parse existing config
  DynamicJsonDocument doc(1024);
  DeserializationError error = deserializeJson(doc, jsonString);
  
  if (error) {
    // If parsing fails, create a new document
    doc.clear();
  }
  
  yield();
  
  // Update currentLayer
  doc["currentLayer"] = currentLayer;
  
  // Preserve other settings if they exist
  if (generalConfig.length() > 0) {
    DynamicJsonDocument generalDoc(1024);
    DeserializationError generalError = deserializeJson(generalDoc, generalConfig);
    if (!generalError) {
      if (generalDoc.containsKey("display")) {
        doc["display"] = generalDoc["display"];
      }
      if (generalDoc.containsKey("systemTime")) {
        doc["systemTime"] = generalDoc["systemTime"];
      }
      if (generalDoc.containsKey("limits")) {
        doc["limits"] = generalDoc["limits"];
      }
    }
  }
  
  yield();
  
  // Write updated config back to file
  if (SD.exists(generalConfigFile)) {
    SD.remove(generalConfigFile);
  }
  
  String generalJson;
  serializeJson(doc, generalJson);
  
  File generalFile = SD.open(generalConfigFile, FILE_WRITE);
  if (generalFile) {
    generalFile.print(generalJson);
    generalFile.close();
    generalConfig = generalJson; // Update in-memory config
  }
  
  yield();
}

void downloadConfig(bool useCurrentConfigPrefix) {
  if (!sdAvailable) {
    Serial.println("DOWNLOAD_ERROR: SD card not available");
    return;
  }
  
  yield();
  
  if (useCurrentConfigPrefix) {
    Serial.print("CURRENT_CONFIG:");
  } else {
    Serial.print("CONFIG:");
  }
  
  // Send general config first
  File generalFile = SD.open(generalConfigFile, FILE_READ);
  if (generalFile) {
    int byteCount = 0;
    while (generalFile.available()) {
      Serial.write(generalFile.read());
      if (++byteCount > 100) {
        yield();
        byteCount = 0;
      }
    }
    generalFile.close();
    Serial.print(",\"layers\":[");
  }
  
  yield();
  
  // Send all layer configs
  bool firstLayer = true;
  for (int i = 1; i <= maxLayers; i++) {
    String fileName = getLayerFileName(i);
    File layerFile = SD.open(fileName.c_str(), FILE_READ);
    if (layerFile) {
      if (!firstLayer) {
        Serial.print(",");
      }
      firstLayer = false;
      
      int byteCount = 0;
      while (layerFile.available()) {
        Serial.write(layerFile.read());
        if (++byteCount > 100) {
          yield();
          byteCount = 0;
        }
      }
      layerFile.close();
    }
    yield();
  }
  
  Serial.println("]}");
  
  yield();
}

// ===== BATTERY STATUS =====
String getBatteryStatus() {
  // Get battery voltage from MAX17048 via UMS3 library
  float batteryVoltage = ums3.getBatteryVoltage();
  
  // Calculate battery percentage from voltage
  // LiPo typical range: 3.0V (empty) to 4.2V (full)
  // Formula: percentage = ((voltage - 3.0) / (4.2 - 3.0)) * 100
  float batteryPercentage = ((batteryVoltage - 3.0) / 1.2) * 100.0;
  
  // Clamp to valid range (0-100)
  if (batteryPercentage < 0) batteryPercentage = 0;
  if (batteryPercentage > 100) batteryPercentage = 100;
  
  // Check if USB power is present
  bool vbusPresent = ums3.getVbusPresent();
  
  // Determine status
  String status = vbusPresent ? "charging" : "discharging";
  
  // Format: BATTERY:percentage,voltage,status
  String response = "BATTERY:";
  response += String((int)batteryPercentage);
  response += ",";
  response += String(batteryVoltage, 2);
  response += ",";
  response += status;
  
  return response;
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
  if (currentLayerConfig.length() == 0) {
    return;
  }
  
  yield();
  
  DynamicJsonDocument doc(4096);
  DeserializationError error = deserializeJson(doc, currentLayerConfig);
  
  if (error) {
    return;
  }
  
  yield();
  
  if (!doc.containsKey("buttons")) {
    return;
  }
  
  // Buttons are stored as an object (dictionary) with keys "1", "2", "3", etc.
  JsonObject buttonsObj = doc["buttons"];
  String buttonKey = String(buttonId);
  
  if (!buttonsObj.containsKey(buttonKey)) {
    return;
  }
  
  JsonObject button = buttonsObj[buttonKey];
  
  if (!button.containsKey("enabled") || !button["enabled"]) {
    return;
  }
  
  String action = button["action"].as<String>();
  // Desktop app uses "key" not "keyValue"
  String keyValue = button.containsKey("keyValue") ? button["keyValue"].as<String>() : button["key"].as<String>();
  
  executeButtonAction(action, keyValue);
}

void executeButtonAction(String action, String keyValue) {
  yield();
  
  if (action == "Type Text") {
    // Use keyboardLayout.write() for Swiss German layout support
    keyboardLayout.write(keyValue);
  } else if (action == "Special Key") {
    // Check if it's a Consumer Control key (media/volume)
    uint16_t consumerCode = getConsumerCode(keyValue);
    if (consumerCode != 0) {
      // It's a media/volume control - use ConsumerControl
      ConsumerControl.press(consumerCode);
      ConsumerControl.release();
    } else {
      // Use keyboardLayout.pressKey() for layout-independent special keys
      keyboardLayout.pressKey(keyValue);
      delay(10); // Small delay to ensure key is registered
    }
  } else if (action == "Key combo") {
    if (keyValue.length() > 0) {
      executeKeyCombo(keyValue);
    }
  } else if (action == "Layer Switch") {
    currentLayer++;
    if (currentLayer > maxLayers) {
      currentLayer = 1;
    }
    loadLayerConfig(currentLayer);
    saveCurrentLayer(); // Save the new current layer to general.json
    if (displayAvailable) {
      updateDisplayMode();
    }
  } else if (action == "Volume Control") {
    uint16_t consumerCode = getConsumerCode(keyValue);
    if (consumerCode != 0) {
      ConsumerControl.press(consumerCode);
      ConsumerControl.release();
    }
  }
}

// ===== ROTARY ENCODER HANDLING =====
void checkRotaryButtons() {
  bool rotaryAState = !digitalRead(ROTARY_A_BUTTON_PIN); // Inverted because of INPUT_PULLUP (pressed = LOW)
  bool rotaryBState = !digitalRead(ROTARY_B_BUTTON_PIN); // Inverted because of INPUT_PULLUP (pressed = LOW)
  
  // Check for Rotary A button press (transition from not pressed to pressed)
  if (rotaryAState && !rotaryAButtonState) {
    if (millis() - rotaryAButtonLastPress > DEBOUNCE_DELAY) {
      rotaryAButtonState = true;
      rotaryAButtonLastPress = millis();
      handleRotaryPress("A");
    }
  } 
  // Check for Rotary A button release (transition from pressed to not pressed)
  else if (!rotaryAState && rotaryAButtonState) {
    rotaryAButtonState = false;
  }
  
  // Check for Rotary B button press (transition from not pressed to pressed)
  if (rotaryBState && !rotaryBButtonState) {
    if (millis() - rotaryBButtonLastPress > DEBOUNCE_DELAY) {
      rotaryBButtonState = true;
      rotaryBButtonLastPress = millis();
      handleRotaryPress("B");
    }
  } 
  // Check for Rotary B button release (transition from pressed to not pressed)
  else if (!rotaryBState && rotaryBButtonState) {
    rotaryBButtonState = false;
  }
}

void handleRotaryPress(String knobLetter) {
  if (currentLayerConfig.length() == 0) {
    return;
  }
  
  yield();
  
  DynamicJsonDocument doc(4096);
  DeserializationError error = deserializeJson(doc, currentLayerConfig);
  
  if (error) {
    return;
  }
  
  yield();
  
  if (!doc.containsKey("knobs")) {
    return;
  }
  
  JsonObject knobs = doc["knobs"];
  if (!knobs.containsKey(knobLetter)) {
    return;
  }
  
  JsonObject knobConfig = knobs[knobLetter];
  String pressAction = knobConfig.containsKey("pressAction") ? knobConfig["pressAction"].as<String>() : "None";
  
  String pressKey = "";
  if (knobConfig.containsKey("pressKey")) {
    if (knobConfig["pressKey"].is<const char*>()) {
      pressKey = knobConfig["pressKey"].as<String>();
      pressKey.trim();
    } else if (knobConfig["pressKey"].isNull()) {
      pressKey = "";
    } else if (knobConfig["pressKey"].is<String>()) {
      pressKey = knobConfig["pressKey"].as<String>();
      pressKey.trim();
    }
  }
  
  executeKnobAction(pressAction, pressKey);
}

void handleRotaryRotation(String knobLetter, String direction) {
  if (currentLayerConfig.length() == 0) {
    return;
  }
  
  yield();
  
  DynamicJsonDocument doc(4096);
  DeserializationError error = deserializeJson(doc, currentLayerConfig);
  
  if (error) {
    return;
  }
  
  yield();
  
  if (!doc.containsKey("knobs")) {
    return;
  }
  
  JsonObject knobs = doc["knobs"];
  if (!knobs.containsKey(knobLetter)) {
    return;
  }
  
  JsonObject knobConfig = knobs[knobLetter];
  String action = direction == "cw" 
    ? (knobConfig.containsKey("cwAction") ? knobConfig["cwAction"].as<String>() : "None")
    : (knobConfig.containsKey("ccwAction") ? knobConfig["ccwAction"].as<String>() : "None");
  
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
  
  executeKnobAction(action, keyValue);
}

void executeKnobAction(String action, String keyValue) {
  if (action == "None" || action.length() == 0) {
    return;
  }
  
  yield();
  
  if (action == "Type Text") {
    // Use keyboardLayout.write() for Swiss German layout support
    keyboardLayout.write(keyValue);
  } else if (action == "Special Key") {
    // Check if it's a Consumer Control key (media/volume)
    uint16_t consumerCode = getConsumerCode(keyValue);
    if (consumerCode != 0) {
      // It's a media/volume control - use ConsumerControl
      ConsumerControl.press(consumerCode);
      ConsumerControl.release();
    } else {
      // Use keyboardLayout.pressKey() for layout-independent special keys
      keyboardLayout.pressKey(keyValue);
      delay(10);
    }
  } else if (action == "Key combo") {
    if (keyValue.length() > 0) {
      executeKeyCombo(keyValue);
    }
  } else if (action == "Layer Switch") {
    currentLayer++;
    if (currentLayer > maxLayers) {
      currentLayer = 1;
    }
    loadLayerConfig(currentLayer);
    saveCurrentLayer(); // Save the new current layer to general.json
    if (displayAvailable) {
      updateDisplayMode();
    }
  } else if (action == "Increase Volume") {
    ConsumerControl.press(0xE9); // VOLUME_INCREMENT
    ConsumerControl.release();
  } else if (action == "Decrease Volume") {
    ConsumerControl.press(0xEA); // VOLUME_DECREMENT
    ConsumerControl.release();
  } else if (action == "Volume Control") {
    uint16_t consumerCode = getConsumerCode(keyValue);
    if (consumerCode != 0) {
      ConsumerControl.press(consumerCode);
      ConsumerControl.release();
    }
  } else if (action == "Scroll Up") {
    keyboardLayout.pressKey("ARROW UP");
  } else if (action == "Scroll Down") {
    keyboardLayout.pressKey("ARROW DOWN");
  }
}

// Helper function to convert modifier string to keycode
uint8_t getModifierKeycode(String modifierStr) {
  modifierStr.trim();
  modifierStr.toUpperCase();
  
  if (modifierStr == "CTRL" || modifierStr == "CONTROL") {
    return 0x80; // KEY_LEFT_CTRL
  } else if (modifierStr == "SHIFT") {
    return 0x81; // KEY_LEFT_SHIFT
  } else if (modifierStr == "ALT") {
    return 0x82; // KEY_LEFT_ALT
  } else if (modifierStr == "WIN" || modifierStr == "WINDOWS" || modifierStr == "WINDOWS KEY") {
    return 0x83; // KEY_LEFT_GUI
  } else if (modifierStr == "RIGHT CTRL" || modifierStr == "RCTRL") {
    return 0x84; // KEY_RIGHT_CTRL
  } else if (modifierStr == "RIGHT SHIFT" || modifierStr == "RSHIFT") {
    return 0x85; // KEY_RIGHT_SHIFT
  } else if (modifierStr == "RIGHT ALT" || modifierStr == "RALT" || modifierStr == "ALTGR") {
    return 0x86; // KEY_RIGHT_ALT
  } else if (modifierStr == "RIGHT GUI" || modifierStr == "RGUI") {
    return 0x87; // KEY_RIGHT_GUI
  }
  return 0; // Unknown modifier
}

// Helper function to convert key string to keycode (ASCII or library keycode)
uint8_t getComboKeycode(String keyStr) {
  keyStr.trim();
  keyStr.toUpperCase();
  
  // Check if it's a single letter (A-Z) or number (0-9) - return ASCII
  if (keyStr.length() == 1 && ((keyStr[0] >= 'A' && keyStr[0] <= 'Z') || (keyStr[0] >= '0' && keyStr[0] <= '9'))) {
    return keyStr[0]; // Return ASCII directly
  }
  
  // Map special keys to library keycodes
  if (keyStr == "ESCAPE" || keyStr == "ESC") return 0xB1;
  else if (keyStr == "ENTER") return 0xB0;
  else if (keyStr == "TAB") return 0xB3;
  else if (keyStr == "SPACE" || keyStr == "SPACEBAR") return ' ';
  else if (keyStr == "BACKSPACE") return 0xB2;
  else if (keyStr == "DELETE" || keyStr == "DEL") return 0xD4;
  else if (keyStr == "HOME") return 0xD2;
  else if (keyStr == "END") return 0xD5;
  else if (keyStr == "PAGE UP" || keyStr == "PAGEUP") return 0xD3;
  else if (keyStr == "PAGE DOWN" || keyStr == "PAGEDOWN") return 0xD6;
  else if (keyStr == "ARROW UP" || keyStr == "UP" || keyStr == "UP_ARROW") return 0xDA;
  else if (keyStr == "ARROW DOWN" || keyStr == "DOWN" || keyStr == "DOWN_ARROW") return 0xD9;
  else if (keyStr == "ARROW LEFT" || keyStr == "LEFT" || keyStr == "LEFT_ARROW") return 0xD8;
  else if (keyStr == "ARROW RIGHT" || keyStr == "RIGHT" || keyStr == "RIGHT_ARROW") return 0xD7;
  else if (keyStr == "F1") return 0xC2;
  else if (keyStr == "F2") return 0xC3;
  else if (keyStr == "F3") return 0xC4;
  else if (keyStr == "F4") return 0xC5;
  else if (keyStr == "F5") return 0xC6;
  else if (keyStr == "F6") return 0xC7;
  else if (keyStr == "F7") return 0xC8;
  else if (keyStr == "F8") return 0xC9;
  else if (keyStr == "F9") return 0xCA;
  else if (keyStr == "F10") return 0xCB;
  else if (keyStr == "F11") return 0xCC;
  else if (keyStr == "F12") return 0xCD;
  
  return 0; // Unknown key
}

// ===== KEY COMBO EXECUTION =====
void executeKeyCombo(String comboString) {
  // Parse combo like "Ctrl+C", "Ctrl+Shift+C", or "Win+Alt+Tab"
  // Support up to 3 keys: 2 modifiers + 1 key
  
  // Split by '+' to get all parts
  String parts[3];
  int partCount = 0;
  int lastPos = 0;
  
  for (int i = 0; i < comboString.length() && partCount < 3; i++) {
    if (comboString.charAt(i) == '+') {
      parts[partCount] = comboString.substring(lastPos, i);
      parts[partCount].trim();
      parts[partCount].toUpperCase();
      partCount++;
      lastPos = i + 1;
    }
  }
  // Add the last part (the actual key)
  if (lastPos < comboString.length() && partCount < 3) {
    parts[partCount] = comboString.substring(lastPos);
    parts[partCount].trim();
    parts[partCount].toUpperCase();
    partCount++;
  }
  
  if (partCount < 2) {
    return; // Need at least modifier + key
  }
  
  // The last part is always the key
  String keyStr = parts[partCount - 1];
  uint8_t keycode = getComboKeycode(keyStr);
  
  if (keycode == 0) {
    return; // Unknown key
  }
  
  // All other parts are modifiers
  // Press all modifiers first, then the key
  for (int i = 0; i < partCount - 1; i++) {
    uint8_t modifier = getModifierKeycode(parts[i]);
    if (modifier != 0) {
      Keyboard.press(modifier);
    }
  }
  
  // Press the key
  Keyboard.press(keycode);
  
  // Hold for a moment
  delay(50);
  
  // Release all
  Keyboard.releaseAll();
  delay(10);
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
  if (!displayEnabled || !displayAvailable) {
    return;
  }
  
  // Don't check connection on every update - only update if display is available
  display.showMessage(message);
}

void updateDisplayMode() {
  if (!displayEnabled || !displayAvailable || displayMode == "off") {
    if (displayAvailable) {
      display.clearAndUpdate();
    }
    return;
  }
  
  // Don't check connection on every update - only update if display is available
  
  if (displayMode == "layer") {
    updateDisplay("Layer: " + String(currentLayer));
  } else if (displayMode == "battery") {
    float batteryVoltage = ums3.getBatteryVoltage();
    float batteryPercentage = ((batteryVoltage - 3.0) / 1.2) * 100.0;
    if (batteryPercentage < 0) batteryPercentage = 0;
    if (batteryPercentage > 100) batteryPercentage = 100;
    bool vbusPresent = ums3.getVbusPresent();
    String batteryText = "Bat: " + String((int)batteryPercentage) + "%";
    if (vbusPresent) {
      batteryText += " CHG";
    }
    updateDisplay(batteryText);
  } else if (displayMode == "time") {
    if (systemTime.length() > 0) {
      updateDisplay(systemTime);
    } else {
      updateDisplay("Time?");
    }
  }
}


