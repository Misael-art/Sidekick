import { beforeEach, describe, expect, it, vi } from 'vitest';

const sendCommandMock = vi.fn();

vi.mock('../bridge/bridge', () => ({
  sendCommand: sendCommandMock,
}));

describe('flowStore', () => {
  beforeEach(async () => {
    vi.resetModules();
    sendCommandMock.mockReset();
  });

  it('sends flowId when loading a flow', async () => {
    const { useFlowStore } = await import('./flowStore');

    sendCommandMock.mockResolvedValue({
      id: 'flow-123',
      name: 'Loaded Flow',
      nodes: [],
      connections: [],
    });

    await useFlowStore.getState().loadFlow('flow-123');

    expect(sendCommandMock).toHaveBeenCalledWith('flow', 'loadFlow', { flowId: 'flow-123' });
  }, 15_000);

  it('uses the backend response when creating a new flow', async () => {
    const { useFlowStore } = await import('./flowStore');

    sendCommandMock.mockResolvedValue({
      id: 'backend-flow-id',
      name: 'Untitled Flow',
    });

    await useFlowStore.getState().newFlow();

    const state = useFlowStore.getState();
    expect(sendCommandMock).toHaveBeenCalledWith('flow', 'newFlow', { name: 'Untitled Flow' });
    expect(state.flowId).toBe('backend-flow-id');
    expect(state.flowName).toBe('Untitled Flow');
    expect(state.nodes).toEqual([]);
    expect(state.edges).toEqual([]);
  });

  it('keeps the flow id returned by saveFlow', async () => {
    const { useFlowStore } = await import('./flowStore');

    useFlowStore.setState({
      flowId: '',
      flowName: 'Draft Flow',
      isDirty: true,
      lastPersistedSnapshot: '',
      nodes: [],
      edges: [],
      selectedNodeId: null,
      nodeDefinitions: [],
    });

    sendCommandMock.mockResolvedValue({
      flowId: 'saved-flow-id',
    });

    await useFlowStore.getState().saveFlow();

    expect(useFlowStore.getState().flowId).toBe('saved-flow-id');
    expect(useFlowStore.getState().isDirty).toBe(false);
  });

  it('marks the flow as dirty after local edits', async () => {
    const { useFlowStore } = await import('./flowStore');

    expect(useFlowStore.getState().isDirty).toBe(false);

    useFlowStore.getState().setFlowName('Edited Flow');

    expect(useFlowStore.getState().isDirty).toBe(true);
  });

  it('stores the latest validation result returned by the backend', async () => {
    const { useFlowStore } = await import('./flowStore');

    useFlowStore.setState({
      flowId: 'flow-validation',
      flowName: 'Validation Flow',
      isDirty: false,
      lastPersistedSnapshot: '',
      nodes: [],
      edges: [],
      selectedNodeId: null,
      nodeDefinitions: [],
      validationResult: null,
    });

    sendCommandMock.mockResolvedValue({
      isValid: false,
      errors: ['Missing trigger'],
      warnings: ['Unused node'],
      issues: [
        { severity: 'error', code: 'flow.trigger.missing', message: 'Missing trigger' },
        { severity: 'warning', code: 'node.unused', message: 'Unused node' },
      ],
    });

    const result = await useFlowStore.getState().validateFlow();

    expect(sendCommandMock).toHaveBeenCalledWith('engine', 'validateFlow', expect.any(Object));
    expect(result.isValid).toBe(false);
    expect(useFlowStore.getState().validationResult).toEqual(result);
  });

  it('clears dirty state after loading a flow', async () => {
    const { useFlowStore } = await import('./flowStore');

    useFlowStore.setState({
      flowId: 'draft-id',
      flowName: 'Draft Flow',
      isDirty: true,
      lastPersistedSnapshot: '',
      nodes: [],
      edges: [],
      selectedNodeId: null,
      nodeDefinitions: [],
    });

    sendCommandMock.mockResolvedValue({
      id: 'loaded-flow-id',
      name: 'Loaded Flow',
      nodes: [],
      connections: [],
    });

    await useFlowStore.getState().loadFlow('loaded-flow-id');

    expect(useFlowStore.getState().isDirty).toBe(false);
    expect(useFlowStore.getState().flowId).toBe('loaded-flow-id');
  });

  it('updates multiple node properties in a single write', async () => {
    const { useFlowStore } = await import('./flowStore');

    useFlowStore.setState({
      flowId: 'flow-1',
      flowName: 'Selector Flow',
      isDirty: false,
      lastPersistedSnapshot: JSON.stringify({
        flowId: 'flow-1',
        flowName: 'Selector Flow',
        nodes: [
          {
            id: 'node-browser',
            typeId: 'action.browserClick',
            position: { x: 0, y: 0 },
            propertyValues: {
              automationId: '',
              controlType: '',
              elementName: '',
              windowTitle: '',
            },
          },
        ],
        edges: [],
      }),
      nodes: [
        {
          id: 'node-browser',
          type: 'actionNode',
          position: { x: 0, y: 0 },
          data: {
            typeId: 'action.browserClick',
            displayName: 'Browser Click',
            category: 'Action',
            color: '#58a6ff',
            inputPorts: [],
            outputPorts: [],
            properties: [],
            propertyValues: {
              windowTitle: '',
              automationId: '',
              elementName: '',
              controlType: '',
            },
          },
        },
      ],
      edges: [],
      selectedNodeId: 'node-browser',
      nodeDefinitions: [],
    });

    useFlowStore.getState().updateNodeProperties('node-browser', {
      windowTitle: 'Portal',
      automationId: 'search-box',
      elementName: 'Search',
      controlType: 'Edit',
    });

    const propertyValues = useFlowStore.getState().nodes[0].data.propertyValues;
    expect(propertyValues).toMatchObject({
      windowTitle: 'Portal',
      automationId: 'search-box',
      elementName: 'Search',
      controlType: 'Edit',
    });
    expect(useFlowStore.getState().isDirty).toBe(true);
  });

  it('adds a node with capture-derived property overrides and selects it', async () => {
    const { useFlowStore } = await import('./flowStore');
    const { getDevNodeDefinitions } = await import('../devNodeDefinitions');

    useFlowStore.setState({
      flowId: 'flow-capture',
      flowName: 'Capture Flow',
      isDirty: false,
      lastPersistedSnapshot: '',
      nodes: [],
      edges: [],
      selectedNodeId: null,
      nodeDefinitions: getDevNodeDefinitions(),
    });

    const nodeId = useFlowStore.getState().addNode('action.desktopClickElement', { x: 320, y: 180 }, {
      windowTitle: 'Trae',
      windowTitleMatch: 'contains',
      processName: 'Trae',
      processPath: 'C:\\Users\\misae\\AppData\\Local\\Programs\\Trae\\Trae.exe',
      automationId: 'continue-button',
      elementName: 'Continue',
      controlType: 'button',
    });

    expect(nodeId).toBeTruthy();
    expect(useFlowStore.getState().selectedNodeId).toBe(nodeId);
    expect(useFlowStore.getState().nodes[0].data.propertyValues).toMatchObject({
      windowTitle: 'Trae',
      windowTitleMatch: 'contains',
      processName: 'Trae',
      processPath: 'C:\\Users\\misae\\AppData\\Local\\Programs\\Trae\\Trae.exe',
      automationId: 'continue-button',
      elementName: 'Continue',
      controlType: 'button',
    });
  });

  it('updates node metadata and marks flow as dirty', async () => {
    const { useFlowStore } = await import('./flowStore');

    useFlowStore.setState({
      flowId: 'flow-meta',
      flowName: 'Meta Flow',
      isDirty: false,
      lastPersistedSnapshot: JSON.stringify({
        flowId: 'flow-meta',
        flowName: 'Meta Flow',
        nodes: [
          {
            id: 'node-meta',
            typeId: 'logic.delay',
            position: { x: 0, y: 0 },
            nodeAlias: '',
            nodeComment: '',
            propertyValues: {},
          },
        ],
        edges: [],
      }),
      nodes: [
        {
          id: 'node-meta',
          type: 'logicNode',
          position: { x: 0, y: 0 },
          data: {
            typeId: 'logic.delay',
            displayName: 'Delay',
            nodeAlias: '',
            nodeComment: '',
            category: 'Logic',
            color: '#EAB308',
            inputPorts: [],
            outputPorts: [],
            properties: [],
            propertyValues: {},
          },
        },
      ],
      edges: [],
      selectedNodeId: 'node-meta',
      nodeDefinitions: [],
    });

    useFlowStore.getState().updateNodeMetadata('node-meta', {
      nodeAlias: 'Delay principal',
      nodeComment: 'Aguarda antes de continuar o fluxo',
    });

    const nodeData = useFlowStore.getState().nodes[0].data;
    expect(nodeData.nodeAlias).toBe('Delay principal');
    expect(nodeData.nodeComment).toContain('Aguarda');
    expect(useFlowStore.getState().isDirty).toBe(true);
  });
});
