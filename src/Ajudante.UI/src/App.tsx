import { useEffect } from 'react';
import { ReactFlowProvider } from '@xyflow/react';
import Toolbar from './components/Toolbar/Toolbar';
import NodePalette from './components/Sidebar/NodePalette';
import FlowCanvas from './components/Canvas/FlowCanvas';
import PropertyPanel from './components/Sidebar/PropertyPanel';
import ExecutionStatus from './components/StatusBar/ExecutionStatus';
import { useFlowStore } from './store/flowStore';
import { useAppStore } from './store/appStore';
import { initBridge, onEvent, sendCommand } from './bridge/bridge';
import type { NodeDefinition, NodeStatus } from './bridge/types';

export default function App() {
  const setNodeDefinitions = useFlowStore((s) => s.setNodeDefinitions);
  const selectedNodeId = useFlowStore((s) => s.selectedNodeId);
  const setNodeStatus = useAppStore((s) => s.setNodeStatus);
  const setRunning = useAppStore((s) => s.setRunning);
  const addLog = useAppStore((s) => s.addLog);

  useEffect(() => {
    // Initialise WebView2 bridge
    initBridge();

    // Request node definitions from backend (response-based)
    sendCommand('registry', 'getNodeDefinitions', {}).then((defs: NodeDefinition[]) => {
      if (Array.isArray(defs) && defs.length > 0) {
        setNodeDefinitions(defs);
      }
    }).catch((err) => {
      console.warn('[App] Failed to fetch node definitions:', err);
    });

    // Also listen for pushed node definitions (backend pushes on startup)
    const offRegistry = onEvent('registry', 'nodeDefinitions', (payload: NodeDefinition[]) => {
      setNodeDefinitions(payload);
    });

    // Listen for execution events
    const offNodeStatus = onEvent('engine', 'nodeStatusChanged', (payload: { nodeId: string; status: NodeStatus }) => {
      setNodeStatus(payload.nodeId, payload.status);
    });

    const offFlowCompleted = onEvent('engine', 'flowCompleted', (payload: { flowId: string }) => {
      setRunning(false);
      addLog({
        timestamp: new Date().toISOString(),
        level: 'info',
        message: `Flow ${payload.flowId} completed successfully.`,
      });
    });

    const offFlowError = onEvent('engine', 'flowError', (payload: { flowId: string; error: string }) => {
      setRunning(false);
      addLog({
        timestamp: new Date().toISOString(),
        level: 'error',
        message: `Flow ${payload.flowId} failed: ${payload.error}`,
      });
    });

    const offLog = onEvent('engine', 'logMessage', (payload: { nodeId: string; message: string }) => {
      addLog({
        timestamp: new Date().toISOString(),
        level: 'info',
        message: payload.message,
        nodeId: payload.nodeId,
      });
    });

    // In dev mode (not in WebView2), load demo node definitions
    if (!('chrome' in window && (window as any).chrome?.webview)) {
      setNodeDefinitions(getDevNodeDefinitions());
    }

    return () => {
      offRegistry();
      offNodeStatus();
      offFlowCompleted();
      offFlowError();
      offLog();
    };
  }, [setNodeDefinitions, setNodeStatus, setRunning, addLog]);

  return (
    <ReactFlowProvider>
      <div className="app">
        <Toolbar />
        <div className="app__workspace">
          <NodePalette />
          <div className="app__canvas-area">
            <FlowCanvas />
          </div>
          {selectedNodeId && (
            <PropertyPanel />
          )}
        </div>
        <ExecutionStatus />
      </div>
    </ReactFlowProvider>
  );
}

/** Dev-mode sample definitions so the palette is not empty during development.
 *  TypeIds match the real backend node registry. */
function getDevNodeDefinitions(): NodeDefinition[] {
  return [
    // ── Triggers ──────────────────────────────────────────────────
    {
      typeId: 'trigger.hotkey',
      displayName: 'Hotkey Trigger',
      category: 'Trigger',
      description: 'Starts the flow when a hotkey is pressed',
      color: '#EF4444',
      inputPorts: [],
      outputPorts: [
        { id: 'flow-out', name: 'Flow', dataType: 'Flow' },
      ],
      properties: [
        { id: 'hotkey', name: 'Hotkey', type: 'Hotkey', defaultValue: 'Ctrl+F9' },
      ],
    },
    {
      typeId: 'trigger.imageDetected',
      displayName: 'Image Detected',
      category: 'Trigger',
      description: 'Triggers when an image is detected on screen',
      color: '#EF4444',
      inputPorts: [],
      outputPorts: [
        { id: 'flow-out', name: 'Flow', dataType: 'Flow' },
        { id: 'location', name: 'Location', dataType: 'Point' },
      ],
      properties: [
        { id: 'template', name: 'Template Image', type: 'ImageTemplate' },
        { id: 'confidence', name: 'Confidence', type: 'Float', defaultValue: 0.8 },
      ],
    },
    {
      typeId: 'trigger.pixelChange',
      displayName: 'Pixel Change',
      category: 'Trigger',
      description: 'Triggers when pixels change in a region',
      color: '#EF4444',
      inputPorts: [],
      outputPorts: [
        { id: 'flow-out', name: 'Flow', dataType: 'Flow' },
      ],
      properties: [
        { id: 'region', name: 'Region', type: 'Point', defaultValue: '' },
        { id: 'threshold', name: 'Threshold', type: 'Float', defaultValue: 0.1 },
      ],
    },
    {
      typeId: 'trigger.windowEvent',
      displayName: 'Window Event',
      category: 'Trigger',
      description: 'Triggers on a window event (open, close, focus)',
      color: '#EF4444',
      inputPorts: [],
      outputPorts: [
        { id: 'flow-out', name: 'Flow', dataType: 'Flow' },
      ],
      properties: [
        { id: 'windowTitle', name: 'Window Title', type: 'String', defaultValue: '' },
        { id: 'event', name: 'Event', type: 'Dropdown', defaultValue: 'Opened', options: ['Opened', 'Closed', 'Focused'] },
      ],
    },
    {
      typeId: 'trigger.filesystem',
      displayName: 'Filesystem',
      category: 'Trigger',
      description: 'Triggers on file system changes',
      color: '#EF4444',
      inputPorts: [],
      outputPorts: [
        { id: 'flow-out', name: 'Flow', dataType: 'Flow' },
      ],
      properties: [
        { id: 'path', name: 'Watch Path', type: 'FolderPath' },
        { id: 'filter', name: 'Filter', type: 'String', defaultValue: '*.*' },
      ],
    },
    // ── Logic ─────────────────────────────────────────────────────
    {
      typeId: 'logic.ifElse',
      displayName: 'If / Else',
      category: 'Logic',
      description: 'Branch based on a condition',
      color: '#EAB308',
      inputPorts: [
        { id: 'flow-in', name: 'Flow', dataType: 'Flow' },
        { id: 'condition', name: 'Condition', dataType: 'Boolean' },
      ],
      outputPorts: [
        { id: 'true-out', name: 'True', dataType: 'Flow' },
        { id: 'false-out', name: 'False', dataType: 'Flow' },
      ],
      properties: [],
    },
    {
      typeId: 'logic.delay',
      displayName: 'Delay',
      category: 'Logic',
      description: 'Wait for a specified duration',
      color: '#EAB308',
      inputPorts: [
        { id: 'flow-in', name: 'Flow', dataType: 'Flow' },
      ],
      outputPorts: [
        { id: 'flow-out', name: 'Flow', dataType: 'Flow' },
      ],
      properties: [
        { id: 'delay', name: 'Delay (ms)', type: 'Integer', defaultValue: 1000 },
      ],
    },
    {
      typeId: 'logic.compareText',
      displayName: 'Compare Text',
      category: 'Logic',
      description: 'Compare two text values',
      color: '#EAB308',
      inputPorts: [
        { id: 'flow-in', name: 'Flow', dataType: 'Flow' },
        { id: 'a', name: 'A', dataType: 'String' },
        { id: 'b', name: 'B', dataType: 'String' },
      ],
      outputPorts: [
        { id: 'match', name: 'Match', dataType: 'Flow' },
        { id: 'no-match', name: 'No Match', dataType: 'Flow' },
      ],
      properties: [
        { id: 'caseSensitive', name: 'Case Sensitive', type: 'Boolean', defaultValue: false },
      ],
    },
    {
      typeId: 'logic.loop',
      displayName: 'Loop',
      category: 'Logic',
      description: 'Repeat a section a set number of times',
      color: '#EAB308',
      inputPorts: [
        { id: 'flow-in', name: 'Flow', dataType: 'Flow' },
      ],
      outputPorts: [
        { id: 'body', name: 'Body', dataType: 'Flow' },
        { id: 'done', name: 'Done', dataType: 'Flow' },
        { id: 'index', name: 'Index', dataType: 'Number' },
      ],
      properties: [
        { id: 'count', name: 'Count', type: 'Integer', defaultValue: 10 },
      ],
    },
    {
      typeId: 'logic.setVariable',
      displayName: 'Set Variable',
      category: 'Logic',
      description: 'Set a flow variable value',
      color: '#EAB308',
      inputPorts: [
        { id: 'flow-in', name: 'Flow', dataType: 'Flow' },
        { id: 'value', name: 'Value', dataType: 'Any' },
      ],
      outputPorts: [
        { id: 'flow-out', name: 'Flow', dataType: 'Flow' },
      ],
      properties: [
        { id: 'variableName', name: 'Variable Name', type: 'String', defaultValue: '' },
      ],
    },
    {
      typeId: 'logic.getVariable',
      displayName: 'Get Variable',
      category: 'Logic',
      description: 'Read a flow variable value',
      color: '#EAB308',
      inputPorts: [
        { id: 'flow-in', name: 'Flow', dataType: 'Flow' },
      ],
      outputPorts: [
        { id: 'flow-out', name: 'Flow', dataType: 'Flow' },
        { id: 'value', name: 'Value', dataType: 'Any' },
      ],
      properties: [
        { id: 'variableName', name: 'Variable Name', type: 'String', defaultValue: '' },
      ],
    },
    // ── Actions ───────────────────────────────────────────────────
    {
      typeId: 'action.mouseClick',
      displayName: 'Mouse Click',
      category: 'Action',
      description: 'Click at a screen position',
      color: '#22C55E',
      inputPorts: [
        { id: 'flow-in', name: 'Flow', dataType: 'Flow' },
        { id: 'position', name: 'Position', dataType: 'Point' },
      ],
      outputPorts: [
        { id: 'flow-out', name: 'Flow', dataType: 'Flow' },
      ],
      properties: [
        { id: 'x', name: 'X', type: 'Integer', defaultValue: 0 },
        { id: 'y', name: 'Y', type: 'Integer', defaultValue: 0 },
        {
          id: 'button',
          name: 'Button',
          type: 'Dropdown',
          defaultValue: 'Left',
          options: ['Left', 'Right', 'Middle'],
        },
        { id: 'clicks', name: 'Clicks', type: 'Integer', defaultValue: 1 },
      ],
    },
    {
      typeId: 'action.mouseMove',
      displayName: 'Mouse Move',
      category: 'Action',
      description: 'Move the mouse to a position',
      color: '#22C55E',
      inputPorts: [
        { id: 'flow-in', name: 'Flow', dataType: 'Flow' },
        { id: 'position', name: 'Position', dataType: 'Point' },
      ],
      outputPorts: [
        { id: 'flow-out', name: 'Flow', dataType: 'Flow' },
      ],
      properties: [
        { id: 'x', name: 'X', type: 'Integer', defaultValue: 0 },
        { id: 'y', name: 'Y', type: 'Integer', defaultValue: 0 },
        { id: 'duration', name: 'Duration (ms)', type: 'Integer', defaultValue: 0 },
      ],
    },
    {
      typeId: 'action.mouseDrag',
      displayName: 'Mouse Drag',
      category: 'Action',
      description: 'Drag from one position to another',
      color: '#22C55E',
      inputPorts: [
        { id: 'flow-in', name: 'Flow', dataType: 'Flow' },
      ],
      outputPorts: [
        { id: 'flow-out', name: 'Flow', dataType: 'Flow' },
      ],
      properties: [
        { id: 'fromX', name: 'From X', type: 'Integer', defaultValue: 0 },
        { id: 'fromY', name: 'From Y', type: 'Integer', defaultValue: 0 },
        { id: 'toX', name: 'To X', type: 'Integer', defaultValue: 0 },
        { id: 'toY', name: 'To Y', type: 'Integer', defaultValue: 0 },
        { id: 'duration', name: 'Duration (ms)', type: 'Integer', defaultValue: 200 },
      ],
    },
    {
      typeId: 'action.keyboardType',
      displayName: 'Keyboard Type',
      category: 'Action',
      description: 'Types text via keyboard',
      color: '#22C55E',
      inputPorts: [
        { id: 'flow-in', name: 'Flow', dataType: 'Flow' },
        { id: 'text-in', name: 'Text', dataType: 'String' },
      ],
      outputPorts: [
        { id: 'flow-out', name: 'Flow', dataType: 'Flow' },
      ],
      properties: [
        { id: 'text', name: 'Text', type: 'String', defaultValue: '' },
        { id: 'delayPerChar', name: 'Delay per char (ms)', type: 'Integer', defaultValue: 50 },
      ],
    },
    {
      typeId: 'action.keyboardPress',
      displayName: 'Keyboard Press',
      category: 'Action',
      description: 'Send a keyboard shortcut or key press',
      color: '#22C55E',
      inputPorts: [
        { id: 'flow-in', name: 'Flow', dataType: 'Flow' },
      ],
      outputPorts: [
        { id: 'flow-out', name: 'Flow', dataType: 'Flow' },
      ],
      properties: [
        { id: 'hotkey', name: 'Hotkey', type: 'Hotkey', defaultValue: '' },
      ],
    },
    {
      typeId: 'action.openProgram',
      displayName: 'Open Program',
      category: 'Action',
      description: 'Launch an external program',
      color: '#22C55E',
      inputPorts: [
        { id: 'flow-in', name: 'Flow', dataType: 'Flow' },
      ],
      outputPorts: [
        { id: 'flow-out', name: 'Flow', dataType: 'Flow' },
      ],
      properties: [
        { id: 'path', name: 'Program Path', type: 'FilePath' },
        { id: 'args', name: 'Arguments', type: 'String', defaultValue: '' },
      ],
    },
    {
      typeId: 'action.killProcess',
      displayName: 'Kill Process',
      category: 'Action',
      description: 'Terminate a running process',
      color: '#22C55E',
      inputPorts: [
        { id: 'flow-in', name: 'Flow', dataType: 'Flow' },
      ],
      outputPorts: [
        { id: 'flow-out', name: 'Flow', dataType: 'Flow' },
      ],
      properties: [
        { id: 'processName', name: 'Process Name', type: 'String', defaultValue: '' },
      ],
    },
    {
      typeId: 'action.playSound',
      displayName: 'Play Sound',
      category: 'Action',
      description: 'Play an audio file',
      color: '#22C55E',
      inputPorts: [
        { id: 'flow-in', name: 'Flow', dataType: 'Flow' },
      ],
      outputPorts: [
        { id: 'flow-out', name: 'Flow', dataType: 'Flow' },
      ],
      properties: [
        { id: 'filePath', name: 'Sound File', type: 'FilePath' },
      ],
    },
    {
      typeId: 'action.deleteFile',
      displayName: 'Delete File',
      category: 'Action',
      description: 'Delete a file from disk',
      color: '#22C55E',
      inputPorts: [
        { id: 'flow-in', name: 'Flow', dataType: 'Flow' },
      ],
      outputPorts: [
        { id: 'flow-out', name: 'Flow', dataType: 'Flow' },
      ],
      properties: [
        { id: 'filePath', name: 'File Path', type: 'FilePath' },
      ],
    },
  ];
}
