/*
* Pad-Avan Macropad (UM FeatherS3[D] version, also works with non [D])
*
* Firmware version v2.0
* 
* Before flashing make sure to install the boards and these librarys:
* 
* Boards: 
* esp32 by espressif
* 
* Librarys:
* - ArduinoJson by Benoit Blanchon
* - UMS3 Helper by UnexpectedMaker
* - RotaryEncoder by Matthias Hertel
* - Adafruit GFX Library by Adafruit (and make sure to also install all dependencies)
* - Adafruit SSD1306 by Adafruit (and make sure to also install all dependencies)
* 
* After succesfully install everything make sure to make the following changes in the tools section of arduino:
* 
* Set board to: UM FeatherS3
* Set your connected COM port
* Enable: USB CDC On Boot
* Make sure PSRAM is ENABLED
* Set Upload mode to: USB-OTG CDC (TinyUSB)
* Make sure upload speed is set to: 921600
* Set USB Mode to: USB-OTG (TinyUSB)
* 
* Have fun ;)
*/

 #include <USB.h>
 #include <USBHIDKeyboard.h>
 #include <USBHIDConsumerControl.h>
 #include <USBHIDMouse.h>
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
 
 // LDO_2 for SD power delivery
 //#define LDO_2 39 // Commented out because it's not used, only if the sd module draws to much power, but then the module 3v3 needs to be rerouted to 3v3_2
 
 // Display: I2C, Address 0x3C
 #define SCREEN_WIDTH 128
 #define SCREEN_HEIGHT 32
 #define OLED_ADDRESS 0x3C
 
// ===== GLOBAL OBJECTS =====
USBHIDKeyboard Keyboard;
USBHIDConsumerControl ConsumerControl;
USBHIDMouse Mouse;
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

// Layer order array (stores layer names in order for counting)
String layerOrder[10] = {""}; // Max 10 layers
int layerOrderCount = 0;
bool wrapAround = true; // Default to true for backward compatibility
 
 // Store current layer config in RAM
 String currentLayerConfig = "";
 
 const String FIRMWARE_VERSION = "2.0";
 
 // ===== STATE VARIABLES =====
 bool sdAvailable = false;
 bool displayAvailable = false; // Track if display initialized successfully
 bool uploading = false;
 bool generalConfigLoaded = false; // Track if general config is loaded
 bool hostHandshakeDone = false; // Track if PING/PONG handshake completed
 String jsonBuffer = "";
 String generalConfig = ""; // Store general config in RAM
 
 // Button states (for debouncing)
 bool buttonStates[6] = {false, false, false, false, false, false};
 unsigned long buttonLastPress[6] = {0, 0, 0, 0, 0, 0};
 const unsigned long DEBOUNCE_DELAY = 50; // 50ms debounce

// Hold to repeat state for buttons
bool buttonHoldToRepeat[6] = {false, false, false, false, false, false};
bool buttonHoldFirstRepeat[6] = {false, false, false, false, false, false}; // Track if first repeat
String buttonHoldAction[6] = {"", "", "", "", "", ""};
String buttonHoldKeyValue[6] = {"", "", "", "", "", ""};
unsigned long buttonHoldLastRepeat[6] = {0, 0, 0, 0, 0, 0};

// Hold to repeat state for rotary encoder buttons (2 knobs)
bool rotaryHoldToRepeat[2] = {false, false};
bool rotaryHoldFirstRepeat[2] = {false, false};
String rotaryHoldAction[2] = {"", ""};
String rotaryHoldKeyValue[2] = {"", ""};
unsigned long rotaryHoldLastRepeat[2] = {0, 0};

const unsigned long HOLD_REPEAT_DELAY = 50; // 50ms between repeats (faster)
const unsigned long HOLD_REPEAT_INITIAL_DELAY = 200; // 200ms before first repeat starts (faster)
 
 // Rotary encoder states
 int rotaryALastPosition = 0;
 int rotaryBLastPosition = 0;
 bool rotaryAButtonState = false;
 bool rotaryBButtonState = false;
 unsigned long rotaryAButtonLastPress = 0;
 unsigned long rotaryBButtonLastPress = 0;
 
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
void executeMouseClick(String buttonName);
uint8_t getKeycode(String key);
uint16_t getConsumerCode(String control);
void navigateLayerUp();
void navigateLayerDown();
 
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
   
   
  // Initialize USB HID
  Keyboard.begin();
  ConsumerControl.begin();
  Mouse.begin();
  delay(200); // Give USB stack time to add HID descriptors
   
   // Initialize I2C for display
   Wire.begin();
   Wire.setClock(100000); // 100kHz for stability
   Wire.setTimeout(100); // 100ms timeout to prevent hangs
   
   // Initialize UMS3 board peripherals (call this after Wire.begin())
   ums3.begin();
   
   // Initialize all button pins
   pinMode(BUTTON_1_PIN, INPUT_PULLUP);
   pinMode(BUTTON_2_PIN, INPUT_PULLUP);
   pinMode(BUTTON_3_PIN, INPUT_PULLUP);
   pinMode(BUTTON_4_PIN, INPUT_PULLUP);
   pinMode(BUTTON_5_PIN, INPUT_PULLUP);
   pinMode(BUTTON_6_PIN, INPUT_PULLUP);
   
   // Initialize rotary encoder button pins
   pinMode(ROTARY_A_BUTTON_PIN, INPUT_PULLUP);
   pinMode(ROTARY_B_BUTTON_PIN, INPUT_PULLUP);
   
   // Initialize rotary encoder positions
   rotaryA.setPosition(0);
   rotaryB.setPosition(0);
   
   // Read initial positions after a few ticks to ensure encoder is stable
   delay(10);
   rotaryA.tick();
   rotaryB.tick();
   rotaryALastPosition = rotaryA.getPosition();
   rotaryBLastPosition = rotaryB.getPosition();
   
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
        // Use layer name from layerOrder if available, otherwise fall back to "Layer: X"
        if (layerOrderCount > 0 && currentLayer > 0 && currentLayer <= layerOrderCount) {
          String layerName = layerOrder[currentLayer - 1];
          if (layerName.length() > 0) {
            currentContent = layerName;
          } else {
            currentContent = "Layer: " + String(currentLayer);
          }
        } else {
          currentContent = "Layer: " + String(currentLayer);
        }
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
   // Enable LDO_2 for SD card power  // Commented out because it's not used, only if the sd module draws to much power, but then the module 3v3 needs to be rerouted to 3v3_2
   //pinMode(LDO_2, OUTPUT);
   //digitalWrite(LDO_2, HIGH);
   delay(100);
   
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
     
  size_t docSize = 2048; // Increased buffer for general config (needs space for layerOrder array)
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
  
  // Parse layer order array
  if (doc.containsKey("layerOrder")) {
    JsonArray layerOrderArray = doc["layerOrder"];
    layerOrderCount = 0;
    for (int i = 0; i < layerOrderArray.size() && i < 10; i++) {
      layerOrder[i] = layerOrderArray[i].as<String>();
      layerOrderCount++;
    }
    // Update maxLayers based on layerOrder count
    if (layerOrderCount > 0) {
      maxLayers = layerOrderCount;
    }
  }
  
  // Parse wrap-around setting
  if (doc.containsKey("wrapAround")) {
    wrapAround = doc["wrapAround"].as<bool>();
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
    DynamicJsonDocument generalDoc(2048); // Increased size for layerOrder array
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
      if (generalDoc.containsKey("layerOrder")) {
        doc["layerOrder"] = generalDoc["layerOrder"];
      }
      if (generalDoc.containsKey("wrapAround")) {
        doc["wrapAround"] = generalDoc["wrapAround"];
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
  
  // Update layerOrder array from saved config
  if (doc.containsKey("layerOrder")) {
    JsonArray layerOrderArray = doc["layerOrder"];
    layerOrderCount = 0;
    for (int i = 0; i < layerOrderArray.size() && i < 10; i++) {
      layerOrder[i] = layerOrderArray[i].as<String>();
      layerOrderCount++;
    }
    if (layerOrderCount > 0) {
      maxLayers = layerOrderCount;
    }
  }
   
   File generalFile = SD.open(generalConfigFile, FILE_WRITE);
   if (generalFile) {
     generalFile.print(generalJson);
     generalFile.close();
     generalConfig = generalJson; // Update in-memory config
   }
   
   yield();
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
    if (doc.containsKey("layerOrder")) {
      generalDoc["layerOrder"] = doc["layerOrder"];
    }
    if (doc.containsKey("wrapAround")) {
      generalDoc["wrapAround"] = doc["wrapAround"];
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
       
       // Update layerOrder array from saved config
       if (generalDoc.containsKey("layerOrder")) {
         JsonArray layerOrderArray = generalDoc["layerOrder"];
         layerOrderCount = 0;
         for (int i = 0; i < layerOrderArray.size() && i < 10; i++) {
           layerOrder[i] = layerOrderArray[i].as<String>();
           layerOrderCount++;
         }
         if (layerOrderCount > 0) {
           maxLayers = layerOrderCount;
         }
       }
       
       // Update wrap-around setting
       if (generalDoc.containsKey("wrapAround")) {
         wrapAround = generalDoc["wrapAround"].as<bool>();
       }
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
  Serial.flush(); // Ensure all data is sent before returning
  
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
   if (Serial.available() <= 0) {
     return;
   }
   
   // Read line with timeout to prevent blocking
   String line = "";
   unsigned long startTime = millis();
   while (Serial.available() > 0 && (millis() - startTime < 100)) {
     char c = Serial.read();
     if (c == '\n' || c == '\r') {
       break;
     }
     if (c >= 32 || c == '\t') { // Only add printable characters and tab
       line += c;
     }
   }
   line.trim();
   
   if (line.length() == 0) {
     return;
   }
   
   // Handle PING/PONG handshake first
   if (line == "PING") {
     // Clear any pending output before responding
     Serial.flush();
     
     Serial.println("PONG");
     Serial.flush(); // Ensure response is sent immediately
     
     if (!hostHandshakeDone) {
       // Small delay to ensure PONG is sent before READY
       delay(10);
       hostHandshakeDone = true;
       // Send READY after successful handshake
       Serial.println("READY");
       Serial.flush();
     }
     return;
   }
   
   // Before handshake: ignore all other commands
   if (!hostHandshakeDone) {
     return;
   }
   
   // From here on: handle all other commands (only after handshake)
   
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
       Serial.flush();
       return;
     }
   
     if (line == "BATTERY_STATUS") {
       String batteryResponse = getBatteryStatus();
       Serial.println(batteryResponse);
       Serial.flush();
       return;
     }
   
     if (line == "GET_VERSION") {
       Serial.println("VERSION:" + FIRMWARE_VERSION);
       Serial.flush();
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
    // Use layer name from layerOrder if available, otherwise fall back to "Layer: X"
    String layerDisplayText = "";
    if (layerOrderCount > 0 && currentLayer > 0 && currentLayer <= layerOrderCount) {
      String layerName = layerOrder[currentLayer - 1];
      if (layerName.length() > 0) {
        layerDisplayText = layerName;
      } else {
        layerDisplayText = "Layer: " + String(currentLayer);
      }
    } else {
      layerDisplayText = "Layer: " + String(currentLayer);
    }
    updateDisplay(layerDisplayText);
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
 
// ===== BUTTON HANDLING =====
void checkButtons() {
  // Check all 6 buttons
  int buttonPins[6] = {BUTTON_1_PIN, BUTTON_2_PIN, BUTTON_3_PIN, BUTTON_4_PIN, BUTTON_5_PIN, BUTTON_6_PIN};
  
  for (int i = 0; i < 6; i++) {
    bool currentState = !digitalRead(buttonPins[i]); // Inverted because of INPUT_PULLUP (pressed = LOW)
    
    // 1. PRESS DETECT
    // If button is physically pressed, was previously off, and debounce time has passed
    if (currentState && !buttonStates[i] && (millis() - buttonLastPress[i] > DEBOUNCE_DELAY)) {
      buttonStates[i] = true;
      buttonLastPress[i] = millis(); // Record time of press
      handleButtonPress(i + 1); // Button numbers are 1-6, array index is 0-5
    } 
    // 2. RELEASE DETECT (FIXED)
    // Only accept a release if the button has been held longer than the debounce time.
    // This ignores the micro-disconnects (bouncing) that kill the repeat timer.
    else if (!currentState && buttonStates[i]) {
      if (millis() - buttonLastPress[i] > DEBOUNCE_DELAY) {
        buttonStates[i] = false;
        // Only stop hold to repeat if it's a REAL release
        buttonHoldToRepeat[i] = false;
        buttonHoldFirstRepeat[i] = false;
        buttonHoldAction[i] = "";
        buttonHoldKeyValue[i] = "";
      }
    }
    
    // 3. HOLD LOGIC
    // Check if button is effectively held (buttonStates is true)
    // Note: We check buttonStates[i] instead of just currentState to survive the bounces
    if (buttonStates[i] && buttonHoldToRepeat[i]) {
      unsigned long currentTime = millis();
      unsigned long requiredDelay = buttonHoldFirstRepeat[i] ? HOLD_REPEAT_INITIAL_DELAY : HOLD_REPEAT_DELAY;
      
      // Calculate time elapsed since last action
      unsigned long timeElapsed;
      if (currentTime >= buttonHoldLastRepeat[i]) {
        // Normal case: no wrap-around
        timeElapsed = currentTime - buttonHoldLastRepeat[i];
      } else {
        // Wrap-around case (unlikely but handle it)
        timeElapsed = (ULONG_MAX - buttonHoldLastRepeat[i]) + currentTime + 1;
      }
      
      // Trigger repeat only once per delay period
      if (timeElapsed >= requiredDelay) {
        if (buttonHoldAction[i].length() > 0) {
          executeButtonAction(buttonHoldAction[i], buttonHoldKeyValue[i]);
        }
        buttonHoldFirstRepeat[i] = false;
        buttonHoldLastRepeat[i] = currentTime; // Reset to now for next check
      }
    }
  }
}
 
 void handleButtonPress(int buttonNumber) {
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
   String buttonKey = String(buttonNumber);
   
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
  
 // Check if hold to repeat is enabled (only for Type Text and Special Key)
  bool holdToRepeat = false;
  if ((action == "Type Text" || action == "Special Key") && button.containsKey("holdToRepeat")) {
    holdToRepeat = button["holdToRepeat"].as<bool>();
  }
  
  // Store hold to repeat state if enabled
  int buttonIndex = buttonNumber - 1; // Convert to 0-based index
  if (holdToRepeat) {
    buttonHoldToRepeat[buttonIndex] = true;
    buttonHoldFirstRepeat[buttonIndex] = true; // Mark as first repeat
    // Make sure to copy the strings properly
    buttonHoldAction[buttonIndex] = String(action);
    buttonHoldKeyValue[buttonIndex] = String(keyValue);
    // Set timestamp to NOW - first repeat will happen after HOLD_REPEAT_INITIAL_DELAY
    buttonHoldLastRepeat[buttonIndex] = millis();
  } else {
    buttonHoldToRepeat[buttonIndex] = false;
    buttonHoldFirstRepeat[buttonIndex] = false;
    buttonHoldAction[buttonIndex] = "";
    buttonHoldKeyValue[buttonIndex] = "";
  }
   
   executeButtonAction(action, keyValue);
 }
 
 void executeButtonAction(String action, String keyValue) {
   yield();
   
   if (action == "Type Text") {
     // Use keyboardLayout.write() for Swiss German layout support
     keyboardLayout.write(keyValue);
  } else if (action == "Special Key") {
    // Check if it's a mouse button
    keyValue.toUpperCase();
    if (keyValue == "LEFT MOUSE" || keyValue == "RIGHT MOUSE" || keyValue == "MIDDLE MOUSE" || 
        keyValue == "LEFT" || keyValue == "RIGHT" || keyValue == "MIDDLE") {
      executeMouseClick(keyValue);
    }
    // Check if it's a Consumer Control key (media/volume)
    else {
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
    }
  } else if (action == "Key combo") {
     Serial.print("[BUTTON] Key combo action triggered, keyValue: '");
     Serial.print(keyValue);
     Serial.println("'");
     if (keyValue.length() > 0) {
       executeKeyCombo(keyValue);
     } else {
       Serial.println("[BUTTON] ERROR: Key combo keyValue is empty!");
     }
   } else if (action == "Layer Switch") {
    // Check direction from keyValue: "up" or "down" (default to "up")
    String direction = keyValue;
    direction.toLowerCase();
    
    if (direction.equals("down")) {
      navigateLayerDown();
    } else {
      navigateLayerUp();
    }
  } else if (action == "Volume Control") {
     uint16_t consumerCode = getConsumerCode(keyValue);
     if (consumerCode != 0) {
       ConsumerControl.press(consumerCode);
       ConsumerControl.release();
     }
   }
 }
 
 // Helper function to convert modifier string to keycode
uint8_t getModifierKeycode(String modifierStr) {
  modifierStr.trim();
  modifierStr.toUpperCase();
  
  Serial.print("[GETMODIFIER] Input: '");
  Serial.print(modifierStr);
  Serial.print("'");
  
  if (modifierStr == "CTRL" || modifierStr == "CONTROL") {
    Serial.println(" -> KEY_LEFT_CTRL (0x80)");
    return 0x80; // KEY_LEFT_CTRL
  } else if (modifierStr == "SHIFT") {
    Serial.println(" -> KEY_LEFT_SHIFT (0x81)");
    return 0x81; // KEY_LEFT_SHIFT
  } else if (modifierStr == "ALT") {
    Serial.println(" -> KEY_LEFT_ALT (0x82)");
    return 0x82; // KEY_LEFT_ALT
  } else if (modifierStr == "WIN" || modifierStr == "WINDOWS" || modifierStr == "WINDOWS KEY") {
    Serial.println(" -> KEY_LEFT_GUI (0x83)");
    return 0x83; // KEY_LEFT_GUI
  } else if (modifierStr == "RIGHT CTRL" || modifierStr == "RCTRL") {
    Serial.println(" -> KEY_RIGHT_CTRL (0x84)");
    return 0x84; // KEY_RIGHT_CTRL
  } else if (modifierStr == "RIGHT SHIFT" || modifierStr == "RSHIFT") {
    Serial.println(" -> KEY_RIGHT_SHIFT (0x85)");
    return 0x85; // KEY_RIGHT_SHIFT
  } else if (modifierStr == "RIGHT ALT" || modifierStr == "RALT" || modifierStr == "ALTGR") {
    Serial.println(" -> KEY_RIGHT_ALT (0x86)");
    return 0x86; // KEY_RIGHT_ALT
  } else if (modifierStr == "RIGHT GUI" || modifierStr == "RGUI") {
    Serial.println(" -> KEY_RIGHT_GUI (0x87)");
    return 0x87; // KEY_RIGHT_GUI
  }
  Serial.println(" -> ERROR: Unknown modifier");
  return 0; // Unknown modifier
}
 
// Helper function to convert key string to keycode (ASCII or library keycode)
uint8_t getComboKeycode(String keyStr) {
  keyStr.trim();
  String upperKey = keyStr;
  upperKey.toUpperCase();
  
  Serial.print("[GET_COMBO] Looking for key: ");
  Serial.println(upperKey);

  // 1. LETTERS (A-Z) -> Return LOWERCASE ASCII
  // The library maps 'a' (0x61) to the key 'A' WITHOUT Shift.
  // The library maps 'A' (0x41) to the key 'A' WITH Shift.
  // To fix your "Ctrl+Shift+C" bug, we must return 'a', not 'A'.
  if (upperKey.length() == 1 && upperKey[0] >= 'A' && upperKey[0] <= 'Z') {
    return upperKey[0] + 32; // Convert 'A'(65) to 'a'(97)
  }

  // 2. NUMBERS (0-9) -> Return ASCII
  // Returning the ASCII digit is safe here.
  if (upperKey.length() == 1 && isDigit(upperKey[0])) {
    return upperKey[0];
  }

  // 3. SPECIAL KEYS -> Use Library Specific Keycodes (0xB0+) [cite: 2]
  // These constants are defined in USBHIDKeyboard (and shown in your KeyboardLayoutWinCH.h)
  if (upperKey == "ENTER" || upperKey == "RETURN") return 0xB0;
  if (upperKey == "ESC" || upperKey == "ESCAPE") return 0xB1;
  if (upperKey == "BACKSPACE" || upperKey == "BKSP") return 0xB2;
  if (upperKey == "TAB") return 0xB3;
  if (upperKey == "SPACE" || upperKey == "SPACEBAR") return ' '; // ASCII 0x20 is fine for space
  
  if (upperKey == "F1") return 0xC2;
  if (upperKey == "F2") return 0xC3;
  if (upperKey == "F3") return 0xC4;
  if (upperKey == "F4") return 0xC5;
  if (upperKey == "F5") return 0xC6;
  if (upperKey == "F6") return 0xC7;
  if (upperKey == "F7") return 0xC8;
  if (upperKey == "F8") return 0xC9;
  if (upperKey == "F9") return 0xCA;
  if (upperKey == "F10") return 0xCB;
  if (upperKey == "F11") return 0xCC;
  if (upperKey == "F12") return 0xCD;

  if (upperKey == "INSERT") return 0xD1;
  if (upperKey == "HOME") return 0xD2;
  if (upperKey == "PAGE UP" || upperKey == "PAGEUP") return 0xD3;
  if (upperKey == "DELETE" || upperKey == "DEL") return 0xD4;
  if (upperKey == "END") return 0xD5;
  if (upperKey == "PAGE DOWN" || upperKey == "PAGEDOWN") return 0xD6;
  
  if (upperKey == "ARROW RIGHT" || upperKey == "RIGHT") return 0xD7;
  if (upperKey == "ARROW LEFT" || upperKey == "LEFT") return 0xD8;
  if (upperKey == "ARROW DOWN" || upperKey == "DOWN") return 0xD9;
  if (upperKey == "ARROW UP" || upperKey == "UP") return 0xDA;

  return 0; // Unknown key
}
 
// ===== KEY COMBO EXECUTION =====
void executeKeyCombo(String comboString) {
  yield(); // Feed watchdog before processing
  
  // Parse combo like "Ctrl+C", "Ctrl+Shift+C", or "Win+Alt+Tab"
  // Support up to 3 keys: 2 modifiers + 1 key
  
  // Split by '+' to get all parts
  // We need to preserve original case for the key, but uppercase for modifiers
  String parts[3];
  String originalParts[3]; // Store original case
  int partCount = 0;
  int lastPos = 0;
  
  for (int i = 0; i < comboString.length() && partCount < 3; i++) {
    if (comboString.charAt(i) == '+') {
      originalParts[partCount] = comboString.substring(lastPos, i);
      originalParts[partCount].trim();
      parts[partCount] = originalParts[partCount];
      parts[partCount].toUpperCase(); // Uppercase for modifier lookup
      partCount++;
      lastPos = i + 1;
    }
  }
  // Add the last part (the actual key)
  if (lastPos < comboString.length() && partCount < 3) {
    originalParts[partCount] = comboString.substring(lastPos);
    originalParts[partCount].trim();
    parts[partCount] = originalParts[partCount];
    parts[partCount].toUpperCase(); // Uppercase for display, but we'll use original for keycode
    partCount++;
  }
  
  if (partCount < 2) {
    return; // Need at least modifier + key
  }
  
  yield(); // Feed watchdog after parsing
  
  // The last part is always the key - use original case
  String originalKeyStr = originalParts[partCount - 1];
  
  // For key combos, always use lowercase keycode - don't add Shift automatically
  // Shift should only be added if explicitly included in the combo string
  // Convert to lowercase for keycode lookup
  String keyForLookup = originalKeyStr;
  keyForLookup.toLowerCase();
  
  uint8_t keycode = getComboKeycode(keyForLookup);
  
  if (keycode == 0) {
    return; // Unknown key
  }
  
  yield(); // Feed watchdog before pressing keys
  
  // All other parts are modifiers
  // Press all modifiers first, then the key
  for (int i = 0; i < partCount - 1; i++) {
    uint8_t modifier = getModifierKeycode(parts[i]);
    if (modifier != 0) {
      Keyboard.press(modifier);
    }
  }
  
  // Small delay to ensure modifiers are registered
  delay(10);
  yield();
  
  // Press the key
  Keyboard.press(keycode);
  
  // Small delay to ensure key press is registered
  delay(10);
  
  // Hold for a moment with all keys pressed
  delay(50);
  
  // Release all
  Keyboard.releaseAll();
  delay(10);
  
  yield(); // Feed watchdog after execution
}
 
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
   
   return 0;
 }
 
// ===== CONSUMER CONTROL =====
uint16_t getConsumerCode(String control) {
  control.toUpperCase();
  
  if (control == "VOLUME UP") return 0xE9;
  if (control == "VOLUME DOWN") return 0xEA;
  if (control == "MUTE") return 0xE2;
  if (control == "PLAY/PAUSE" || control == "PLAY_PAUSE" || control == "PLAY PAUSE") return 0xCD;
  if (control == "NEXT TRACK" || control == "NEXT") return 0xB5;
  if (control == "PREVIOUS TRACK" || control == "PREVIOUS" || control == "PREV") return 0xB6;
  if (control == "STOP") return 0xB7;
  
  return 0;
}

// ===== MOUSE BUTTON HANDLING =====
void executeMouseClick(String buttonName) {
  buttonName.toUpperCase();
  
  if (buttonName == "LEFT MOUSE" || buttonName == "LEFT") {
    Mouse.click(MOUSE_LEFT);
  } else if (buttonName == "RIGHT MOUSE" || buttonName == "RIGHT") {
    Mouse.click(MOUSE_RIGHT);
  } else if (buttonName == "MIDDLE MOUSE" || buttonName == "MIDDLE") {
    Mouse.click(MOUSE_MIDDLE);
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
  // Read pressKey for actions that need it: Type Text, Special Key, Key combo, Layer Switch
  if (pressAction == "Type Text" || pressAction == "Special Key" || pressAction == "Key combo" || pressAction == "Layer Switch") {
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
  }
  
  // Check if hold to repeat is enabled (only for Type Text and Special Key)
  bool holdToRepeat = false;
  if ((pressAction == "Type Text" || pressAction == "Special Key") && knobConfig.containsKey("pressHoldToRepeat")) {
    holdToRepeat = knobConfig["pressHoldToRepeat"].as<bool>();
  }
  
  // Store hold to repeat state if enabled
  int rotaryIndex = (knobLetter == "A") ? 0 : 1;
  if (holdToRepeat) {
    rotaryHoldToRepeat[rotaryIndex] = true;
    rotaryHoldFirstRepeat[rotaryIndex] = true;
    rotaryHoldAction[rotaryIndex] = String(pressAction);
    rotaryHoldKeyValue[rotaryIndex] = String(pressKey);
    rotaryHoldLastRepeat[rotaryIndex] = millis();
  } else {
    rotaryHoldToRepeat[rotaryIndex] = false;
    rotaryHoldFirstRepeat[rotaryIndex] = false;
    rotaryHoldAction[rotaryIndex] = "";
    rotaryHoldKeyValue[rotaryIndex] = "";
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
    // Check if it's a mouse button
    keyValue.toUpperCase();
    if (keyValue == "LEFT MOUSE" || keyValue == "RIGHT MOUSE" || keyValue == "MIDDLE MOUSE" || 
        keyValue == "LEFT" || keyValue == "RIGHT" || keyValue == "MIDDLE") {
      executeMouseClick(keyValue);
    }
    // Check if it's a Consumer Control key (media/volume)
    else {
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
    }
  } else if (action == "Key combo") {
     Serial.print("[KNOB] Key combo action triggered, keyValue: '");
     Serial.print(keyValue);
     Serial.println("'");
     if (keyValue.length() > 0) {
       executeKeyCombo(keyValue);
     } else {
       Serial.println("[KNOB] ERROR: Key combo keyValue is empty!");
     }
  } else if (action == "Layer Count Up") {
    navigateLayerUp();
  } else if (action == "Layer Count Down") {
    navigateLayerDown();
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
  } else if (action == "Layer Switch") {
    // Check direction from keyValue: "up" or "down" (default to "up")
    String direction = keyValue;
    direction.toLowerCase();
    
    if (direction.equals("down")) {
      navigateLayerDown();
    } else {
      navigateLayerUp();
    }
  }
}

// Helper function to navigate to next layer (respects wrapAround)
void navigateLayerUp() {
  // Use layerOrder array if available, otherwise fall back to numeric counting
  if (layerOrderCount > 0) {
    // currentLayer is 1-based, so convert to 0-based index
    int currentIndex = currentLayer - 1;
    if (currentIndex < 0) currentIndex = 0;
    if (currentIndex >= layerOrderCount) currentIndex = layerOrderCount - 1;
    
    // Move to next layer in order
    currentIndex++;
    if (currentIndex >= layerOrderCount) {
      if (wrapAround) {
        currentIndex = 0; // Wrap around to first layer
      } else {
        currentIndex = layerOrderCount - 1; // Stay at last layer
      }
    }
    
    // Convert back to 1-based layer number
    currentLayer = currentIndex + 1;
  } else {
    // Fallback to numeric counting
    currentLayer++;
    if (currentLayer > maxLayers) {
      if (wrapAround) {
        currentLayer = 1; // Wrap around to first layer
      } else {
        currentLayer = maxLayers; // Stay at last layer
      }
    }
  }
  loadLayerConfig(currentLayer);
  saveCurrentLayer(); // Save the new current layer to general.json
  if (displayAvailable) {
    updateDisplayMode();
  }
}

// Helper function to navigate to previous layer (respects wrapAround)
void navigateLayerDown() {
  // Use layerOrder array if available, otherwise fall back to numeric counting
  if (layerOrderCount > 0) {
    // currentLayer is 1-based, so convert to 0-based index
    int currentIndex = currentLayer - 1;
    if (currentIndex < 0) currentIndex = 0;
    if (currentIndex >= layerOrderCount) currentIndex = layerOrderCount - 1;
    
    // Move to previous layer in order
    currentIndex--;
    if (currentIndex < 0) {
      if (wrapAround) {
        currentIndex = layerOrderCount - 1; // Wrap around to last layer
      } else {
        currentIndex = 0; // Stay at first layer
      }
    }
    
    // Convert back to 1-based layer number
    currentLayer = currentIndex + 1;
  } else {
    // Fallback to numeric counting
    currentLayer--;
    if (currentLayer < 1) {
      if (wrapAround) {
        currentLayer = maxLayers; // Wrap around to last layer
      } else {
        currentLayer = 1; // Stay at first layer
      }
    }
  }
  loadLayerConfig(currentLayer);
  saveCurrentLayer(); // Save the new current layer to general.json
  if (displayAvailable) {
    updateDisplayMode();
  }
}
 