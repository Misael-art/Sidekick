<div align="center">

# ⚡ Sidekick

### Visual Automation for Windows

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![React](https://img.shields.io/badge/React-18-61DAFB?style=for-the-badge&logo=react&logoColor=black)](https://react.dev/)
[![WPF](https://img.shields.io/badge/WPF-WebView2-0078D7?style=for-the-badge&logo=windows&logoColor=white)](https://learn.microsoft.com/en-us/microsoft-edge/webview2/)
[![License](https://img.shields.io/badge/License-MIT-22C55E?style=for-the-badge)](LICENSE)

**Build powerful Windows automations visually — no coding required.**

Sidekick is a modern, node-based automation tool that lets you create macros, workflows, and automated tasks through an intuitive drag-and-drop interface. Powered by .NET 8, React, and Windows UIAutomation.

---

[Features](#-features) •
[Architecture](#-architecture) •
[Getting Started](#-getting-started) •
[Node Library](#-node-library) •
[Plugin System](#-plugin-system) •
[Contributing](#-contributing)

</div>

---

## ✨ Features

<table>
<tr>
<td width="50%">

### 🎨 Visual Node Editor
Drag-and-drop nodes, connect ports, drop a wire on empty canvas to create the next step, or right-click for a searchable context menu.

### ⌨️ Global Hotkeys
Trigger any flow with system-wide keyboard shortcuts — even when Sidekick is minimized to tray.

### 🔄 Continuous Runtime
Arm trigger-based flows, keep watchers active in the background, and let Sidekick queue executions safely with per-flow coalescing.

### 🖱️ Mouse & Keyboard Automation
Simulate clicks, drags, key presses, and typed text at pixel-perfect coordinates.

</td>
<td width="50%">

### 🔍 Screen Detection
Detect pixel colors, find images on screen, and react to window events in real-time.

### 🧭 Mira & Snip Assets
Capture UI selectors with Mira, capture image templates with Snip, test selectors, score selector robustness, and create pre-filled nodes from the latest capture.

### 🔁 Logic & Control Flow
If/Else branching, loops, variables, delays, and text comparisons — all without writing code.

### 🪟 Desktop Automation
Wait for, read, click, focus, restore, and monitor real Windows desktop elements with `processName`, `processPath`, and title matching.

### 🖥️ Visual Overlays & Console
Show foreground color, image, and text overlays with timers/motion, and run controlled console commands with PWD, timeout, stdout, stderr, and exit-code outputs.

### 🛒 Local Recipe Marketplace
Open official built-in recipes from the toolbar Marketplace with search and safe local loading. Remote downloads are intentionally gated until package signing and capability warnings are implemented.

</td>
</tr>
</table>

---

## 🏗️ Architecture

```
┌──────────────────────────────────────────────────────────┐
│                    Sidekick.App (WPF)                    │
│  ┌──────────┐  ┌──────────────┐  ┌───────────────────┐  │
│  │ WebView2 │◄─┤ WebBridge    │──┤ BridgeRouter      │  │
│  │ (React)  │  │ (Messages)   │  │ (Command Handler) │  │
│  └──────────┘  └──────────────┘  └─────────┬─────────┘  │
│                                            │            │
│                                  ┌─────────▼─────────┐  │
│                                  │ FlowRuntimeManager│  │
│                                  │ Queue + Triggers  │  │
│                                  └───────────────────┘  │
│                        │                                 │
│  ┌────────────────────────────────────────────────────┐  │
│  │               System Tray Manager                  │  │
│  └────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────┘
                         │
          ┌──────────────┼──────────────┐
          ▼              ▼              ▼
   ┌─────────────┐ ┌──────────┐ ┌────────────┐
   │  Core       │ │  Nodes   │ │  Platform  │
   │             │ │          │ │            │
   │ • Executor  │ │ • Actions│ │ • Mouse    │
   │ • Registry  │ │ • Triggers│ │ • Keyboard│
   │ • Validator │ │ • Logic  │ │ • Screen   │
   │ • Serialize │ │          │ │ • UIAuto   │
   └─────────────┘ └──────────┘ └────────────┘
```

| Layer | Project | Description |
|:---|:---|:---|
| **App** | `Ajudante.App` | WPF host with WebView2, system tray, and bridge layer |
| **UI** | `Ajudante.UI` | React + TypeScript + Vite — the visual node editor |
| **Core** | `Ajudante.Core` | Flow engine, node registry, serialization, validation |
| **Nodes** | `Ajudante.Nodes` | Built-in action, trigger, and logic nodes |
| **Platform** | `Ajudante.Platform` | Win32 interop — mouse, keyboard, screen, UIAutomation |

---

## 🚀 Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 18+](https://nodejs.org/) (for the UI)
- Windows 10/11

### Build & Run

```bash
# 1. Clone the repository
git clone https://github.com/Misael-art/Sidekick.git
cd Sidekick

# 2. Build the frontend
cd src/Ajudante.UI
npm install
npm run build
cd ../..

# 3. Build and run the app
dotnet build
dotnet run --project src/Ajudante.App
```

### Development with Hot Reload

For a faster frontend development loop, Sidekick supports Vite hot-reload:

```bash
# Terminal 1: Start the Vite dev server
cd src/Ajudante.UI
npm run dev

# Terminal 2: Run the WPF app in Debug mode
dotnet run --project src/Ajudante.App -c Debug
```

> In Debug mode, the app automatically detects the Vite dev server on `localhost:5173` and connects to it, so any React changes are reflected instantly without rebuilding.

### Run Tests

```bash
dotnet test Ajudante.sln
cd src/Ajudante.UI
npm run test
```

> Automated tests cover the Core engine, runtime orchestration, sample flows, bridge contracts, trigger lifecycle, built-in nodes, and the React stores/components that surface runtime state.

### Sample Flows

The `flows/` folder includes ready-to-open examples and recipes:

- `portfolio_snip_reuse_demo.json` shows Snip asset reuse in `Image Detected Trigger`.
- `portfolio_browser_mira_demo.json` shows Mira selector reuse in `Browser Wait Element` and `Browser Click`.
- `portfolio_browser_mira_text_demo.json` shows Mira selector reuse in `Browser Type` and `Browser Extract Text`.
- `recipe_desktop_automation.json` shows capture/apply/read with desktop selectors.
- `recipe_wait_text_then_click.json` waits for visible text and then clicks a desktop button.
- `recipe_popup_auto_confirm.json` arms an auto-confirm popup flow with debounce/cooldown/max repeat.
- `recipe_visual_fallback_click.json` clicks a screen image match when UIAutomation is not enough.
- `recipe_scheduler_interval.json` shows interval-based automation.
- `recipe_overlay_visual_message.json` shows a fullscreen color/text overlay with timer and motion.
- `recipe_console_pwd_command.json` shows PWD setup, console command execution, stdout capture, and logging.
- `trae_auto_continue.json` is the official Trae Continue flow using a desktop element trigger, window focus, cooldown, debounce, and max repeat.

### Runtime Data & Compatibility

- Official data directory: `%AppData%/Sidekick/`
- Legacy directory still recognized: `%AppData%/Ajudante/`
- On first run after upgrade, Sidekick migrates legacy flows, logs, and plugins into the official Sidekick folder without deleting the old folder.

---

## 📦 Node Library

### 🔴 Triggers
| Node | Description |
|:---|:---|
| **Hotkey Trigger** | Fires on a global keyboard shortcut (e.g., `Ctrl+F5`) |
| **File System Trigger** | Watches a folder for file changes |
| **Pixel Change Trigger** | Detects when a screen region changes color |
| **Image Detected Trigger** | Fires when a reference image appears on screen |
| **Window Event Trigger** | Reacts to window open/close/focus events |
| **Desktop Element Appeared** | Fires when a desktop UIAutomation element appears |
| **Desktop Text Changed** | Fires when a desktop element text changes |
| **Schedule Time** | Fires once per day at a configured local time |
| **Interval** | Fires repeatedly at a fixed interval |
| **Process Event** | Fires when a process starts or stops |

### 🟢 Actions
| Node | Description |
|:---|:---|
| **Mouse Click** | Clicks at specified coordinates (Left/Right/Middle, Single/Double) |
| **Mouse Move** | Moves the cursor to a position |
| **Mouse Drag** | Drags from one point to another |
| **Keyboard Press** | Simulates key press/release combinations |
| **Keyboard Type** | Types a text string with configurable speed |
| **Open Program** | Launches an executable or file |
| **Kill Process** | Terminates a running process by name |
| **Wait Process** | Waits for a process to start or stop |
| **Window Control** | Focuses, brings forward, minimizes, maximizes, or restores a desktop window |
| **Desktop Wait Element** | Waits for a desktop element with process-aware selectors |
| **Desktop Click Element** | Clicks a desktop element, falling back to coordinates when needed |
| **Desktop Read Element Text** | Reads text from a desktop element |
| **Click Image Match** | Finds a screen image and clicks the match center |
| **Overlay Solid Color** | Covers the screen or a region with a configurable foreground color overlay |
| **Overlay Image** | Shows image overlays with fit, background, timer, plane, fullscreen, and motion controls |
| **Overlay Text** | Shows text overlays with font, color, effects, alignment, background, timer, plane, fullscreen, and motion controls |
| **Console Set Directory** | Sets the flow PWD variable for command automation |
| **Console Command** | Runs direct/cmd/powershell commands with timeout, stdout, stderr, exit code, and error routing |
| **Delete File** | Removes a file from disk |
| **Play Sound** | Plays a WAV audio file |

### 🟡 Logic
| Node | Description |
|:---|:---|
| **If / Else** | Branches based on variable comparisons or pixel colors |
| **Loop** | Repeats a block of nodes N times with optional delay |
| **Delay** | Pauses execution for a specified duration |
| **Set Variable** | Stores a value in the flow context |
| **Get Variable** | Reads a value from the flow context |
| **Compare Text** | Branches on string equality, contains, starts/ends with |
| **Cooldown** | Routes repeated executions through a cooldown branch |

---

## 🧩 Plugin System

Extend Sidekick by creating custom node DLLs:

```csharp
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;

[NodeInfo(
    TypeId = "action.myCustomAction",
    DisplayName = "My Custom Action",
    Category = NodeCategory.Action,
    Description = "Does something amazing")]
public class MyCustomAction : IActionNode
{
    public string Id { get; set; } = "";
    public NodeDefinition Definition => new() { /* ... */ };

    public void Configure(Dictionary<string, object?> properties) { }

    public Task<NodeResult> ExecuteAsync(
        FlowExecutionContext context, CancellationToken ct)
    {
        // Your automation logic here
        return Task.FromResult(NodeResult.Ok("out"));
    }
}
```

Drop the compiled DLL into `%AppData%/Sidekick/plugins/` and restart — Sidekick loads it automatically using an **isolated AssemblyLoadContext** that prevents file-locking, so you can update plugins without closing the app.

---

## 📁 Project Structure

```
Sidekick/
├── src/
│   ├── Ajudante.App/          # WPF host application
│   │   ├── Bridge/            # WebView2 ↔ React message bridge
│   │   ├── TrayIcon/          # System tray manager
│   │   ├── Overlays/          # Screen overlay windows
│   │   └── wwwroot/           # Compiled React output
│   ├── Ajudante.Core/         # Engine & infrastructure
│   │   ├── Engine/            # FlowExecutor, Validator, Context
│   │   ├── Interfaces/        # INode, ITriggerNode, IActionNode
│   │   ├── Models/            # Flow, NodeDefinition, etc.
│   │   ├── Registry/          # NodeRegistry + PluginLoadContext
│   │   └── Serialization/     # JSON flow persistence
│   ├── Ajudante.Nodes/        # Built-in nodes
│   │   ├── Actions/           # Mouse, keyboard, process, file
│   │   ├── Triggers/          # Hotkey, file system, pixel, image
│   │   └── Logic/             # If/else, loop, delay, variables
│   ├── Ajudante.Platform/     # Windows interop layer
│   │   ├── Input/             # Mouse/keyboard simulation, hotkeys
│   │   ├── Screen/            # Pixel reader, image search
│   │   └── UIAutomation/      # Windows UIAutomation wrappers
│   └── Ajudante.UI/           # React + Vite frontend
│       └── src/               # TypeScript components
├── tests/
│   ├── Ajudante.Core.Tests/   # 80 unit tests
│   └── Ajudante.Nodes.Tests/  # 76 unit tests
└── Ajudante.sln
```

---

## 🔧 Publishing

### Build a Release

```bash
cd F:/Projects/Ajudante
dotnet publish .\src\Ajudante.App\Ajudante.App.csproj -c Release -o .\src\Ajudante.App\bin\publish
```

This produces `Sidekick.exe` and its runtime assets at `src/Ajudante.App/bin/publish/`. Close any running `Sidekick.exe` from that folder before publishing, otherwise Windows will keep DLLs locked.

Release validation snapshot `2026-04-30`:

- `dotnet build Ajudante.sln`: passed, 0 errors/0 warnings
- `dotnet test Ajudante.sln --no-build`: passed, 247 tests
- `npm run test`: passed, 36 tests
- `npm run build`: passed, generated `index-D1Xl9ODN.js` and `index-xxIiRT3a.css`
- `dotnet publish .\src\Ajudante.App\Ajudante.App.csproj -c Release -o .\src\Ajudante.App\bin\publish`: passed, generated `Sidekick.exe`

Known release caveats:

- OCR is not yet a product feature; visual fallback currently means image template matching.
- Re-publishing to `bin/publish` requires closing any running `Sidekick.exe` from that folder first, otherwise Windows keeps DLLs locked.

### Create an Installer

An [InnoSetup](https://jrsoftware.org/isinfo.php) script is included at `src/Ajudante.App/installer.iss`. After publishing:

1. Install [InnoSetup 6](https://jrsoftware.org/isdl.php)
2. Open `installer.iss` in the InnoSetup Compiler
3. Click **Build** → produces `Sidekick_Setup_1.0.0.exe`

The installer includes:
- Desktop shortcut (optional)
- Start Menu group
- Optional Windows startup registration
- Clean uninstaller

---

## 🤝 Contributing

Contributions are welcome! Here's how to get started:

1. **Fork** the repository
2. **Create** a feature branch (`git checkout -b feature/amazing-node`)
3. **Commit** your changes (`git commit -m 'Add amazing node'`)
4. **Push** to the branch (`git push origin feature/amazing-node`)
5. **Open** a Pull Request

### Development Guidelines

- Follow existing code patterns and naming conventions
- Add unit tests for new nodes
- Keep nodes focused — one responsibility per node
- Use `NodeResult.Ok()` / `NodeResult.Fail()` for consistent error handling

---

## 📄 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

---

<div align="center">
<br>

**Built with ❤️ by [Misael](https://github.com/Misael-art)**

<sub>Powered by .NET 8 • React 18 • WebView2 • Windows UIAutomation</sub>

</div>
