// @vitest-environment jsdom

import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('@xyflow/react', async (importOriginal) => {
      const React = await import('react');
      const actual = await importOriginal<typeof import('@xyflow/react')>();

      return {
    ...actual,
    ReactFlow: ({
      children,
      connectionRadius,
      edges,
      isValidConnection,
      nodes,
      onEdgeContextMenu,
      onInit,
      onNodeContextMenu,
      onPaneContextMenu,
      onConnectStart,
      onConnectEnd,
    }: any) => {
      const paneRef = React.useRef<HTMLDivElement | null>(null);
      const rendererRef = React.useRef<HTMLDivElement | null>(null);
      const sourceHandleRef = React.useRef<HTMLButtonElement | null>(null);
      React.useEffect(() => {
        onInit?.({
          screenToFlowPosition: ({ x, y }: { x: number; y: number }) => ({ x, y }),
        });
      }, [onInit]);

      return (
        <div className="react-flow">
          <div
            data-testid="flow-pane"
            className="react-flow__pane"
            ref={paneRef}
            onContextMenu={(event) => onPaneContextMenu?.(event)}
          >
            <button
              data-testid="connect-start"
              className="react-flow__handle"
              ref={sourceHandleRef}
              onClick={() => onConnectStart?.({}, { nodeId: 'node-source', handleId: 'out', handleType: 'source' })}
            >
              connect-start
            </button>
            <button
              data-testid="connect-end-source-handle"
              className="react-flow__handle"
              onClick={() => onConnectEnd?.({ target: sourceHandleRef.current, clientX: 460, clientY: 240 })}
            >
              connect-end-source
            </button>
            <button
              data-testid="connect-end-pane"
              onClick={() => onConnectEnd?.({ target: paneRef.current, clientX: 460, clientY: 240 })}
            >
              connect-end-pane
            </button>
            <button
              data-testid="open-node-menu"
              onClick={() => onNodeContextMenu?.({
                preventDefault: () => {},
                clientX: 300,
                clientY: 220,
              }, nodes?.[0])}
            >
              node-menu
            </button>
            <button
              data-testid="open-edge-menu"
              onClick={() => onEdgeContextMenu?.({
                preventDefault: () => {},
                clientX: 360,
                clientY: 260,
              }, edges?.[0])}
            >
              edge-menu
            </button>
            <button
              data-testid="validate-incompatible"
              onClick={() => isValidConnection?.({
                source: 'node-source',
                sourceHandle: 'value',
                target: 'node-target',
                targetHandle: 'in',
              })}
            >
              validate-incompatible
            </button>
            {children}
          </div>
          <div
            data-testid="flow-renderer"
            className="react-flow__renderer"
            ref={rendererRef}
          >
            <button
              data-testid="connect-end-renderer"
              onClick={() => onConnectEnd?.({ target: rendererRef.current, clientX: 460, clientY: 240 })}
            >
              connect-end-renderer
            </button>
          </div>
          <div data-testid="connection-radius">{connectionRadius}</div>
        </div>
      );
    },
    MiniMap: () => null,
    Controls: () => null,
    Background: () => null,
    BackgroundVariant: actual.BackgroundVariant,
  };
});

describe('FlowCanvas context menu', () => {
  beforeEach(async () => {
    vi.resetModules();
    document.body.innerHTML = '';
    (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
  });

  it('creates a desktop click node from the latest Mira capture via right-click menu', async () => {
    const React = await import('react');
    const { default: FlowCanvas } = await import('./FlowCanvas');
    const { useFlowStore } = await import('../../store/flowStore');
    const { useAppStore } = await import('../../store/appStore');
    const { getDevNodeDefinitions } = await import('../../devNodeDefinitions');

    useFlowStore.setState({
      flowId: 'flow-context',
      flowName: 'Context Flow',
      nodes: [],
      edges: [],
      selectedNodeId: null,
      nodeDefinitions: getDevNodeDefinitions(),
    });

    useAppStore.setState({
      capturedElement: {
        automationId: 'continue-button',
        name: 'Continue',
        className: 'Button',
        controlType: 'button',
        boundingRect: { x: 100, y: 100, width: 80, height: 24 },
        processId: 1234,
        processName: 'Trae',
        processPath: '%LOCALAPPDATA%\\Programs\\Trae\\Trae.exe',
        windowTitle: 'Trae',
      },
      capturedRegion: null,
    });

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    act(() => {
      root.render(React.createElement(FlowCanvas));
    });

    const pane = container.querySelector('[data-testid="flow-pane"]')!;
    act(() => {
      pane.dispatchEvent(new MouseEvent('contextmenu', {
        bubbles: true,
        clientX: 420,
        clientY: 240,
      }));
    });

    const quickAction = Array.from(container.querySelectorAll('button'))
      .find((button) => button.textContent?.includes('Clicar alvo da Mira'));
    expect(quickAction).toBeTruthy();

    act(() => {
      quickAction!.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });

    const state = useFlowStore.getState();
    expect(state.nodes).toHaveLength(1);
    expect(state.nodes[0].data.typeId).toBe('action.desktopClickElement');
    expect(state.nodes[0].data.propertyValues).toMatchObject({
      windowTitle: 'Trae',
      windowTitleMatch: 'contains',
      processName: 'Trae',
      processPath: '%LOCALAPPDATA%\\Programs\\Trae\\Trae.exe',
      automationId: 'continue-button',
      elementName: 'Continue',
      controlType: 'button',
    });

    act(() => {
      root.unmount();
    });
  }, 15_000);

  it('creates and auto-connects a node when dropping a connection on empty pane', async () => {
    const React = await import('react');
    const { default: FlowCanvas } = await import('./FlowCanvas');
    const { useFlowStore } = await import('../../store/flowStore');
    const { getDevNodeDefinitions } = await import('../../devNodeDefinitions');

    useFlowStore.setState({
      flowId: 'flow-connect',
      flowName: 'Connect Flow',
      nodeDefinitions: getDevNodeDefinitions(),
      nodes: [
        {
          id: 'node-source',
          type: 'logicNode',
          position: { x: 50, y: 50 },
          data: {
            typeId: 'logic.delay',
            displayName: 'Delay',
            nodeAlias: '',
            nodeComment: '',
            category: 'Logic',
            color: '#EAB308',
            inputPorts: [{ id: 'in', name: 'In', dataType: 'Flow' }],
            outputPorts: [{ id: 'out', name: 'Out', dataType: 'Flow' }],
            properties: [],
            propertyValues: {},
          },
        },
      ],
      edges: [],
      selectedNodeId: null,
    });

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    act(() => {
      root.render(React.createElement(FlowCanvas));
    });

    const startButton = container.querySelector('[data-testid="connect-start"]') as HTMLButtonElement;
    const endButton = container.querySelector('[data-testid="connect-end-pane"]') as HTMLButtonElement;

    act(() => {
      startButton.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });

    await act(async () => {
      endButton.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });
    await act(async () => {
      await Promise.resolve();
    });

    const addDelayButton = Array.from(container.querySelectorAll('.flow-context-menu__item'))
      .find((button) => button.textContent?.includes('Delay')) as HTMLButtonElement | undefined;
    expect(addDelayButton).toBeTruthy();

    act(() => {
      addDelayButton!.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });

    const state = useFlowStore.getState();
    expect(state.nodes.length).toBe(2);
    expect(state.edges.length).toBe(1);
    expect(state.edges[0].source).toBe('node-source');
    expect(state.edges[0].sourceHandle).toBe('out');
    expect(state.edges[0].targetHandle).toBe('in');

    act(() => {
      root.unmount();
    });
  }, 15_000);

  it('opens the add menu when a connection ends on any empty canvas surface', async () => {
    const React = await import('react');
    const { default: FlowCanvas } = await import('./FlowCanvas');
    const { useFlowStore } = await import('../../store/flowStore');
    const { getDevNodeDefinitions } = await import('../../devNodeDefinitions');

    useFlowStore.setState({
      flowId: 'flow-connect-renderer',
      flowName: 'Connect Renderer Flow',
      nodeDefinitions: getDevNodeDefinitions(),
      nodes: [
        {
          id: 'node-source',
          type: 'logicNode',
          position: { x: 50, y: 50 },
          data: {
            typeId: 'logic.delay',
            displayName: 'Delay',
            nodeAlias: '',
            nodeComment: '',
            category: 'Logic',
            color: '#EAB308',
            inputPorts: [{ id: 'in', name: 'In', dataType: 'Flow' }],
            outputPorts: [{ id: 'out', name: 'Out', dataType: 'Flow' }],
            properties: [],
            propertyValues: {},
          },
        },
      ],
      edges: [],
      selectedNodeId: null,
    });

    const container = document.createElement('div');
    Object.defineProperty(container, 'getBoundingClientRect', {
      value: () => ({ left: 0, top: 0, right: 1000, bottom: 800, width: 1000, height: 800 }),
    });
    document.body.appendChild(container);
    const root = createRoot(container);

    act(() => {
      root.render(React.createElement(FlowCanvas));
    });

    const startButton = container.querySelector('[data-testid="connect-start"]') as HTMLButtonElement;
    const endButton = container.querySelector('[data-testid="connect-end-renderer"]') as HTMLButtonElement;

    act(() => {
      startButton.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });

    act(() => {
      endButton.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });

    expect(container.querySelector('.flow-context-menu')).toBeTruthy();

    act(() => {
      root.unmount();
    });
  }, 15_000);

  it('opens the add menu when a started connection is released without a target handle', async () => {
    const React = await import('react');
    const { default: FlowCanvas } = await import('./FlowCanvas');
    const { useFlowStore } = await import('../../store/flowStore');
    const { getDevNodeDefinitions } = await import('../../devNodeDefinitions');

    useFlowStore.setState({
      flowId: 'flow-connect-source-release',
      flowName: 'Connect Source Release Flow',
      nodeDefinitions: getDevNodeDefinitions(),
      nodes: [
        {
          id: 'node-source',
          type: 'triggerNode',
          position: { x: 50, y: 50 },
          data: {
            typeId: 'trigger.manualStart',
            displayName: 'Start Manual',
            nodeAlias: '',
            nodeComment: '',
            category: 'Trigger',
            color: '#EF4444',
            inputPorts: [],
            outputPorts: [{ id: 'triggered', name: 'Triggered', dataType: 'Flow' }],
            properties: [],
            propertyValues: {},
          },
        },
      ],
      edges: [],
      selectedNodeId: null,
    });

    const container = document.createElement('div');
    Object.defineProperty(container, 'getBoundingClientRect', {
      value: () => ({ left: 0, top: 0, right: 1000, bottom: 800, width: 1000, height: 800 }),
    });
    document.body.appendChild(container);
    const root = createRoot(container);

    act(() => {
      root.render(React.createElement(FlowCanvas));
    });

    const startButton = container.querySelector('[data-testid="connect-start"]') as HTMLButtonElement;
    const releaseButton = container.querySelector('[data-testid="connect-end-source-handle"]') as HTMLButtonElement;

    act(() => {
      startButton.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });

    act(() => {
      releaseButton.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });

    expect(container.querySelector('.flow-context-menu')).toBeTruthy();
    expect(container.querySelector('[data-testid="connection-radius"]')?.textContent).toBe('48');

    act(() => {
      root.unmount();
    });
  }, 15_000);

  it('duplicates and disables nodes from the node context menu', async () => {
    const React = await import('react');
    const { default: FlowCanvas } = await import('./FlowCanvas');
    const { useFlowStore } = await import('../../store/flowStore');
    const { getDevNodeDefinitions } = await import('../../devNodeDefinitions');

    useFlowStore.setState({
      flowId: 'flow-node-menu',
      flowName: 'Node Menu Flow',
      nodeDefinitions: getDevNodeDefinitions(),
      nodes: [
        {
          id: 'node-source',
          type: 'logicNode',
          position: { x: 50, y: 50 },
          data: {
            typeId: 'logic.delay',
            displayName: 'Delay',
            nodeAlias: '',
            nodeComment: '',
            category: 'Logic',
            color: '#EAB308',
            inputPorts: [{ id: 'in', name: 'In', dataType: 'Flow' }],
            outputPorts: [{ id: 'out', name: 'Out', dataType: 'Flow' }],
            properties: [],
            propertyValues: {},
          },
        },
      ],
      edges: [],
      selectedNodeId: null,
    });

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    act(() => {
      root.render(React.createElement(FlowCanvas));
    });

    act(() => {
      (container.querySelector('[data-testid="open-node-menu"]') as HTMLButtonElement)
        .dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });

    const duplicateButton = Array.from(container.querySelectorAll('button'))
      .find((button) => button.textContent?.includes('Duplicar'));
    const disableButton = Array.from(container.querySelectorAll('button'))
      .find((button) => button.textContent?.includes('Desabilitar'));
    expect(duplicateButton).toBeTruthy();
    expect(disableButton).toBeTruthy();

    act(() => {
      duplicateButton!.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });
    expect(useFlowStore.getState().nodes).toHaveLength(2);

    act(() => {
      (container.querySelector('[data-testid="open-node-menu"]') as HTMLButtonElement)
        .dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });
    const disableButtonAfterDuplicate = Array.from(container.querySelectorAll('button'))
      .find((button) => button.textContent?.includes('Desabilitar'));
    act(() => {
      disableButtonAfterDuplicate!.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });
    expect(useFlowStore.getState().nodes[0].data.nodeDisabled).toBe(true);

    act(() => {
      root.unmount();
    });
  }, 15_000);

  it('inserts a node into an edge from the edge context menu', async () => {
    const React = await import('react');
    const { default: FlowCanvas } = await import('./FlowCanvas');
    const { useFlowStore } = await import('../../store/flowStore');
    const { getDevNodeDefinitions } = await import('../../devNodeDefinitions');

    useFlowStore.setState({
      flowId: 'flow-edge-menu',
      flowName: 'Edge Menu Flow',
      nodeDefinitions: getDevNodeDefinitions(),
      nodes: [
        {
          id: 'node-source',
          type: 'triggerNode',
          position: { x: 50, y: 50 },
          data: {
            typeId: 'trigger.manualStart',
            displayName: 'Start Manual',
            nodeAlias: '',
            nodeComment: '',
            category: 'Trigger',
            color: '#EF4444',
            inputPorts: [],
            outputPorts: [{ id: 'triggered', name: 'Triggered', dataType: 'Flow' }],
            properties: [],
            propertyValues: {},
          },
        },
        {
          id: 'node-target',
          type: 'actionNode',
          position: { x: 360, y: 50 },
          data: {
            typeId: 'action.logMessage',
            displayName: 'Log',
            nodeAlias: '',
            nodeComment: '',
            category: 'Action',
            color: '#22C55E',
            inputPorts: [{ id: 'in', name: 'In', dataType: 'Flow' }],
            outputPorts: [{ id: 'out', name: 'Out', dataType: 'Flow' }],
            properties: [],
            propertyValues: {},
          },
        },
      ],
      edges: [
        {
          id: 'edge-source-target',
          source: 'node-source',
          sourceHandle: 'triggered',
          target: 'node-target',
          targetHandle: 'in',
          type: 'smoothstep',
        },
      ],
      selectedNodeId: null,
    });

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    act(() => {
      root.render(React.createElement(FlowCanvas));
    });

    act(() => {
      (container.querySelector('[data-testid="open-edge-menu"]') as HTMLButtonElement)
        .dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });

    const insertButton = Array.from(container.querySelectorAll('button'))
      .find((button) => button.textContent?.includes('Inserir passo'));
    expect(insertButton).toBeTruthy();

    act(() => {
      insertButton!.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });

    const addDelayButton = Array.from(container.querySelectorAll('.flow-context-menu__item'))
      .find((button) => button.textContent?.includes('Delay')) as HTMLButtonElement | undefined;
    expect(addDelayButton).toBeTruthy();

    act(() => {
      addDelayButton!.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });

    const state = useFlowStore.getState();
    expect(state.nodes).toHaveLength(3);
    expect(state.edges).toHaveLength(2);
    expect(state.edges.some((edge) => edge.source === 'node-source' && edge.target === state.selectedNodeId)).toBe(true);
    expect(state.edges.some((edge) => edge.source === state.selectedNodeId && edge.target === 'node-target')).toBe(true);

    act(() => {
      root.unmount();
    });
  }, 15_000);
});
