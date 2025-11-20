# Contributing to Padawan

Thank you for your interest in contributing to Padawan! This document provides guidelines and instructions for contributing.

**Note**: This repository contains both:
- **Padawan** - The macro pad hardware firmware (Arduino)
- **PadAwan-Force** - The desktop configuration software (Avalonia UI)

## How to Contribute

### Reporting Bugs

If you find a bug, please open an issue with:
- A clear, descriptive title
- Steps to reproduce the bug
- Expected behavior
- Actual behavior
- Your hardware setup (board version, etc.)
- Any relevant logs or error messages

### Suggesting Features

Feature suggestions are welcome! Please open an issue with:
- A clear description of the feature
- Use case / why it would be useful
- Any implementation ideas (optional)

### Code Contributions

1. **Fork the repository**
2. **Create a feature branch** (`git checkout -b feature/amazing-feature`)
3. **Make your changes**
   - Follow the existing code style
   - Add comments for complex logic
   - Update documentation if needed
4. **Test your changes**
   - Test on Windows (primary platform)
   - Test with actual hardware if possible
5. **Commit your changes** (`git commit -m 'Add amazing feature'`)
   - Use clear, descriptive commit messages
6. **Push to your branch** (`git push origin feature/amazing-feature`)
7. **Open a Pull Request**

## Code Style

### C# (Desktop Application)
- Use 4 spaces for indentation
- Follow C# naming conventions
- Use meaningful variable and method names
- Add XML comments for public methods

### Arduino/C++ (Firmware)
- Use 2 spaces for indentation
- Follow Arduino naming conventions
- Use `DEBUG_PRINTLN` for debug output
- Comment complex logic

## Development Setup

See the main [README.md](README.md) for setup instructions.

## Questions?

Feel free to open an issue for any questions or clarifications!

