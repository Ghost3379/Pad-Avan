/*
 * Keyboard Layout for Windows Swiss German (QWERTZ)
 * Official Swiss German keyboard layout mapping
 */

#ifndef KEYBOARD_LAYOUT_WIN_CH_H
#define KEYBOARD_LAYOUT_WIN_CH_H

#include <USBHIDKeyboard.h>

class KeyboardLayoutWinCH {
private:
  USBHIDKeyboard* keyboard;
  
  // Check if character requires AltGr handling
  bool isSwissSpecialChar(char c) {
    return (c == 'ä' || c == 'ö' || c == 'ü' || 
            c == 'Ä' || c == 'Ö' || c == 'Ü' || 
            c == '€' || c == '@' || c == '§' || c == '|' ||
            c == 'é' || c == 'è' || c == 'à' ||
            c == 'É' || c == 'È' || c == 'À' || c == 'Ç');
  }
  
  void typeSwissChar(char c) {
    // Official Swiss German keyboard layout mappings
    // AltGr = Right Alt = 0xE6
    
    if (c == 'ä') {
      // AltGr + ;
      keyboard->press(0xE6); // Right Alt
      keyboard->press(0x34); // ; key
      keyboard->releaseAll();
    } else if (c == 'ö') {
      // AltGr + [
      keyboard->press(0xE6); // Right Alt
      keyboard->press(0x33); // [ key
      keyboard->releaseAll();
    } else if (c == 'ü') {
      // AltGr + ]
      keyboard->press(0xE6); // Right Alt
      keyboard->press(0x2F); // ] key
      keyboard->releaseAll();
    } else if (c == 'Ä') {
      // Shift + AltGr + ;
      keyboard->press(0x02); // Shift
      keyboard->press(0xE6); // Right Alt
      keyboard->press(0x34); // ; key
      keyboard->releaseAll();
    } else if (c == 'Ö') {
      // Shift + AltGr + [
      keyboard->press(0x02); // Shift
      keyboard->press(0xE6); // Right Alt
      keyboard->press(0x33); // [ key
      keyboard->releaseAll();
    } else if (c == 'Ü') {
      // Shift + AltGr + ]
      keyboard->press(0x02); // Shift
      keyboard->press(0xE6); // Right Alt
      keyboard->press(0x2F); // ] key
      keyboard->releaseAll();
    } else if (c == '€') {
      // AltGr + E
      keyboard->press(0xE6); // Right Alt
      keyboard->press(0x08); // E key
      keyboard->releaseAll();
    } else if (c == '@') {
      // AltGr + 2
      keyboard->press(0xE6); // Right Alt
      keyboard->press(0x1F); // 2 key
      keyboard->releaseAll();
    } else if (c == '§') {
      // AltGr + 3
      keyboard->press(0xE6); // Right Alt
      keyboard->press(0x20); // 3 key
      keyboard->releaseAll();
    } else if (c == '|') {
      // AltGr + 7
      keyboard->press(0xE6); // Right Alt
      keyboard->press(0x24); // 7 key
      keyboard->releaseAll();
    } else if (c == 'é') {
      // AltGr + E, then 2
      keyboard->press(0xE6); // Right Alt
      keyboard->press(0x08); // E key
      keyboard->releaseAll();
      delay(10);
      keyboard->press(0x1F); // 2 key
      keyboard->releaseAll();
    } else if (c == 'è') {
      // AltGr + `
      keyboard->press(0xE6); // Right Alt
      keyboard->press(0x35); // ` key
      keyboard->releaseAll();
    } else if (c == 'à') {
      // AltGr + A
      keyboard->press(0xE6); // Right Alt
      keyboard->press(0x04); // A key
      keyboard->releaseAll();
    } else if (c == 'É') {
      // Shift + AltGr + E, then 2
      keyboard->press(0x02); // Shift
      keyboard->press(0xE6); // Right Alt
      keyboard->press(0x08); // E key
      keyboard->releaseAll();
      delay(10);
      keyboard->press(0x02); // Shift
      keyboard->press(0x1F); // 2 key
      keyboard->releaseAll();
    } else if (c == 'È') {
      // Shift + AltGr + `
      keyboard->press(0x02); // Shift
      keyboard->press(0xE6); // Right Alt
      keyboard->press(0x35); // ` key
      keyboard->releaseAll();
    } else if (c == 'À') {
      // Shift + AltGr + A
      keyboard->press(0x02); // Shift
      keyboard->press(0xE6); // Right Alt
      keyboard->press(0x04); // A key
      keyboard->releaseAll();
    } else if (c == 'Ç') {
      // Shift + AltGr + C
      keyboard->press(0x02); // Shift
      keyboard->press(0xE6); // Right Alt
      keyboard->press(0x06); // C key
      keyboard->releaseAll();
    }
  }
  
  uint8_t getBasicKeycode(char c) {
    // Convert character to HID keycode
    if (c >= 'a' && c <= 'z') {
      return 0x04 + (c - 'a'); // a=0x04, b=0x05, etc.
    }
    if (c >= 'A' && c <= 'Z') {
      return 0x04 + (c - 'A'); // Same codes, but with Shift modifier
    }
    if (c >= '1' && c <= '9') {
      return 0x1E + (c - '1'); // 1=0x1E, 2=0x1F, etc.
    }
    if (c == '0') return 0x27;
    
    // Special characters (Swiss QWERTZ layout - official mapping)
    switch (c) {
      case ' ': return 0x2C; // Space
      case '\n': return 0x28; // Enter
      case '\t': return 0x2B; // Tab
      case '!': return 0x1E; // 1 with Shift
      case '"': return 0x1F; // 2 with Shift
      case '*': return 0x20; // 3 with Shift
      case 'ç': return 0x21; // 4 with Shift
      case '%': return 0x22; // 5 with Shift
      case '&': return 0x23; // 6 with Shift
      case '/': return 0x24; // 7 with Shift
      case '(': return 0x25; // 8 with Shift
      case ')': return 0x26; // 9 with Shift
      case '=': return 0x27; // 0 with Shift
      case '?': return 0x2D; // - with Shift
      case '_': return 0x2E; // = with Shift
      case '-': return 0x2D;
      case '+': return 0x2E; // = key
      case ',': return 0x36;
      case '.': return 0x37;
      case ':': return 0x36; // , with Shift
      case ';': return 0x33; // [ key
      case '<': return 0x36; // , with Shift
      case '>': return 0x37; // . with Shift
      default: return 0;
    }
  }
  
  uint8_t getModifierForChar(char c) {
    // Return modifier needed for character
    if (c >= 'A' && c <= 'Z') {
      return 0x02; // Shift
    }
    
    // Characters that need Shift in Swiss layout
    switch (c) {
      case '!': case '"': case '*': case 'ç': case '%': 
      case '&': case '/': case '(': case ')': case '=':
      case '?': case '_': case ':': case '<': case '>':
        return 0x02; // Shift
      default:
        return 0;
    }
  }

public:
  KeyboardLayoutWinCH(USBHIDKeyboard* kb) : keyboard(kb) {}
  
  void write(String text) {
    // Note: Serial/DEBUG_PRINTLN not available in header file
    // Debug output would need to be added in padawan.ino if needed
    
    for (unsigned int i = 0; i < text.length(); i++) {
      char c = text[i];
      
      // Handle Swiss special characters (AltGr combinations)
      if (isSwissSpecialChar(c)) {
        typeSwissChar(c);
        delay(10); // Small delay between characters
        continue;
      }
      
      // Handle regular characters
      uint8_t keycode = getBasicKeycode(c);
      if (keycode == 0) {
        // Unknown character, skip
        continue;
      }
      
      uint8_t modifier = getModifierForChar(c);
      
      // USB HID requires modifier and key to be pressed together
      // We need to send them in a single report
      if (modifier != 0) {
        // For modifiers, we need to set the modifier byte and the keycode
        // USBHIDKeyboard might need both at once
        keyboard->press(modifier);
        delay(2);
        keyboard->press(keycode);
        delay(10); // Hold for a moment
        keyboard->releaseAll();
        delay(2);
      } else {
        keyboard->press(keycode);
        delay(10); // Hold for a moment
        keyboard->releaseAll();
        delay(2);
      }
      
      delay(10); // Small delay between characters
    }
  }
};

#endif // KEYBOARD_LAYOUT_WIN_CH_H

