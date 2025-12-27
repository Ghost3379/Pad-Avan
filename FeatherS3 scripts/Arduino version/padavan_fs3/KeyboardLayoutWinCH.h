/*
 * Keyboard Layout for Windows Swiss German (QWERTZ)
 * Official Swiss German keyboard layout mapping
 */

#ifndef KEYBOARD_LAYOUT_WIN_CH_H
#define KEYBOARD_LAYOUT_WIN_CH_H

#include <USBHIDKeyboard.h>

// Standard Modifier Constants for USBHIDKeyboard
#define KEY_LEFT_CTRL   0x80
#define KEY_LEFT_SHIFT  0x81
#define KEY_LEFT_ALT    0x82
#define KEY_LEFT_GUI    0x83
#define KEY_RIGHT_CTRL  0x84
#define KEY_RIGHT_SHIFT 0x85
#define KEY_RIGHT_ALT   0x86 // AltGr
#define KEY_RIGHT_GUI   0x87

class KeyboardLayoutWinCH {
private:
  USBHIDKeyboard* keyboard;
  
  // Helper: Press AltGr + Key (using ASCII)
  void pressAltGr(char key) {
    keyboard->press(KEY_RIGHT_ALT);
    keyboard->press(key);
    keyboard->releaseAll();
  }

  // Helper: Press Shift + AltGr + Key
  void pressShiftAltGr(char key) {
    keyboard->press(KEY_LEFT_SHIFT);
    keyboard->press(KEY_RIGHT_ALT);
    keyboard->press(key);
    keyboard->releaseAll();
  }
  
  // Check if character requires AltGr handling
  bool isSwissSpecialChar(char c) {
    return (c == 'ä' || c == 'ö' || c == 'ü' || 
            c == 'Ä' || c == 'Ö' || c == 'Ü' || 
            c == '€' || c == '@' || c == '§' || c == '|' ||
            c == 'é' || c == 'è' || c == 'à' ||
            c == 'É' || c == 'È' || c == 'À' || c == 'Ç');
  }
  
  void typeSwissChar(char c) {
    // Swiss German special characters using AltGr combinations
    // Using ASCII characters, not raw HID codes
    
    switch (c) {
      // AltGr combinations
      case '@': pressAltGr('2'); break;
      case '§': pressAltGr('3'); break;
      case '|': pressAltGr('7'); break;
      case '€': pressAltGr('e'); break;
      case 'é': 
        pressAltGr('e');
        delay(10);
        keyboard->press('2');
        keyboard->releaseAll();
        break;
      case 'è': pressAltGr('`'); break;
      case 'à': pressAltGr('a'); break;
      case 'É':
        pressShiftAltGr('e');
        delay(10);
        keyboard->press(KEY_LEFT_SHIFT);
        keyboard->press('2');
        keyboard->releaseAll();
        break;
      case 'È': pressShiftAltGr('`'); break;
      case 'À': pressShiftAltGr('a'); break;
      case 'Ç': pressShiftAltGr('c'); break;
      default: break;
    }
  }
  

public:
  KeyboardLayoutWinCH(USBHIDKeyboard* kb) : keyboard(kb) {}
  
  // Press a special key by name (using USBHIDKeyboard library keycodes, not raw HID)
  // The library expects KEY_* constants (>= 0xB0 for special keys, 0x80-0x87 for modifiers)
  // Raw HID codes (0x29, 0x3A, etc.) are interpreted as ASCII, causing wrong characters
  void pressKey(String keyName) {
    keyName.toUpperCase();
    
    // Function keys (F1-F12) - USBHIDKeyboard library keycodes
    if (keyName == "F1") { keyboard->press(0xC2); keyboard->releaseAll(); return; }
    if (keyName == "F2") { keyboard->press(0xC3); keyboard->releaseAll(); return; }
    if (keyName == "F3") { keyboard->press(0xC4); keyboard->releaseAll(); return; }
    if (keyName == "F4") { keyboard->press(0xC5); keyboard->releaseAll(); return; }
    if (keyName == "F5") { keyboard->press(0xC6); keyboard->releaseAll(); return; }
    if (keyName == "F6") { keyboard->press(0xC7); keyboard->releaseAll(); return; }
    if (keyName == "F7") { keyboard->press(0xC8); keyboard->releaseAll(); return; }
    if (keyName == "F8") { keyboard->press(0xC9); keyboard->releaseAll(); return; }
    if (keyName == "F9") { keyboard->press(0xCA); keyboard->releaseAll(); return; }
    if (keyName == "F10") { keyboard->press(0xCB); keyboard->releaseAll(); return; }
    if (keyName == "F11") { keyboard->press(0xCC); keyboard->releaseAll(); return; }
    if (keyName == "F12") { keyboard->press(0xCD); keyboard->releaseAll(); return; }
    
    // Navigation keys - USBHIDKeyboard library keycodes
    if (keyName == "ESCAPE" || keyName == "ESC") { keyboard->press(0xB1); keyboard->releaseAll(); return; }
    if (keyName == "ENTER") { keyboard->press(0xB0); keyboard->releaseAll(); return; }
    if (keyName == "TAB") { keyboard->press(0xB3); keyboard->releaseAll(); return; }
    if (keyName == "SPACE" || keyName == "SPACEBAR") { keyboard->press(' '); keyboard->releaseAll(); return; }
    if (keyName == "BACKSPACE") { keyboard->press(0xB2); keyboard->releaseAll(); return; }
    if (keyName == "DELETE" || keyName == "DEL") { keyboard->press(0xD4); keyboard->releaseAll(); return; }
    if (keyName == "HOME") { keyboard->press(0xD2); keyboard->releaseAll(); return; }
    if (keyName == "END") { keyboard->press(0xD5); keyboard->releaseAll(); return; }
    if (keyName == "PAGE UP" || keyName == "PAGEUP") { keyboard->press(0xD3); keyboard->releaseAll(); return; }
    if (keyName == "PAGE DOWN" || keyName == "PAGEDOWN") { keyboard->press(0xD6); keyboard->releaseAll(); return; }
    if (keyName == "ARROW UP" || keyName == "UP" || keyName == "UP_ARROW") { keyboard->press(0xDA); keyboard->releaseAll(); return; }
    if (keyName == "ARROW DOWN" || keyName == "DOWN" || keyName == "DOWN_ARROW") { keyboard->press(0xD9); keyboard->releaseAll(); return; }
    if (keyName == "ARROW LEFT" || keyName == "LEFT" || keyName == "LEFT_ARROW") { keyboard->press(0xD8); keyboard->releaseAll(); return; }
    if (keyName == "ARROW RIGHT" || keyName == "RIGHT" || keyName == "RIGHT_ARROW") { keyboard->press(0xD7); keyboard->releaseAll(); return; }
    
    // Modifier keys - USBHIDKeyboard library keycodes (0x80-0x87 range)
    if (keyName == "WINDOWS KEY" || keyName == "WINDOWS" || keyName == "WIN") { 
      keyboard->press(0x83); // KEY_LEFT_GUI
      keyboard->releaseAll(); 
      return; 
    }
    if (keyName == "MENU KEY" || keyName == "MENU" || keyName == "APPLICATION") { 
      keyboard->press(0xED); // Application/Menu key (non-printing key range)
      keyboard->releaseAll(); 
      return; 
    }
    
    // Single letter keys (A-Z) - use ASCII directly, library handles conversion
    if (keyName.length() == 1 && keyName[0] >= 'A' && keyName[0] <= 'Z') {
      keyboard->press(keyName[0]);
      keyboard->releaseAll();
      return;
    }
    
    // Single number keys (0-9) - use ASCII directly
    if (keyName == "0") { keyboard->press('0'); keyboard->releaseAll(); return; }
    if (keyName == "1") { keyboard->press('1'); keyboard->releaseAll(); return; }
    if (keyName == "2") { keyboard->press('2'); keyboard->releaseAll(); return; }
    if (keyName == "3") { keyboard->press('3'); keyboard->releaseAll(); return; }
    if (keyName == "4") { keyboard->press('4'); keyboard->releaseAll(); return; }
    if (keyName == "5") { keyboard->press('5'); keyboard->releaseAll(); return; }
    if (keyName == "6") { keyboard->press('6'); keyboard->releaseAll(); return; }
    if (keyName == "7") { keyboard->press('7'); keyboard->releaseAll(); return; }
    if (keyName == "8") { keyboard->press('8'); keyboard->releaseAll(); return; }
    if (keyName == "9") { keyboard->press('9'); keyboard->releaseAll(); return; }
  }
  
  void write(String text) {
    // US-to-CH Translation: The library thinks it's a US keyboard, but Windows expects CH layout
    // We need to send the US key that is at the position where the CH key should be
    
    for (unsigned int i = 0; i < text.length(); i++) {
      char c = text[i];
      
      // Handle Swiss special characters (AltGr combinations)
      if (isSwissSpecialChar(c)) {
        typeSwissChar(c);
        delay(10);
        continue;
      }
      
      // US-to-CH key mapping for regular characters
      // Z and Y are swapped (QWERTY vs QWERTZ)
      if (c == 'z' || c == 'Z') {
        keyboard->print(c == 'z' ? 'y' : 'Y');
        delay(5);
        continue;
      }
      if (c == 'y' || c == 'Y') {
        keyboard->print(c == 'y' ? 'z' : 'Z');
        delay(5);
        continue;
      }
      
      // Umlaute: On CH layout, these are direct keys, but on US they're at different positions
      // We send the US key that is at the CH position
      if (c == 'ä') {
        keyboard->press('\''); // US ' is at CH ä position
        keyboard->releaseAll();
        delay(5);
        continue;
      }
      if (c == 'Ä') {
        keyboard->press(KEY_LEFT_SHIFT);
        keyboard->press('\'');
        keyboard->releaseAll();
        delay(5);
        continue;
      }
      if (c == 'ö') {
        keyboard->press(';'); // US ; is at CH ö position
        keyboard->releaseAll();
        delay(5);
        continue;
      }
      if (c == 'Ö') {
        keyboard->press(KEY_LEFT_SHIFT);
        keyboard->press(';');
        keyboard->releaseAll();
        delay(5);
        continue;
      }
      if (c == 'ü') {
        keyboard->press('['); // US [ is at CH ü position
        keyboard->releaseAll();
        delay(5);
        continue;
      }
      if (c == 'Ü') {
        keyboard->press(KEY_LEFT_SHIFT);
        keyboard->press('[');
        keyboard->releaseAll();
        delay(5);
        continue;
      }
      
      // Special characters that are on different keys
      if (c == '-') {
        keyboard->press('/'); // US / is at CH - position
        keyboard->releaseAll();
        delay(5);
        continue;
      }
      if (c == '_') {
        keyboard->press(KEY_LEFT_SHIFT);
        keyboard->press('/');
        keyboard->releaseAll();
        delay(5);
        continue;
      }
      if (c == ':') {
        keyboard->press(KEY_LEFT_SHIFT);
        keyboard->press('.');
        keyboard->releaseAll();
        delay(5);
        continue;
      }
      if (c == ';') {
        keyboard->press(KEY_LEFT_SHIFT);
        keyboard->press(',');
        keyboard->releaseAll();
        delay(5);
        continue;
      }
      
      // Default: Send ASCII directly, library handles conversion
      keyboard->print(c);
      delay(5);
    }
  }
};

#endif // KEYBOARD_LAYOUT_WIN_CH_H
