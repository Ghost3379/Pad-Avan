#include <SD.h>
#include <SPI.h>

// SD Card pins for FeatherS3
#define SD_CS_PIN 38
#define LDO_2 39  // Power pin for SD card

void setup() {
  Serial.begin(115200);
  
  // Wait for Serial
  while (!Serial) {
    delay(10);
  }
  
  Serial.println("SD Card Wiper");
  Serial.println("==============");
  
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
    Serial.println("ERROR: SD card initialization failed!");
    Serial.println("Please check your SD card and wiring.");
    return;
  }
  
  Serial.println("SD card initialized successfully.");
  Serial.println("Starting wipe process...");
  Serial.println();
  
  // Wipe all files and directories
  wipeSDCard("/");
  
  Serial.println();
  Serial.println("SD card wipe completed!");
  Serial.println("All files and directories have been deleted.");
}

void loop() {
  // Nothing to do here
}

void wipeSDCard(const char* dirPath) {
  File root = SD.open(dirPath);
  
  if (!root) {
    Serial.println("ERROR: Cannot open directory");
    return;
  }
  
  if (!root.isDirectory()) {
    Serial.println("ERROR: Not a directory");
    root.close();
    return;
  }
  
  File file = root.openNextFile();
  
  while (file) {
    String fileName = file.name();
    String fullPath = String(dirPath) + fileName;
    
    if (file.isDirectory()) {
      // Recursively delete subdirectories
      fullPath += "/";
      Serial.println("Deleting directory: " + fullPath);
      wipeSDCard(fullPath.c_str());
      SD.rmdir(fullPath.c_str());
    } else {
      // Delete file
      Serial.println("Deleting file: " + fullPath);
      SD.remove(fullPath.c_str());
    }
    
    file.close();
    file = root.openNextFile();
    yield(); // Feed watchdog
  }
  
  root.close();
  
  // Delete the directory itself if it's not root
  if (strcmp(dirPath, "/") != 0) {
    SD.rmdir(dirPath);
  }
}

