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
      processPath: '%LOCALAPPDATA%\\Programs\\Trae\\Trae.exe',
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
      processPath: '%LOCALAPPDATA%\\Programs\\Trae\\Trae.exe',
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

  it('duplicates the selected node with a nearby position and copied properties', async () => {
    const { useFlowStore } = await import('./flowStore');
    const { getDevNodeDefinitions } = await import('../devNodeDefinitions');

    useFlowStore.setState({
      flowId: 'flow-duplicate',
      flowName: 'Duplicate Flow',
      isDirty: false,
      lastPersistedSnapshot: '',
      nodes: [],
      edges: [],
      selectedNodeId: null,
      nodeDefinitions: getDevNodeDefinitions(),
    });

    const sourceId = useFlowStore.getState().addNode('action.logMessage', { x: 120, y: 160 }, {
      message: 'Hello from source',
      level: 'warning',
    });

    const duplicateId = useFlowStore.getState().duplicateNode(sourceId!);

    const state = useFlowStore.getState();
    expect(duplicateId).toBeTruthy();
    expect(state.nodes).toHaveLength(2);
    expect(state.selectedNodeId).toBe(duplicateId);
    expect(state.nodes[1].position).toEqual({ x: 160, y: 200 });
    expect(state.nodes[1].data.typeId).toBe('action.logMessage');
    expect(state.nodes[1].data.propertyValues).toMatchObject({
      message: 'Hello from source',
      level: 'warning',
    });
    expect(state.isDirty).toBe(true);
  });

  it('toggles a node disabled flag without deleting the node', async () => {
    const { useFlowStore } = await import('./flowStore');
    const { getDevNodeDefinitions } = await import('../devNodeDefinitions');

    useFlowStore.setState({
      flowId: 'flow-disable',
      flowName: 'Disable Flow',
      isDirty: false,
      lastPersistedSnapshot: '',
      nodes: [],
      edges: [],
      selectedNodeId: null,
      nodeDefinitions: getDevNodeDefinitions(),
    });

    const nodeId = useFlowStore.getState().addNode('logic.delay', { x: 0, y: 0 });

    useFlowStore.getState().toggleNodeDisabled(nodeId!, true);
    expect(useFlowStore.getState().nodes[0].data.nodeDisabled).toBe(true);

    useFlowStore.getState().toggleNodeDisabled(nodeId!, false);
    expect(useFlowStore.getState().nodes[0].data.nodeDisabled).toBe(false);
  });

  it('inserts a new node into an existing edge', async () => {
    const { useFlowStore } = await import('./flowStore');
    const { getDevNodeDefinitions } = await import('../devNodeDefinitions');

    useFlowStore.setState({
      flowId: 'flow-insert',
      flowName: 'Insert Flow',
      isDirty: false,
      lastPersistedSnapshot: '',
      nodes: [],
      edges: [],
      selectedNodeId: null,
      nodeDefinitions: getDevNodeDefinitions(),
    });

    const sourceId = useFlowStore.getState().addNode('trigger.manualStart', { x: 0, y: 0 });
    const targetId = useFlowStore.getState().addNode('action.logMessage', { x: 420, y: 0 });
    useFlowStore.getState().onConnect({
      source: sourceId!,
      sourceHandle: 'triggered',
      target: targetId!,
      targetHandle: 'in',
    });
    const originalEdgeId = useFlowStore.getState().edges[0].id;

    const insertedId = useFlowStore.getState().insertNodeOnEdge(originalEdgeId, 'logic.delay', { x: 220, y: 0 });

    const state = useFlowStore.getState();
    expect(insertedId).toBeTruthy();
    expect(state.nodes.find((node) => node.id === insertedId)?.data.typeId).toBe('logic.delay');
    expect(state.edges).toHaveLength(2);
    expect(state.edges.some((edge) => edge.source === sourceId && edge.target === insertedId)).toBe(true);
    expect(state.edges.some((edge) => edge.source === insertedId && edge.target === targetId)).toBe(true);
    expect(state.edges.some((edge) => edge.id === originalEdgeId)).toBe(false);
  });

  it('rejects incompatible connections with an explanation', async () => {
    const { useFlowStore } = await import('./flowStore');
    const { getDevNodeDefinitions } = await import('../devNodeDefinitions');

    useFlowStore.setState({
      flowId: 'flow-invalid',
      flowName: 'Invalid Connection Flow',
      isDirty: false,
      lastPersistedSnapshot: '',
      nodes: [],
      edges: [],
      selectedNodeId: null,
      nodeDefinitions: getDevNodeDefinitions(),
    });

    const sourceId = useFlowStore.getState().addNode('action.jsonExtract', { x: 0, y: 0 });
    const targetId = useFlowStore.getState().addNode('action.logMessage', { x: 320, y: 0 });
    const result = useFlowStore.getState().connectNodes({
      source: sourceId!,
      sourceHandle: 'value',
      target: targetId!,
      targetHandle: 'in',
    });

    expect(result.ok).toBe(false);
    expect(result.reason).toContain('incompat');
    expect(useFlowStore.getState().edges).toHaveLength(0);
  });

  it('reconnects an existing edge when the new ports are compatible', async () => {
    const { useFlowStore } = await import('./flowStore');
    const { getDevNodeDefinitions } = await import('../devNodeDefinitions');

    useFlowStore.setState({
      flowId: 'flow-reconnect',
      flowName: 'Reconnect Flow',
      isDirty: false,
      lastPersistedSnapshot: '',
      nodes: [],
      edges: [],
      selectedNodeId: null,
      nodeDefinitions: getDevNodeDefinitions(),
    });

    const triggerId = useFlowStore.getState().addNode('trigger.manualStart', { x: 0, y: 0 });
    const firstTargetId = useFlowStore.getState().addNode('action.logMessage', { x: 320, y: 0 });
    const secondTargetId = useFlowStore.getState().addNode('logic.delay', { x: 320, y: 160 });
    useFlowStore.getState().onConnect({
      source: triggerId!,
      sourceHandle: 'triggered',
      target: firstTargetId!,
      targetHandle: 'in',
    });
    const edgeId = useFlowStore.getState().edges[0].id;

    const result = useFlowStore.getState().reconnectEdge(edgeId, {
      source: triggerId!,
      sourceHandle: 'triggered',
      target: secondTargetId!,
      targetHandle: 'in',
    });

    expect(result.ok).toBe(true);
    expect(useFlowStore.getState().edges).toHaveLength(1);
    expect(useFlowStore.getState().edges[0]).toMatchObject({
      id: edgeId,
      source: triggerId,
      target: secondTargetId,
      targetHandle: 'in',
    });
  });

  it('auto layouts nodes by flow depth', async () => {
    const { useFlowStore } = await import('./flowStore');
    const { getDevNodeDefinitions } = await import('../devNodeDefinitions');

    useFlowStore.setState({
      flowId: 'flow-layout',
      flowName: 'Layout Flow',
      isDirty: false,
      lastPersistedSnapshot: '',
      nodes: [],
      edges: [],
      selectedNodeId: null,
      nodeDefinitions: getDevNodeDefinitions(),
    });

    const triggerId = useFlowStore.getState().addNode('trigger.manualStart', { x: 900, y: 300 });
    const delayId = useFlowStore.getState().addNode('logic.delay', { x: 10, y: 500 });
    const logId = useFlowStore.getState().addNode('action.logMessage', { x: 20, y: 600 });
    useFlowStore.getState().onConnect({ source: triggerId!, sourceHandle: 'triggered', target: delayId!, targetHandle: 'in' });
    useFlowStore.getState().onConnect({ source: delayId!, sourceHandle: 'out', target: logId!, targetHandle: 'in' });

    useFlowStore.getState().autoLayout();

    const state = useFlowStore.getState();
    const trigger = state.nodes.find((node) => node.id === triggerId)!;
    const delay = state.nodes.find((node) => node.id === delayId)!;
    const log = state.nodes.find((node) => node.id === logId)!;
    expect(trigger.position.x).toBeLessThan(delay.position.x);
    expect(delay.position.x).toBeLessThan(log.position.x);
    expect(trigger.position.y).toBe(80);
  });
});
