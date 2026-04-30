# AGENTS.md — Ajudante: Documentacao Oficial e Regras de Atuacao

> **LEIA TUDO ANTES DE TOCAR EM QUALQUER ARQUIVO.**
> Este documento define as regras permanentes de arquitetura e atuacao.
> Para estado atual, frente ativa e continuidade, leia tambem `PROJECT_STATUS.md` e o RFC da frente em execucao.
> Se houver conflito entre uma descricao historica deste arquivo e `PROJECT_STATUS.md`, prevalece `PROJECT_STATUS.md` para o estado atual.
> Qualquer IA que atue neste repositorio DEVE seguir cada regra a risca.

---

## 1. O QUE E O AJUDANTE

Ajudante e uma ferramenta de automacao visual (RPA — Robotic Process Automation) para Windows.
O usuario cria automacoes conectando blocos visuais (nodes) com fios em uma interface grafica — sem escrever codigo.

**Stack tecnologico:**
- **Backend:** C# .NET 8 (4 projetos)
- **Frontend:** React 18 + TypeScript + React Flow v12 (`@xyflow/react`) + Zustand
- **Build do frontend:** Vite
- **Host desktop:** WPF com WebView2
- **Deteccao de imagem:** Emgu CV (OpenCV)
- **Inspecao de UI:** Windows UIAutomation API
- **Simulacao de input:** Win32 SendInput API
- **System tray:** Hardcodet.NotifyIcon.Wpf

---

## 2. ESTRUTURA DO PROJETO

```
Ajudante/
├── Ajudante.sln                           # Solucao .NET com 4 projetos
├── AGENTS.md                              # ESTE ARQUIVO — regras obrigatorias
├── .gitignore
│
├── src/
│   ├── Ajudante.Core/                     # Motor puro — ZERO dependencias de UI
│   │   ├── Ajudante.Core.csproj           # net8.0
│   │   ├── Interfaces/                    # Contratos (INode, ITriggerNode, etc.)
│   │   ├── Models/                        # DTOs e enums (Flow, NodeDefinition, etc.)
│   │   ├── Engine/                        # Executor, TriggerManager, Validator
│   │   ├── Registry/                      # NodeRegistry (descobre nodes via reflection)
│   │   └── Serialization/                 # FlowSerializer (JSON save/load)
│   │
│   ├── Ajudante.Platform/                 # Servicos Windows nativos
│   │   ├── Ajudante.Platform.csproj       # net8.0-windows, UseWPF, AllowUnsafeBlocks
│   │   ├── Input/                         # MouseSimulator, KeyboardSimulator, GlobalHotkeyManager
│   │   ├── Screen/                        # ScreenCapture, TemplateMatching, PixelReader
│   │   ├── UIAutomation/                  # ElementInspector, ElementInfo
│   │   ├── Windows/                       # WindowWatcher, ProcessWatcher
│   │   └── FileSystem/                    # FileWatcher
│   │
│   ├── Ajudante.Nodes/                    # 20 nodes embutidos
│   │   ├── Ajudante.Nodes.csproj          # net8.0-windows, referencia Core + Platform
│   │   ├── Triggers/                      # 5 trigger nodes (vermelho #EF4444)
│   │   ├── Logic/                         # 6 logic nodes  (amarelo #EAB308)
│   │   └── Actions/                       # 9 action nodes (verde #22C55E)
│   │
│   ├── Ajudante.App/                      # Aplicacao WPF host
│   │   ├── Ajudante.App.csproj            # net8.0-windows, WinExe, WebView2, NotifyIcon
│   │   ├── App.xaml / App.xaml.cs          # Lifecycle, mutex, exception handling
│   │   ├── MainWindow.xaml / .xaml.cs      # WebView2 host + titulo customizado
│   │   ├── Bridge/                        # WebBridge, BridgeRouter, BridgeMessage
│   │   ├── TrayIcon/                      # SystemTrayManager
│   │   └── Overlays/                      # MiraWindow, SnipWindow
│   │
│   └── Ajudante.UI/                       # Frontend React
│       ├── package.json                   # React 18, @xyflow/react, zustand, vite
│       ├── vite.config.ts                 # build → ../Ajudante.App/wwwroot/
│       └── src/
│           ├── bridge/                    # types.ts, bridge.ts (comunicacao C#↔JS)
│           ├── store/                     # flowStore.ts, appStore.ts (Zustand)
│           ├── components/
│           │   ├── Canvas/                # FlowCanvas.tsx
│           │   ├── Nodes/                 # BaseNode, TriggerNode, LogicNode, ActionNode
│           │   ├── Sidebar/               # NodePalette, PropertyPanel
│           │   ├── Toolbar/               # Toolbar.tsx
│           │   └── StatusBar/             # ExecutionStatus.tsx
│           ├── styles/                    # global.css (tema escuro)
│           ├── App.tsx                    # Layout principal
│           └── main.tsx                   # Entry point
│
├── tests/                                 # (estrutura criada, testes pendentes)
│   ├── Ajudante.Core.Tests/
│   ├── Ajudante.Nodes.Tests/
│   └── Ajudante.Platform.Tests/
│
└── assets/
    ├── icons/
    └── sounds/
```

---

## 3. GRAFO DE DEPENDENCIAS (nao violar)

```
Ajudante.Core          ← referenciado por todos, nao referencia ninguem
    ↑
Ajudante.Platform      ← referencia Core
    ↑
Ajudante.Nodes         ← referencia Core + Platform
    ↑
Ajudante.App           ← referencia Core + Platform + Nodes
```

**REGRA ABSOLUTA:** A direcao de dependencia e de BAIXO para CIMA. Nunca:
- Core referenciar Platform, Nodes ou App
- Platform referenciar Nodes ou App
- Nodes referenciar App

---

## 4. REGRAS RIGIDAS DE ATUACAO

### 4.1 Regras Gerais

1. **NUNCA altere a estrutura de projetos/dependencias** sem autorizacao explicita do usuario.
2. **NUNCA remova ou renomeie** interfaces publicas existentes em `Ajudante.Core.Interfaces/`.
3. **SEMPRE compile a solucao inteira** (`dotnet build Ajudante.sln`) apos qualquer mudanca em C#.
4. **SEMPRE execute `npm run build`** em `src/Ajudante.UI/` apos mudancas no frontend.
5. **NUNCA commite codigo que nao compile** com 0 erros.
6. **Use o PATH correto:** `export PATH="/c/Program Files/dotnet:$PATH"` antes de `dotnet`.
7. **Leia um arquivo ANTES de editar.** Nunca presuma conteudo.
8. **Antes de qualquer mudanca relevante, leia nesta ordem:** `AGENTS.md` -> `PROJECT_STATUS.md` -> RFC da frente ativa -> arquivos afetados.

### 4.2 Regras do C# Backend

8. **Toda classe estavel e `sealed`** no Platform e Nodes. Nao remova `sealed` sem razao.
9. **Classes de servico no Platform sao `static`:** MouseSimulator, KeyboardSimulator, ScreenCapture, TemplateMatching, PixelReader, ElementInspector. **NUNCA tente instanciar** uma classe static (`new MouseSimulator()` = erro de compilacao).
10. **Eventos do Platform usam `EventHandler<TArgs>`**, nao delegates simples. Os handlers devem ter assinatura `(object? sender, TArgs e)`.
11. **`FlowExecutionContext`** e o nome correto da classe (NAO `ExecutionContext`, que conflita com `System.Threading.ExecutionContext`).
12. **`NodeInfoAttribute.TypeId`** tem o modificador `new` porque oculta `Attribute.TypeId`. Mantenha o `new`.
13. **JSON usa camelCase** em todo o projeto. O `FlowSerializer` e o `BridgeRouter` usam `JsonNamingPolicy.CamelCase`.
14. **Serializacao e `System.Text.Json`** — nao introduza Newtonsoft.Json.
15. **Target frameworks:**
    - Core: `net8.0` (puro, sem dependencias Windows)
    - Platform: `net8.0-windows` (com UseWPF, UseWindowsForms, AllowUnsafeBlocks)
    - Nodes: `net8.0-windows` (com UseWindowsForms)
    - App: `net8.0-windows` (com UseWPF, WinExe)

### 4.3 Regras do Frontend React

16. **Use `@xyflow/react`** (React Flow v12), NAO `reactflow` (pacote antigo v11).
17. **Imports corretos:**
    ```tsx
    import { ReactFlow, MiniMap, Controls, Background, Handle, Position } from '@xyflow/react';
    import '@xyflow/react/dist/style.css';
    ```
18. **`FlowNodeData` DEVE extender `Record<string, unknown>`** — exigencia do React Flow v12 para generics.
19. **Estado global e Zustand**, nao Context API, nao Redux.
20. **O build do Vite vai para `../Ajudante.App/wwwroot/`** com `base: './'`.
21. **O WebView2 serve via virtual host `https://app.local/`** mapeado para a pasta wwwroot.

### 4.4 Regras para Criacao de Novos Nodes

22. **Todo node DEVE:**
    - Ter o atributo `[NodeInfo(TypeId = "...", DisplayName = "...", Category = ..., Color = "...", Description = "...")]`
    - Implementar exatamente UMA interface: `ITriggerNode`, `ILogicNode`, ou `IActionNode`
    - Ter a propriedade `Id` (get; set) com valor default `""`
    - Ter a propriedade `Definition` retornando `NodeDefinition` com todos os ports e properties definidos
    - Ter `Configure(Dictionary<string, object?> properties)` que armazena valores
    - Ter `ExecuteAsync(FlowExecutionContext context, CancellationToken ct)` funcional
    - Para triggers: ter `StartWatchingAsync()` e `StopWatchingAsync()` tambem

23. **Convencao de TypeId:** `categoria.nomeCamelCase`
    - Triggers: `trigger.hotkey`, `trigger.imageDetected`, etc.
    - Logic: `logic.ifElse`, `logic.delay`, etc.
    - Actions: `action.mouseClick`, `action.keyboardType`, etc.

24. **Convencao de cores:**
    - Trigger: `#EF4444` (vermelho)
    - Logic: `#EAB308` (amarelo)
    - Action: `#22C55E` (verde)

25. **Convencao de ports:**
    - Input flow port: `{ Id = "in", Name = "In", DataType = PortDataType.Flow }`
    - Output flow port: `{ Id = "out", Name = "Out", DataType = PortDataType.Flow }`
    - Triggers NAO tem input port (sao o inicio do fluxo)
    - Triggers tem output port `{ Id = "triggered", Name = "Triggered", DataType = PortDataType.Flow }`
    - Nodes de branching (IfElse, CompareText) tem 2+ output ports com ids semanticos (`"true"`, `"false"`, `"match"`, `"noMatch"`)

26. **Ao usar servicos do Platform em Nodes:** chame metodos estáticos diretamente.
    ```csharp
    // CORRETO:
    MouseSimulator.MoveTo(x, y);
    KeyboardSimulator.TypeText(text);
    PixelReader.GetPixelColor(x, y);
    TemplateMatching.FindOnScreen(template, threshold);
    ScreenCapture.CaptureRegion(x, y, w, h);
    ElementInspector.GetElementUnderCursor();
    
    // ERRADO (nao compila — sao classes static):
    var mouse = new MouseSimulator(); // ERRO!
    _keyboard.TypeText(text);         // ERRO!
    ```

27. **Suporte a templates:** Propriedades de texto devem suportar `{{variableName}}` via `context.ResolveTemplate()`.

### 4.5 Regras da Comunicacao Bridge (C# ↔ JS)

28. **Protocolo de mensagens:** Toda mensagem segue o formato:
    ```json
    {
      "type": "command" | "event" | "response",
      "channel": "flow" | "engine" | "platform" | "inspector" | "registry",
      "action": "nomeDoAction",
      "requestId": "uuid-opcional",
      "payload": { ... }
    }
    ```
29. **JS → C#:** `window.chrome.webview.postMessage(JSON.stringify(msg))`
30. **C# → JS:** `webView.CoreWebView2.PostWebMessageAsJson(json)` — SEMPRE via Dispatcher (thread UI).
31. **Canais existentes e seus actions:**

| Canal     | Actions                                             | Direcao      |
|-----------|-----------------------------------------------------|--------------|
| flow      | saveFlow, loadFlow, listFlows, newFlow, deleteFlow  | JS → C#      |
| engine    | runFlow, stopFlow, activateFlow, deactivateFlow, getStatus, getRuntimeStatus | JS → C# |
| engine    | nodeStatusChanged, logMessage, flowCompleted, flowError, runtimeStatusChanged, flowArmed, flowDisarmed, triggerFired, flowQueued, flowQueueCoalesced, runtimeError | C# → JS |
| platform  | startMira, startSnip, cancelInspector               | JS → C#      |
| inspector | elementCaptured, regionCaptured                     | C# → JS      |
| registry  | getNodeDefinitions                                  | JS → C#      |
| registry  | nodeDefinitions (auto-push on NavigationCompleted)  | C# → JS      |

32. **Eventos de runtime sao encaminhados automaticamente:** O `BridgeRouter` trabalha com `FlowRuntimeManager`, que encapsula execucao manual, triggers continuos, fila/coalescencia e eventos de runtime enviados pelo canal `engine` ao frontend.

33. **Conversao de formato frontend↔backend:** O arquivo `bridge/flowConverter.ts` converte entre o formato React Flow (Node<FlowNodeData> + Edge) e o formato C# (BackendFlow com NodeInstance + Connection). Usar `toBackendFlow()` ao enviar para o backend e `fromBackendFlow()` ao receber.

### 4.6 Regras de Arquivos e Builds

34. **Para compilar o backend:**
    ```bash
    export PATH="/c/Program Files/dotnet:$PATH"
    cd F:/Projects/Ajudante
    dotnet build Ajudante.sln
    ```

35. **Para compilar o frontend:**
    ```bash
    cd F:/Projects/Ajudante/src/Ajudante.UI
    npm run build
    ```

36. **Para executar o app (modo debug):**
    ```bash
    export PATH="/c/Program Files/dotnet:$PATH"
    cd F:/Projects/Ajudante
    dotnet run --project src/Ajudante.App/Ajudante.App.csproj
    ```

37. **Dados do usuario ficam em:** `%AppData%/Sidekick/` como raiz atual. A migracao legada de `%AppData%/Ajudante/` e tratada por `AppPaths.Initialize()`. Subpastas relevantes: `flows/`, `logs/`, `plugins/`, `WebView2Data/`.

---

## 5. MAPA COMPLETO DE ARQUIVOS E APIs

### 5.1 Ajudante.Core — Interfaces

| Arquivo | Tipo | Membros Publicos |
|---------|------|-----------------|
| `Interfaces/INode.cs` | `interface INode` | `Id {get;set}`, `Definition {get}` → NodeDefinition, `ExecuteAsync(FlowExecutionContext, CancellationToken)` → Task\<NodeResult\>, `Configure(Dictionary<string,object?>)` |
| `Interfaces/ITriggerNode.cs` | `interface ITriggerNode : INode` | `event Action<TriggerEventArgs>? Triggered`, `StartWatchingAsync(CancellationToken)`, `StopWatchingAsync()` |
| `Interfaces/ITriggerNode.cs` | `class TriggerEventArgs : EventArgs` | `Data` → Dictionary\<string,object?\>, `Timestamp` → DateTime |
| `Interfaces/ILogicNode.cs` | `interface ILogicNode : INode` | (sem membros adicionais) |
| `Interfaces/IActionNode.cs` | `interface IActionNode : INode` | (sem membros adicionais) |
| `Interfaces/INodeRegistry.cs` | `interface INodeRegistry` | `ScanAssembly(Assembly)`, `ScanDirectory(string)`, `GetAllDefinitions()` → NodeDefinition[], `CreateInstance(string typeId)` → INode, `GetDefinition(string)` → NodeDefinition? |

### 5.2 Ajudante.Core — Models

| Arquivo | Tipo | Membros Chave |
|---------|------|--------------|
| `Models/Flow.cs` | `class Flow` | Id, Name, Version, Variables (List\<FlowVariable\>), Nodes (List\<NodeInstance\>), Connections (List\<Connection\>), CreatedAt, ModifiedAt |
| `Models/Flow.cs` | `class FlowVariable` | Name (required), Type (VariableType), Default (object?) |
| `Models/Flow.cs` | `enum VariableType` | String, Integer, Float, Boolean |
| `Models/NodeDefinition.cs` | `class NodeDefinition` | TypeId, DisplayName, Category, Description, Color, InputPorts, OutputPorts, Properties |
| `Models/NodeDefinition.cs` | `enum NodeCategory` | Trigger, Logic, Action |
| `Models/NodeDefinition.cs` | `class PortDefinition` | Id, Name, DataType |
| `Models/NodeDefinition.cs` | `enum PortDataType` | Flow, String, Number, Boolean, Point, Image, Any |
| `Models/NodeDefinition.cs` | `class PropertyDefinition` | Id, Name, Type, DefaultValue, Description, Options |
| `Models/NodeDefinition.cs` | `enum PropertyType` | String, Integer, Float, Boolean, FilePath, FolderPath, Hotkey, Point, Color, Dropdown, ImageTemplate |
| `Models/NodeResult.cs` | `class NodeResult` | Success, OutputPort, Outputs, Error. Factories: `Ok(port?)`, `Ok(port, outputs)`, `Fail(error)` |
| `Models/NodeStatus.cs` | `enum NodeStatus` | Idle, Running, Completed, Error, Skipped |
| `Models/NodeInstance.cs` | `class NodeInstance` | Id, TypeId (required), Position, Properties |
| `Models/NodeInstance.cs` | `class NodePosition` | X (double), Y (double) |
| `Models/Connection.cs` | `class Connection` | Id, SourceNodeId, SourcePort, TargetNodeId, TargetPort (todos required exceto Id) |
| `Models/Port.cs` | `class Port` | Id, Name, Direction, DataType, Value |
| `Models/Port.cs` | `enum PortDirection` | Input, Output |
| `Models/Variable.cs` | `class Variable` | Name (required), Type, Value |
| `Models/NodeInfoAttribute.cs` | `class NodeInfoAttribute : Attribute` | TypeId (new, required), DisplayName (required), Category (required), Description, Color |

### 5.3 Ajudante.Core — Engine

| Arquivo | Tipo | Membros Chave |
|---------|------|--------------|
| `Engine/FlowExecutionContext.cs` | `class FlowExecutionContext` | **Construtor:** (Flow, CancellationToken). **Props:** Flow, CancellationToken. **Metodos:** SetVariable, GetVariable, GetVariable\<T\>, SetNodeOutputs, GetNodeOutput, ResolveTemplate |
| `Engine/FlowExecutor.cs` | `class FlowExecutor` | **Construtor:** (INodeRegistry). **Props:** IsRunning. **Eventos:** NodeStatusChanged, LogMessage, FlowCompleted, FlowError. **Metodos:** ExecuteAsync, ExecuteFromTriggerAsync, Cancel |
| `Engine/TriggerManager.cs` | `class TriggerManager : IDisposable` | **Construtor:** (INodeRegistry, FlowExecutor). **Eventos:** LogMessage. **Metodos:** ActivateFlowTriggersAsync, DeactivateAllAsync, DeactivateFlowAsync, Dispose |
| `Engine/FlowValidator.cs` | `class FlowValidator` | **Construtor:** (INodeRegistry). **Metodos:** Validate(Flow) → ValidationResult |
| `Engine/FlowValidator.cs` | `class ValidationResult` | IsValid, Errors (List\<string\>), Warnings (List\<string\>) |

### 5.4 Ajudante.Core — Registry e Serialization

| Arquivo | Tipo | Membros Chave |
|---------|------|--------------|
| `Registry/NodeRegistry.cs` | `class NodeRegistry : INodeRegistry` | ScanAssembly, ScanDirectory, GetAllDefinitions, CreateInstance, GetDefinition |
| `Serialization/FlowSerializer.cs` | `static class FlowSerializer` | Serialize(Flow) → string, Deserialize(string) → Flow?, SaveAsync(Flow, path), LoadAsync(path), LoadAllAsync(dir) |

### 5.5 Ajudante.Platform — Servicos (TODOS static exceto onde indicado)

| Arquivo | Tipo | Membros Chave |
|---------|------|--------------|
| `Input/MouseSimulator.cs` | `static class MouseSimulator` | MoveTo(x,y), Click(MouseButton), DoubleClick(), RightClick(), MiddleClick(), DragTo(fx,fy,tx,ty), ScrollUp(clicks), ScrollDown(clicks) |
| `Input/MouseSimulator.cs` | `enum MouseButton` | Left, Right, Middle |
| `Input/KeyboardSimulator.cs` | `static class KeyboardSimulator` | TypeText(string), PressKey(VirtualKey), KeyDown(VirtualKey), KeyUp(VirtualKey), PressCombo(params VirtualKey[]) |
| `Input/KeyboardSimulator.cs` | `enum VirtualKey` | VK_BACK, VK_TAB, VK_RETURN, VK_SHIFT, VK_CONTROL, VK_MENU, VK_ESCAPE, VK_SPACE, VK_A..VK_Z, VK_0..VK_9, VK_F1..VK_F12, VK_LWIN, etc. |
| `Input/GlobalHotkeyManager.cs` | `sealed class GlobalHotkeyManager` **(NAO static)** | RegisterHotkey(modifiers, key, callback) → int, UnregisterHotkey(id), UnregisterAll(), Dispose() |
| `Input/GlobalHotkeyManager.cs` | `enum HotkeyModifiers [Flags]` | None, Alt, Ctrl, Shift, Win, NoRepeat |
| `Screen/ScreenCapture.cs` | `static class ScreenCapture` | CaptureScreen() → Bitmap, CaptureRegion(x,y,w,h) → Bitmap, CaptureWindow(hwnd) → Bitmap |
| `Screen/TemplateMatching.cs` | `static class TemplateMatching` | FindOnScreen(byte[] templatePng, double threshold) → MatchResult?, FindInImage(Bitmap, byte[], double) → MatchResult? |
| `Screen/TemplateMatching.cs` | `sealed class MatchResult` | X, Y, Width, Height, Confidence, Center (computed) |
| `Screen/PixelReader.cs` | `static class PixelReader` | GetPixelColor(x,y) → Color, WaitForColorChange(x,y,ct) → Task\<Color\> |
| `UIAutomation/ElementInspector.cs` | `static class ElementInspector` | GetElementAtPoint(x,y) → ElementInfo?, GetElementUnderCursor() → ElementInfo?, GetWindowElements(hwnd) → List\<ElementInfo\> |
| `UIAutomation/ElementInfo.cs` | `sealed class ElementInfo` | AutomationId, Name, ClassName, ControlType (string), BoundingRect (Rectangle), ProcessId, WindowTitle |
| `Windows/WindowWatcher.cs` | `sealed class WindowWatcher` **(NAO static)** | Start(), Stop(), Dispose(). Eventos: WindowOpened, WindowClosed, WindowFocused (todos EventHandler\<WindowEventArgs\>) |
| `Windows/WindowWatcher.cs` | `sealed class WindowEventArgs` | Hwnd, ProcessName, WindowTitle, ProcessId |
| `Windows/ProcessWatcher.cs` | `sealed class ProcessWatcher` **(NAO static)** | Start(pollInterval), Stop(), Dispose(). Eventos: ProcessStarted, ProcessStopped (EventHandler\<ProcessEventArgs\>) |
| `Windows/ProcessWatcher.cs` | `sealed class ProcessEventArgs` | ProcessName, ProcessId |
| `FileSystem/FileWatcher.cs` | `sealed class FileWatcher` **(NAO static)** | Watch(path, filter, subdirs), StopWatching(), Dispose(). IsWatching. Eventos: FileCreated, FileChanged, FileDeleted, FileRenamed (EventHandler\<FileWatchEventArgs\>) |
| `FileSystem/FileWatcher.cs` | `sealed class FileWatchEventArgs` | FullPath, FileName, ChangeType, OldFullPath?, OldFileName? |

### 5.6 Ajudante.Nodes — 20 Nodes Embutidos

#### Triggers (5) — Cor: #EF4444

| TypeId | Classe | Interface | Propriedades | Output Ports |
|--------|--------|-----------|-------------|-------------|
| `trigger.hotkey` | HotkeyTriggerNode | ITriggerNode | key (Hotkey), modifiers (Dropdown: None/Ctrl/Shift/Alt/Win) | triggered |
| `trigger.imageDetected` | ImageDetectedTriggerNode | ITriggerNode | templateImage (ImageTemplate), threshold (Float, 0.8), interval (Integer, 1000) | triggered, x, y |
| `trigger.pixelChange` | PixelChangeTriggerNode | ITriggerNode | x (Integer), y (Integer), interval (Integer, 500) | triggered, oldColor, newColor, x, y |
| `trigger.windowEvent` | WindowEventTriggerNode | ITriggerNode | eventType (Dropdown: Opened/Closed/Focused), windowTitle (String) | triggered, windowTitle |
| `trigger.filesystem` | FileSystemTriggerNode | ITriggerNode | path (FolderPath), filter (String, "*.*"), eventType (Dropdown: Created/Changed/Deleted) | triggered, filePath, fileName |

#### Logic (6) — Cor: #EAB308

| TypeId | Classe | Interface | Propriedades | Output Ports |
|--------|--------|-----------|-------------|-------------|
| `logic.ifElse` | IfElseNode | ILogicNode | conditionType (Dropdown: VariableEquals/VariableContains/VariableGreaterThan/PixelColorIs), variableName (String), compareValue (String) | true, false |
| `logic.delay` | DelayNode | ILogicNode | milliseconds (Integer, 1000) | out |
| `logic.compareText` | CompareTextNode | ILogicNode | text1 (String), text2 (String), comparison (Dropdown: Equals/Contains/StartsWith/EndsWith) | match, noMatch |
| `logic.loop` | LoopNode | ILogicNode | count (Integer, 5), delayBetween (Integer, 0) | body, done |
| `logic.setVariable` | SetVariableNode | ILogicNode | variableName (String), value (String) | out |
| `logic.getVariable` | GetVariableNode | ILogicNode | variableName (String) | out, value |

#### Actions (9) — Cor: #22C55E

| TypeId | Classe | Interface | Propriedades | Output Ports |
|--------|--------|-----------|-------------|-------------|
| `action.mouseClick` | MouseClickNode | IActionNode | x (Integer), y (Integer), button (Dropdown: Left/Right/Middle), clickType (Dropdown: Single/Double) | out |
| `action.mouseMove` | MouseMoveNode | IActionNode | x (Integer), y (Integer) | out |
| `action.mouseDrag` | MouseDragNode | IActionNode | fromX, fromY, toX, toY (Integer) | out |
| `action.keyboardType` | KeyboardTypeNode | IActionNode | text (String), delayBetweenKeys (Integer, 0) | out |
| `action.keyboardPress` | KeyboardPressNode | IActionNode | key (Dropdown: Return/Tab/Escape/A-Z/F1-F12/...), modifiers (Dropdown: None/Ctrl/Shift/Alt) | out |
| `action.openProgram` | OpenProgramNode | IActionNode | path (FilePath), arguments (String) | out |
| `action.killProcess` | KillProcessNode | IActionNode | processName (String) | out |
| `action.playSound` | PlaySoundNode | IActionNode | soundFile (FilePath) | out |
| `action.deleteFile` | DeleteFileNode | IActionNode | filePath (FilePath) | out |

### 5.7 Ajudante.App — Bridge e Host

| Arquivo | Tipo | Funcao |
|---------|------|--------|
| `Bridge/BridgeMessage.cs` | `class BridgeMessage` | DTO do protocolo. Props: Type, Channel, Action, RequestId, Payload (JsonElement?), Error. Classes internas: Types, Channels (constantes) |
| `Bridge/WebBridge.cs` | `class WebBridge : IDisposable` | Inicializa WebView2, mapeia `app.local` → wwwroot, recebe/envia mensagens. Metodos: InitializeAsync, SetRouter, SendEventAsync, SendResponseAsync, SendErrorResponseAsync |
| `Bridge/BridgeRouter.cs` | `class BridgeRouter` | Despacha mensagens por canal. Handlers: save/load/list/new/deleteFlow, run/stop/activate/deactivate/getStatus/getRuntimeStatus, startMira/startSnip, getNodeDefinitions. Encaminha eventos do `FlowRuntimeManager` para o frontend via bridge. Gerencia overlays Mira/Snip. Evento: LogMessage |
| `TrayIcon/SystemTrayManager.cs` | `class SystemTrayManager : IDisposable` | Icone no system tray. Props: IsFlowRunning. Eventos: ShowWindowRequested, QuitRequested, StartFlowRequested, StopFlowRequested. Metodos: Initialize, ShowBalloon |
| `Overlays/MiraWindow.xaml(.cs)` | `class MiraWindow : Window` | Overlay transparente fullscreen para inspecao de elementos. Evento: `Action<ElementInfo>? ElementCaptured`. Usa ElementInspector.GetElementUnderCursor() a cada 50ms |
| `Overlays/SnipWindow.xaml(.cs)` | `class SnipWindow : Window` | Overlay transparente para captura de regiao. Evento: `Action<byte[], Rectangle>? RegionCaptured`. Usa ScreenCapture.CaptureRegion() |
| `MainWindow.xaml(.cs)` | `class MainWindow : Window` | Host principal. Inicializa NodeRegistry, `FlowRuntimeManager`, WebBridge, BridgeRouter, SystemTrayManager. Encaminha eventos de runtime para o frontend |
| `App.xaml(.cs)` | `class App : Application` | Mutex de instancia unica, inicializa `AppPaths`, migra dados legados quando necessario, registra exception handling global e semeia flows iniciais |

### 5.8 Ajudante.UI — Frontend React

| Arquivo | Exportacoes | Funcao |
|---------|-------------|--------|
| `bridge/types.ts` | BridgeMessage, NodeDefinition, PortDefinition, PropertyDefinition, NodeCategory, PortDataType, PropertyType, FlowNodeData, FlowNode, FlowConnection, FlowData, FlowVariable, NodeStatus, LogEntry, InspectorMode | Tipos TypeScript espelhando os DTOs do C# |
| `bridge/bridge.ts` | `sendCommand(channel, action, payload)`, `onEvent(channel, action, callback)`, `offEvent(channel, action, callback)`, `initBridge()` | Wrapper do WebView2 postMessage. Fallback para console.log em modo dev |
| `bridge/flowConverter.ts` | `toBackendFlow(flowId, flowName, nodes, edges)` → BackendFlow, `fromBackendFlow(backend, definitions)` → {nodes, edges} | Conversao bidirecional entre formato React Flow (Node+Edge) e formato C# (BackendFlow com NodeInstance+Connection) |
| `store/flowStore.ts` | `useFlowStore` (Zustand) | nodes, edges, onNodesChange, onEdgesChange, onConnect, selectedNodeId, addNode, removeNode, updateNodeProperty, saveFlow, loadFlow, newFlow, nodeDefinitions, setNodeDefinitions |
| `store/appStore.ts` | `useAppStore` (Zustand) | isRunning, nodeStatuses, logs, inspectorMode, isPaletteOpen, isLogsExpanded + setters/toggles |
| `components/Canvas/FlowCanvas.tsx` | `FlowCanvas` | Canvas React Flow com MiniMap, Controls, Background. Custom node types: triggerNode, logicNode, actionNode. Drag-and-drop do palette |
| `components/Nodes/BaseNode.tsx` | `BaseNode` | Componente base: header colorido, handles de input/output, indicador de status |
| `components/Nodes/TriggerNode.tsx` | `TriggerNode` | Wrapper do BaseNode para triggers |
| `components/Nodes/LogicNode.tsx` | `LogicNode` | Wrapper do BaseNode para logic |
| `components/Nodes/ActionNode.tsx` | `ActionNode` | Wrapper do BaseNode para actions |
| `components/Sidebar/NodePalette.tsx` | `NodePalette` | Lista de nodes agrupados por categoria, arrastavel, com filtro de busca |
| `components/Sidebar/PropertyPanel.tsx` | `PropertyPanel` | Formulario dinamico para propriedades do node selecionado |
| `components/Toolbar/Toolbar.tsx` | `Toolbar` | New/Save/Load, Play/Stop, Mira/Snip, editor de nome do flow |
| `components/StatusBar/ExecutionStatus.tsx` | `ExecutionStatus` | Status bar com nome do flow, indicador running, play/stop, logs |
| `App.tsx` | `App` | Layout principal (toolbar + palette + canvas + property panel + status bar). Inicializa bridge e node definitions em dev mode |
| `styles/global.css` | — | Tema escuro completo (#0d1117 background). Estilos para todos os componentes |

---

## 6. FLUXO DE EXECUCAO DE UM FLOW

```
1. Usuario monta flow no canvas (arrasta nodes, conecta com fios)
2. Usuario clica "Play"
3. Frontend envia: { channel: "engine", action: "runFlow", payload: { flow } }
4. BridgeRouter recebe -> enfileira a execucao no `FlowRuntimeManager`
5. O runtime encontra o node de entrada e executa usando o `FlowExecutor`
6. Para cada node na cadeia:
   a. Emite NodeStatusChanged(nodeId, Running) → frontend destaca o node
   b. Chama node.ExecuteAsync(context, ct)
   c. Node retorna NodeResult com OutputPort e Outputs
   d. Executor segue as conexoes do OutputPort especificado
   e. Emite NodeStatusChanged(nodeId, Completed|Error)
7. Ao terminar: FlowCompleted ou FlowError
8. Frontend recebe eventos e atualiza visual
```

Para triggers:
```
1. Frontend envia `engine/activateFlow`
2. `FlowRuntimeManager.ActivateFlowAsync(flow)` arma os gatilhos do flow
3. Cada `ITriggerNode` chama `StartWatchingAsync()`
4. Quando um trigger dispara, o runtime registra o evento e enfileira a execucao
5. O `FlowExecutor` roda a partir do trigger correspondente
```

---

## 7. FORMATO JSON DE UM FLOW

```json
{
  "id": "uuid",
  "name": "Meu Flow",
  "version": 1,
  "variables": [
    { "name": "downloadPath", "type": "string", "default": "C:\\Downloads" }
  ],
  "nodes": [
    {
      "id": "node-1",
      "typeId": "trigger.filesystem",
      "position": { "x": 100, "y": 200 },
      "properties": {
        "path": "{{downloadPath}}",
        "filter": "*.pdf",
        "event": "created"
      }
    }
  ],
  "connections": [
    {
      "id": "conn-1",
      "sourceNodeId": "node-1",
      "sourcePort": "triggered",
      "targetNodeId": "node-2",
      "targetPort": "in"
    }
  ],
  "createdAt": "2026-04-17T00:00:00Z",
  "modifiedAt": "2026-04-17T00:00:00Z"
}
```

---

## 8. COMO ADICIONAR UM NOVO NODE (checklist)

1. Decida: e Trigger, Logic, ou Action?
2. Crie o arquivo .cs na pasta correta dentro de `src/Ajudante.Nodes/{Triggers|Logic|Actions}/`
3. Adicione `[NodeInfo(...)]` com TypeId unico seguindo a convencao `categoria.nomeCamelCase`
4. Implemente a interface correta (`ITriggerNode`, `ILogicNode`, `IActionNode`)
5. Defina `Definition` com todos os ports e properties
6. Implemente `Configure()` e `ExecuteAsync()`
7. Para triggers: implemente `StartWatchingAsync()` e `StopWatchingAsync()`
8. Compile: `dotnet build Ajudante.sln` — deve dar 0 erros
9. O node sera descoberto automaticamente pelo `NodeRegistry.ScanAssembly()`
10. No frontend, o node aparecera automaticamente no `NodePalette` apos `getNodeDefinitions`

---

## 9. COMO ADICIONAR UM NOVO SERVICO DE PLATAFORMA

1. Crie a classe em `src/Ajudante.Platform/{subpasta}/`
2. Se for utilitario: faca `static class`. Se manter estado: faca `sealed class` com `IDisposable`
3. Use P/Invoke para APIs Win32 quando necessario
4. Adicione `AllowUnsafeBlocks` no csproj se usar ponteiros (ja esta habilitado)
5. Para eventos: use `EventHandler<TArgs>` com uma classe `sealed` derivada de `EventArgs`
6. Compile e verifique

---

## 10. PACOTES NUGET INSTALADOS

| Projeto | Pacote | Versao | Uso |
|---------|--------|--------|-----|
| Platform | Emgu.CV | 4.12.0.5764 | Template matching (OpenCV) |
| Platform | Emgu.CV.runtime.windows | 4.12.0.5764 | Binarios nativos OpenCV |
| Platform | System.Management | 10.0.6 | WMI para monitorar processos |
| App | Microsoft.Web.WebView2 | 1.0.3912.50 | Hospeda frontend React |
| App | Hardcodet.NotifyIcon.Wpf | 2.0.1 | Icone no system tray |

---

## 11. PACOTES NPM INSTALADOS

| Pacote | Uso |
|--------|-----|
| `@xyflow/react` | Editor de nodes visual (React Flow v12) |
| `zustand` | Gerenciamento de estado global |
| `vite` | Build tool |
| `typescript` | Type safety |
| `react` / `react-dom` | Framework UI |

---

## 12. ERROS COMUNS E COMO EVITAR

| Erro | Causa | Solucao |
|------|-------|---------|
| `CS0723: Cannot declare variable of static type` | Tentou instanciar MouseSimulator, KeyboardSimulator, etc. | Use chamadas estaticas: `MouseSimulator.MoveTo(x,y)` |
| `CS0123: No overload matches delegate` | Handler de evento com assinatura errada | Use `(object? sender, TipoEventArgs e)`, nao `(object? sender, string)` |
| `CS1503: Cannot convert FlowExecutionContext to ExecutionContext` | Conflito de nome com System.Threading | A classe se chama `FlowExecutionContext`, nao `ExecutionContext` |
| `CS0114: TypeId hides inherited member` | NodeInfoAttribute.TypeId oculta Attribute.TypeId | Mantenha o `new` no TypeId |
| Frontend nao carrega no WebView2 | Build do Vite nao rodou | Execute `npm run build` em `src/Ajudante.UI/` |
| `reactflow` import error | Pacote errado | Use `@xyflow/react`, nao `reactflow` |

---

## 13. STATUS ATUAL DO PROJETO

**Concluido:**
- [x] Estrutura de solucao .NET com 4 projetos
- [x] Interfaces e modelos do Core
- [x] Engine: FlowExecutor, TriggerManager, FlowValidator, FlowSerializer
- [x] NodeRegistry com descoberta automatica via reflection
- [x] 11 servicos de plataforma Windows (input, screen, UIAutomation, watchers)
- [x] 20 nodes embutidos (5 triggers, 6 logic, 9 actions)
- [x] Frontend React com React Flow, palette, property panel, toolbar, status bar
- [x] WebView2 bridge (C#↔JS bidirecional)
- [x] Overlays Mira (inspecao de elementos) e Snip (captura de tela)
- [x] System tray com menu de contexto
- [x] App lifecycle (single instance, exception handling, crash logs)
- [x] Compilacao limpa: 0 erros, 0 warnings
- [x] Integracao bridge completa: flowConverter.ts, nomes de actions/events normalizados, executor events encaminhados para frontend, overlays Mira/Snip lancados pelo BridgeRouter, auto-push de node definitions no NavigationCompleted

**Pendente:**
- [ ] Testes unitarios
- [ ] Plugin system (carregar DLLs externas de nodes)
- [ ] Dev server com hot reload para desenvolvimento do frontend
- [ ] Icone customizado do app
- [ ] Instalador / empacotamento para distribuicao
- [ ] Documentacao do usuario final
