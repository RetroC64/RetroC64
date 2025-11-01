# RetroC64 User Guide

Build, assemble, and run genuine 6502 programs for the Commodore 64 using C# and .NET. RetroC64 integrates a fluent 6502 assembler (Asm6502), graphics helpers (SkiaSharp for sprites), SID music relocation/playback, and C64-specific utilities - all from one .NET project.

This guide covers basics to advanced usage with the included examples and the RetroC64.Core helpers.

- [Prerequisites](#prerequisites)
  - [Emulator Setup (VICE)](#emulator-setup-vice)
- [Build and Run Modes](#build-and-run-modes)
- [Quick Start](#quick-start)
- [Core Concepts](#core-concepts)
- [Examples](#examples)
  - [BASIC Program](#basic-program)
  - [Assembler Program](#assembler-program)
- [Debugger](#debugger)
  - [Debugger Limitations](#debugger-limitations)
- [Troubleshooting](#troubleshooting)


## Prerequisites

- .NET SDK 9.0 or later
- A C64 emulator such as VICE (optional for auto-run, still useful to test PRG output)
- OS: Windows/macOS/Linux

### Emulator Setup (VICE)

Install the VICE emulator from https://vice-emu.sourceforge.io/ and set the `RETROC64_VICE_BIN` environment variable to the x64sc binary so RetroC64 can launch it in run mode:
- Windows (x64sc.exe):
  - Example: `RETROC64_VICE_BIN=C:\Program Files\c64\GTK3VICE-3.9-win64\bin\x64sc.exe`
- macOS / Linux:
  - Example: `RETROC64_VICE_BIN=/usr/bin/x64sc`

When `RETROC64_VICE_BIN` is set, RetroC64 will auto-launch VICE after building.

## Build and Run Modes

RetroC64 apps support two modes:
- build: Produce PRG and assets into .retroc64/build at the project root.
- run: Build and automatically launch the program in VICE (requires `RETROC64_VICE_BIN` pointing to x64sc/x64sc.exe).

Notes:
- Build artifacts always go to .retroc64/build.

## Quick Start

- Run the BASIC example:
  - `dotnet run --project examples/HelloBasic`
- Run the assembler demo:
  - Place `Sanxion.sid` next to the example (if not already included).
  - `dotnet run --project examples/C64NETConf2025`

The RetroC64 runner builds the program and, if configured, can launch your emulator. Otherwise, you can take the emitted PRG and run it with your emulator.

Also try live coding:
- From `examples/C64NETConf2025` you can launch live coding with `dotnet watch -- run`
  - RetroC64 will rebuild on code changes and reload the effect directly in VICE (run mode).

## Core Concepts

- App entrypoint: `C64AppBuilder.Run<T>(args)`
  - Implement either:
    - `C64AppBasic`: provide BASIC text
    - `C64AppAsmProgram`: generate 6502 code/data via Asm6502
- Assembler [Asm6502](https://github.com/xoofx/Asm6502):
  - The class `C64Assembler` derives from `Mos6502Assembler` and provides an integrated Zero-Page  allocator.
  - Fluent API: `LDA_Imm(value)`, `STA(addr)`, labels, sections, loops, etc.
  - Labels define code/data addresses; data is arranged via `ArrangeBlocks`.
- Memory and registers:
  - Constants in `RetroC64.C64Registers` (e.g., `VIC2_BG_COLOR0`, `IRQ_VECTOR`)
  - Use zero-page via `asm.ZpAlloc(out var zpVar)`
- Data layout:
  - `asm.BeginCodeSection` / `asm.EndCodeSection`
  - `asm.BeginDataSection` / `asm.ArrangeBlocks` / `asm.EndDataSection`
  - Constrained blocks (e.g., sprites aligned to 64 bytes, SID at `$1000`)
- Helpers (`RetroC64.Core C64AssemblerExtensions`):
  - `SetupRamAccess`, `SetupRasterIrq`, `DisableNmi`, `Clear`/`Copy` memory, `Push`/`Pop` registers, PAL/NTSC detection, general utilities.

---

## Examples

### BASIC Program

Prints "HELLO, WORLD" with a counter placeholder:

```csharp
using RetroC64.App;

// A program is a command line app that builds and runs a 6510 assembly program.
return await C64AppBuilder.Run<HelloBasic>(args);

/// <summary>
/// Represents a BASIC program that prints "HELLO, WORLD" 
/// Demonstrates simple variable usage for RetroC64.
/// </summary>
internal class HelloBasic : C64AppBasic
{
    public HelloBasic()
    {
        Text = """
               10 X = 1
               20 PRINT "HELLO, WORLD" X
               30 REM X = X + 1
               40 REM GOTO 20
               """;
    }

    protected override void Initialize(C64AppInitializeContext context)
    {
        // Can perform additional initialization here if needed
    }
}
```

Run with `dotnet run` and it will display the following screen:

![HelloAsm Example](RetroC64-HelloBasic.png)


### Assembler Program

Changes background and border colors:

```csharp
using Asm6502;
using RetroC64;
using RetroC64.App;
using static RetroC64.C64Registers;

// A program is a command line app that builds and runs a 6510 assembly program.
return await C64AppBuilder.Run<HelloAsm>(args);

/// <summary>
/// A simple assembler program that changes the background and border colors.
/// </summary>
public class HelloAsm : C64AppAsmProgram
{
    protected override Mos6502Label Build(C64AppBuildContext context, C64Assembler asm)
    {
        asm.Label(out var start)
            .BeginCodeSection("Main")
            .LDA_Imm(COLOR_RED)
            .STA(VIC2_BG_COLOR0)
            .LDA_Imm(COLOR_GREEN)
            .STA(VIC2_BORDER_COLOR)
            .InfiniteLoop()
            .EndCodeSection();
        return start;
    }
}
```

Run: `dotnet watch -- run` with launch VICE and allow live coding. It will display the following screen:

![HelloAsm Example](RetroC64-HelloAsm.png)

## Debugger

Install the official VSCode extension from the Marketplace:
- https://marketplace.visualstudio.com/items?itemName=xoofx.retro-c64

This is the official Visual Studio Code extension for the RetroC64 debugger. The debugger is actually implemented entirely in the RetroC64 .NET runtime by using the Debug Adapter Protocol (DAP). This extension provides only the necessary VSCode integration to connect to the RetroC64 debugger easily from within VSCode.

Features:
- Attach to debugger: Connect to RetroC64 debugger running on port 6503 (default)
- Register access: Full read-write access to CPU registers, CPU flags, stack, and zero page addresses
- Hardware registers: Access to VIC and SID registers
- Breakpoints: Set code breakpoints and data breakpoints (watchpoints)
- Execution control: Step-in, step-over, step-out, pause, and continue
- Memory inspection: View RAM contents
- Code analysis: View disassembly

In order to use the debugger you simply need to launch your RetroC64 with `run` and create the  following `launch.json` file in your project and press F5 to start debugging:

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "RetroC64 Attach",
      "type": "RetroC64",
      "request": "attach",
      "debugServer": 6503
    }
  ]
}
```

![RetroC64 Debugger](RetroC64-Debugger.png)

### Debugger Limitations

- Live coding with `dotnet watch` might not always work as expected when debugging when lines are changing.

---

## Troubleshooting

TODO

- In order for a project to run with `dotnet watch`, the project should not define `<PublishAot>true</PublishAot>` in its .csproj file otherwise the experience will not work as expected.