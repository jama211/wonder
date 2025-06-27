# 🎮 Wonder Game

> *A text-based adventure that boots like an old computer, acts like a classic terminal, then transforms into a top-down RPG where words literally become your world.*

Welcome to Wonder Game - where nostalgia meets innovation, and where the phrase "text-based graphics" actually makes sense!

## 🎭 What Is This Magnificent Creation?

Wonder Game is a multi-layered gaming experience that starts innocent enough with a realistic Linux boot sequence, transitions into a classic green-on-black terminal adventure, and then - plot twist! - transforms into a 2D RPG where text *literally* becomes the graphics.

Imagine if your old computer's boot sequence had a personality disorder and decided to become a game. That's Wonder Game.

**Key Features:**
- 🖥️ Authentic retro boot sequence (735 lines of pure nostalgia)
- 🎪 Classic text adventure interface with mysterious commands
- 🔄 Mind-bending transformation when you "look harder" 
- 🎨 Revolutionary text-as-graphics system (where "TABLE" is actually a table)
- 🏗️ Built-in level editor for when you want to spell out your furniture
- 🚪 Room-to-room navigation system
- 📝 JSON-based room definitions (because who doesn't love JSON?)

## 🛠️ Development Setup

### Prerequisites

You'll need .NET 9 SDK installed. If you're on an Apple Silicon Mac, this gets interesting...

**For Apple Silicon Macs (M1/M2/M3/M4):**
You need *both* ARM64 and x64 versions of .NET because MonoGame's Content Builder is picky about architecture. Don't ask why, just embrace the chaos.

```bash
# Install ARM64 version (for development)
brew install --cask dotnet-sdk

# Install x64 version (for MonoGame Content Builder)
arch -x86_64 /usr/local/bin/brew install --cask dotnet-sdk

# Add this to your ~/.zshrc for sanity
echo 'export PATH="/usr/local/share/dotnet/x64:$PATH"' >> ~/.zshrc
```

**For Everyone Else:**
```bash
# Just install .NET 9 SDK like a normal person
# https://dotnet.microsoft.com/download
```

### Building & Running

**The Simple Way:**
```bash
# Clone this masterpiece
git clone <your-repo-url>
cd wonder

# One-time setup: install MonoGame Content Builder tool
dotnet tool restore

# Build it (and pray to the MonoGame gods)
dotnet build

# Run it and watch the magic happen
dotnet run
```

**If Things Go Wrong:**
- Make sure you have the right .NET version with `dotnet --version`
- Try `dotnet restore` if packages are being difficult
- Sacrifice a rubber duck to the debugging deities
- Check that your graphics drivers aren't from the stone age

## 🧪 Test Suite

Wonder Game includes a comprehensive test suite for the enhanced command processing system. You can run it anytime to verify that all the smart command parsing features work correctly.

**Run the tests:**
```bash
dotnet run -- run-tests
```

**What gets tested:**
- ✅ **Preposition filtering** - `"look at bunk"` works the same as `"look bunk"`
- ✅ **Command synonyms** - `"observe"` → `"look"`, `"inspect"` → `"examine"`, `"feel"` → `"touch"`
- ✅ **Object recognition** - `"computer"` → `"terminal"`, `"bed"` → `"bunk"`, `"note"` → `"post-it"`
- ✅ **Case insensitivity** - `"LOOK AT THE TERMINAL"` works perfectly
- ✅ **Complex phrases** - `"inspect that yellow sticky note on the computer"` parses correctly

**Sample output:**
```
🧪 Wonder Game Command Processing Tests
✅ Preposition filtering works
✅ Command synonyms work  
✅ Object synonyms work
✅ Case insensitive normalization works

📊 Success Rate: 100.0%
🎯 Summary: Enhanced command parsing is working perfectly!
```

The test suite requires no external dependencies and runs entirely through the standard .NET runtime. Perfect for validating functionality during development or after making changes!

## 🎮 How to Play

1. **Boot Sequence**: Watch the authentic Linux boot messages scroll by. Resist the urge to press Ctrl+C.

2. **Terminal Phase**: You're in a classic terminal interface. Try commands like:
   - `help` - For when you're lost (we've all been there)
   - `look` - Observe your surroundings like a proper adventurer
   - `clear` - Clean that screen like Marie Kondo
   - `look harder` - The magic words that change everything ✨

3. **RPG Phase**: Now you're in a 2D world where:
   - Arrow keys move your `@` character around
   - `E` interacts with objects (or tries to)
   - `P` opens the level editor (for the creative souls)
   - `Escape` takes you back to the terminal (if you're feeling nostalgic)

## 📦 Distribution (The Fun Part)

### Self-Contained Builds (Recommended)
*"Include everything and the kitchen sink"*

These builds bundle the entire .NET runtime, so your users don't need to install anything. Perfect for when you want to share your creation without technical support calls.

```bash
# macOS (Apple Silicon - M1/M2/M3/M4)
dotnet publish -c Release --self-contained --runtime osx-arm64 -o ./publish/macos-arm64

# macOS (Intel - for the vintage Mac enthusiasts)
dotnet publish -c Release --self-contained --runtime osx-x64 -o ./publish/macos-intel

# Windows (Because someone has to)
dotnet publish -c Release --self-contained --runtime win-x64 -o ./publish/windows

# Linux (For the server room heroes)
dotnet publish -c Release --self-contained --runtime linux-x64 -o ./publish/linux
```

**Pros:** Works everywhere, no dependencies, just works™
**Cons:** Slightly larger files (but hey, it's not 1995 anymore)

### Framework-Dependent Builds
*"Assume people know what they're doing"*

These are smaller but require users to have .NET 9 runtime installed. Use at your own risk.

```bash
# The optimistic approach
dotnet publish -c Release -o ./publish/framework-dependent
```

**Pros:** Smaller files, faster uploads
**Cons:** "It works on my machine" syndrome, potential support headaches

### Quick Distribution Script

Want to build for all platforms at once? Because efficiency is beautiful:

```bash
# Build everything (patience required)
dotnet publish -c Release --self-contained --runtime osx-arm64 -o ./publish/macos-arm64
dotnet publish -c Release --self-contained --runtime osx-x64 -o ./publish/macos-intel  
dotnet publish -c Release --self-contained --runtime win-x64 -o ./publish/windows
dotnet publish -c Release --self-contained --runtime linux-x64 -o ./publish/linux

# Zip them up for distribution
cd publish
zip -r wonder-macos-arm64.zip macos-arm64/
zip -r wonder-macos-intel.zip macos-intel/
zip -r wonder-windows.zip windows/
zip -r wonder-linux.zip linux/
```

## 🏗️ Level Editor

Press `P` in the RPG mode to access the built-in level editor. It's like Photoshop, but for text, and with more collision detection.

**Editor Controls:**
- Click to select objects
- Drag to move them around like digital furniture
- Ctrl+D to duplicate (because copy-paste is life)
- Property inspector for tweaking the details
- Save/Revert buttons for when you've made questionable design choices

## 🗂️ Project Structure

```
wonder/
├── Content/           # MonoGame content pipeline files
├── Core/             # Core game systems and utilities  
├── Data/             # Room definitions and boot sequence
├── Screens/          # Different game screens/states
├── WonderGame/       # Data models and game objects
└── publish/          # Build outputs (gitignored for your sanity)
```

## 🐛 Troubleshooting

**Game won't start?**
- Check you have the right .NET version
- Make sure graphics drivers are up to date
- Try turning it off and on again (classic)

**Fonts look weird?**
- MonoGame Content Builder might be having a moment
- Check that FreeType is properly installed (macOS users especially)

**Performance issues?**
- This is a text-based game. If you're having performance issues, maybe it's time for new hardware 😄

## 🤝 Contributing

Found a bug? Want to add a feature? Think the boot sequence needs more cowbell? Pull requests welcome!

Just remember:
- Keep the retro aesthetic alive
- Text-as-graphics is not a bug, it's a feature
- When in doubt, add more terminal green

## 📜 License

Copyright (c) 2024 Jamie Williamson. All rights reserved.

<!--
This software and associated documentation files are the proprietary 
property of Jamie Williamson. No part may be reproduced, distributed, or 
transmitted in any form without prior written permission.
-->

---

*Built with MonoGame, powered by caffeine, and inspired by the golden age of computing when loading a game was an adventure in itself.*
