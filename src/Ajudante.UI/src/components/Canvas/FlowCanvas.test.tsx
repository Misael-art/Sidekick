// @vitest-environment jsdom

import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('@xyflow/react', async (importOriginal) => {
  const React = await import('react');
  const actual = await importOriginal<typeof import('@xyflow/react')>();

  return {
    ...actual,
    ReactFlow: ({ children, onInit, onPaneContextMenu, onConnectStart, onConnectEnd }: any) => {
      const paneRef = React.useRef<HTMLDivElement | null>(null);
      const rendererRef = React.useRef<HTMLDivElement | null>(null);
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
              onClick={() => onConnectStart?.({}, { nodeId: 'node-source', handleId: 'out', handleType: 'source' })}
            >
              connect-start
            </button>
            <button
              data-testid="connect-end-pane"
              onClick={() => onConnectEnd?.({ target: paneRef.current, clientX: 460, clientY: 240 })}
            >
              connect-end-pane
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
        processPath: 'C:\\Users\\misae\\AppData\\Local\\Programs\\Trae\\Trae.exe',
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
      processPath: 'C:\\Users\\misae\\AppData\\Local\\Programs\\Trae\\Trae.exe',
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
});
