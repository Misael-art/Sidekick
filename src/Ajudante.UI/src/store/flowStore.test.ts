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
  });

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
});
