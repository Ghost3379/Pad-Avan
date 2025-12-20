/*
 * PadAwan-Force Macro Pad - Arduino Version
 * ESP32-S3 FeatherS3D Implementation
 * 
 * Minimal version - Serial communication and configuration only
 * No buttons, knobs, or keypressing
 */

#include <USB.h>
#include "tusb.h"
#include <SD.h>
#include <SPI.h>
#include <ArduinoJson.h>
#include <RotaryEncoder.h>
#include <UMS3.h>
#include <string.h>
#include <Wire.h>
#include "DisplayHandler.h"

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

// LDO_2 for SD power delivery
#define LDO_2 39

// Display: I2C, Address 0x3C
#define SCREEN_WIDTH 128
#define SCREEN_HEIGHT 32
#define OLED_ADDRESS 0x3C

// ===== GLOBAL OBJECTS =====
UMS3 ums3;
DisplayHandler display(SCREEN_WIDTH, SCREEN_HEIGHT, OLED_ADDRESS);
RotaryEncoder rotaryA(ROTARY_A_PIN_A, ROTARY_A_PIN_B, RotaryEncoder::LatchMode::FOUR3);
RotaryEncoder rotaryB(ROTARY_B_PIN_A, ROTARY_B_PIN_B, RotaryEncoder::LatchMode::FOUR3);

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

const String FIRMWARE_VERSION = "1.0.3";

// ===== STATE VARIABLES =====
bool sdAvailable = false;
bool displayAvailable = false; // Track if display initialized successfully
bool uploading = false;
bool generalConfigLoaded = false; // Track if general config is loaded
String jsonBuffer = "";
String generalConfig = ""; // Store general config in RAM

// Rotary encoder positions
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

// ===== FORWARD DECLARATIONS =====
void initializeSD();
void loadGeneralConfig();
void parseGeneralConfig();
void loadLayerConfig(int layerNumber);
void saveConfiguration(String jsonString);
String getLayerFileName(int layerNumber);
void downloadConfig(bool useCurrentConfigPrefix);
String getBatteryStatus();
void handleSerial();
void updateDisplay(String message);
void updateDisplayMode();

// ===== SETUP =====
void setup() {
  Serial.begin(115200);
  
  // Wait for Serial to be ready
  while (!Serial) {
    delay(10);
  }
  
  // Clear any pending data
  while (Serial.available() > 0) {
    Serial.read();
  }
  
  // Initialize I2C for display
  Wire.begin();
  Wire.setClock(100000); // 100kHz for stability
  Wire.setTimeout(100); // 100ms timeout to prevent hangs
  
  // Initialize UMS3 board peripherals (call this after Wire.begin())
  ums3.begin();
  
  // Initialize display
  if (!display.begin()) {
    Serial.println("DISPLAY_INIT_FAILED");
    displayAvailable = false;
  } else {
    Serial.println("DISPLAY_INIT_OK");
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
  
  // Initialize rotary encoders
  rotaryA.setPosition(0);
  rotaryB.setPosition(0);
  
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
  
  Serial.println("READY");
}

// ===== MAIN LOOP =====
void loop() {
  yield(); // Feed watchdog
  
  // Update USB stack
  tud_task();
  
  // Parse general config on first loop iteration (after setup completes)
  if (!generalConfigLoaded && generalConfig.length() > 0) {
    parseGeneralConfig();
    generalConfigLoaded = true;
    yield();
  }
  
  // Handle serial communication
  handleSerial();
  
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

// ===== SD CARD FUNCTIONS =====
void initializeSD() {
  // Enable LDO_2 for SD card power
  pinMode(LDO_2, OUTPUT);
  digitalWrite(LDO_2, HIGH);
  delay(100);
  
  // Initialize SPI
  SPI.begin();
  pinMode(SD_CS_PIN, OUTPUT);
  digitalWrite(SD_CS_PIN, HIGH);
    
  // Initialize SD card
  if (!SD.begin(SD_CS_PIN)) {
    sdAvailable = false;
    Serial.println("SD_INIT_FAILED");
  } else {
    sdAvailable = true;
    Serial.println("SD_INIT_OK");
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
  
  // Update display after config save
  if (displayAvailable) {
    updateDisplayMode();
  }
  
  Serial.println("UPLOAD_OK");
}

void downloadConfig(bool useCurrentConfigPrefix) {
  if (!sdAvailable) {
    Serial.println("DOWNLOAD_ERROR: SD card not available");
    return;
  }
  
  if (useCurrentConfigPrefix) {
    Serial.print("CURRENT_CONFIG:");
        } else {
    Serial.print("CONFIG:");
  }
  
  yield();
  
  // Send general config
  File generalFile = SD.open(generalConfigFile, FILE_READ);
  if (generalFile) {
    Serial.print("{\"general\":");
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
  float batteryVoltage = ums3.getBatteryVoltage();
  float batteryPercentage = ((batteryVoltage - 3.0) / 1.2) * 100.0;
  
  if (batteryPercentage < 0) batteryPercentage = 0;
  if (batteryPercentage > 100) batteryPercentage = 100;
  
  bool vbusPresent = ums3.getVbusPresent();
  String status = vbusPresent ? "charging" : "discharging";
  
  String response = "BATTERY:";
  response += String((int)batteryPercentage);
  response += ",";
  response += String(batteryVoltage, 2);
  response += ",";
  response += status;
  
  return response;
}

// ===== SERIAL COMMUNICATION =====
void handleSerial() {
  if (Serial.available() > 0) {
    String line = Serial.readStringUntil('\n');
    line.trim();
  
    if (line.length() == 0) {
    return;
  }
  
    if (line == "PING") {
      Serial.println("PONG");
    return;
  }
  
    if (line == "DOWNLOAD_CONFIG") {
      downloadConfig(false);
    return;
  }
  
    if (line == "GET_CURRENT_CONFIG") {
      downloadConfig(true);
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
      display.setEnabled(displayEnabled);
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
      if (displayAvailable) {
        updateDisplay("Receiving...");
      }
    return;
  }
  
    if (line == "END_JSON") {
      uploading = false;
      if (displayAvailable) {
        updateDisplay("Saving...");
      }
      saveConfiguration(jsonBuffer);
      if (displayAvailable) {
        updateDisplay("Done!");
        delay(500);
        updateDisplayMode();
      }
    return;
  }
  
    if (uploading) {
      if (jsonBuffer.length() > 0) {
        jsonBuffer += "\n";
      }
      jsonBuffer += line;
    return;
  }
  }
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
    String batteryText = "Bat: " + String((int)batteryPercentage) + "%";
    updateDisplay(batteryText);
  } else if (displayMode == "time") {
    if (systemTime.length() > 0) {
      updateDisplay(systemTime);
    } else {
      updateDisplay("Time?");
    }
  }
}

// ===== BUTTON AND ROTARY ENCODER HANDLING =====
// Handler functions will be added here later
