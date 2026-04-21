# v0ided-decomp

One-way decompiler for game decompilation.

## GUI Application

A cross-platform GUI tool built with Avalonia (.NET) that provides unified decompilation for both Godot and LÖVE2D games.

### Features

- **Godot Decompiler**: Uses GDRE Tools CLI to decompile .pck, .exe, and .apk files
- **LÖVE2D Decompiler**: Extracts and processes .love files with options for:
  - Fix Goto statements
  - Port shaders to GLSL ES
  - Minify Lua scripts

### Requirements

- .NET 8 Runtime (or .NET 10 for macOS)
- Python 3 (for LÖVE2D processing)
- GDRE Tools (for Godot decompilation - see below)

### Setup

1. **Download GDRE Tools** (for Godot decompilation):
   - Go to: https://github.com/GDRETools/gdsdecomp/releases
   - Download the latest release for your platform
   - Extract and place the contents in: `godot/gdre_tools/`

2. **Run the GUI**:
   ```bash
   # Option 1: Run from publish folder
   ./publish/v0ided-decomp

   # Option 2: Run with dotnet
   cd V0idedDecomp
   dotnet run
   ```

### Output Locations

Decompiled files are saved to:
- Godot: `~/Documents/v0ided-decomp/godot/[game_name]/`
- LÖVE2D: `~/Documents/v0ided-decomp/love2d/[game_name]/`

### Project Structure

```
v0ided-decomp/
├── V0idedDecomp/          # Avalonia .NET project
│   ├── Views/             # UI views
│   ├── ViewModels/        # MVVM view models
│   └── Assets/
├── godot/
│   └── gdre_tools/        # Place GDRE Tools here
├── love2d/
│   ├── repack.py         # Main extraction script
│   └── slop/             # Processing scripts
└── publish/              # Built application
```

### Building

```bash
cd V0idedDecomp
dotnet publish -c Release -r osx-x64 --self-contained false -o ../publish
```

### License

See LICENSE file.
