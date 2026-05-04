import { readFileSync, readdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { fromBackendFlow, toBackendFlow, type BackendFlow } from './flowConverter';
import { getDevNodeDefinitions } from '../devNodeDefinitions';

function getSampleFlowsDirectory(): string {
  return resolve(process.cwd(), '..', '..', 'flows');
}

function getSampleFlows(): Array<{ fileName: string; flow: BackendFlow }> {
  const directory = getSampleFlowsDirectory();
  return readdirSync(directory)
    .filter((fileName: string) => fileName.endsWith('.json'))
    .sort()
    .map((fileName: string) => ({
      fileName,
      flow: JSON.parse(readFileSync(resolve(directory, fileName), 'utf-8')) as BackendFlow,
    }));
}

describe('flowConverter sample flows', () => {
  const testDefinitions = getDevNodeDefinitions();

  it('covers every node type used by the bundled demo flows', () => {
    const availableTypeIds = new Set(testDefinitions.map((definition) => definition.typeId));

    for (const sample of getSampleFlows()) {
      expect(
        sample.flow.nodes.map((node) => node.typeId).filter((typeId) => !availableTypeIds.has(typeId)),
        sample.fileName,
      ).toEqual([]);
    }
  });

  it('converts every demo flow to frontend nodes and edges without dropping known nodes', () => {
    for (const sample of getSampleFlows()) {
      const converted = fromBackendFlow(sample.flow, testDefinitions);
      const executableNodes = converted.nodes.filter((node) => node.type !== 'stickyNote');

      expect(converted.nodes, sample.fileName).toHaveLength(sample.flow.nodes.length + (sample.flow.annotations?.length ?? 0));
      expect(converted.edges, sample.fileName).toHaveLength(sample.flow.connections.length);
      expect(executableNodes.map((node) => node.data.typeId), sample.fileName).toEqual(
        sample.flow.nodes.map((node) => node.typeId),
      );
    }
  });

  it('round-trips every demo flow through the frontend converter', () => {
    for (const sample of getSampleFlows()) {
      const converted = fromBackendFlow(sample.flow, testDefinitions);
      const roundTrip = toBackendFlow(sample.flow.id, sample.flow.name, converted.nodes, converted.edges, {
        variables: converted.variables,
      });

      expect(roundTrip.id, sample.fileName).toBe(sample.flow.id);
      expect(roundTrip.name, sample.fileName).toBe(sample.flow.name);
      const sortVars = (list: { name: string }[]) => [...list].sort((a, b) => a.name.localeCompare(b.name));
      expect(sortVars(roundTrip.variables), sample.fileName).toEqual(sortVars(sample.flow.variables ?? []));
      expect(roundTrip.nodes.map((node) => node.typeId), sample.fileName).toEqual(
        sample.flow.nodes.map((node) => node.typeId),
      );
      expect(
        roundTrip.connections.map((connection) => `${connection.sourceNodeId}:${connection.sourcePort}->${connection.targetNodeId}:${connection.targetPort}`),
        sample.fileName,
      ).toEqual(
        sample.flow.connections.map((connection) => `${connection.sourceNodeId}:${connection.sourcePort}->${connection.targetNodeId}:${connection.targetPort}`),
      );
    }
  });

  it('persists node alias/comment only when requested', () => {
    const sample = getSampleFlows()[0].flow;
    const converted = fromBackendFlow(sample, testDefinitions);
    const firstNode = converted.nodes[0];
    firstNode.data.nodeAlias = 'Alias de teste';
    firstNode.data.nodeComment = 'Comentario de teste';

    const runtimeFlow = toBackendFlow(sample.id, sample.name, converted.nodes, converted.edges);
    const persistedFlow = toBackendFlow(sample.id, sample.name, converted.nodes, converted.edges, {
      persistUiMetadata: true,
    });

    expect(runtimeFlow.nodes[0].properties.__ui_alias).toBeUndefined();
    expect(runtimeFlow.nodes[0].properties.__ui_comment).toBeUndefined();
    expect(runtimeFlow.nodes[0].properties['__ui.alias']).toBeUndefined();
    expect(runtimeFlow.nodes[0].properties['__ui.comment']).toBeUndefined();
    expect(persistedFlow.nodes[0].properties['__ui.alias']).toBe('Alias de teste');
    expect(persistedFlow.nodes[0].properties['__ui.comment']).toBe('Comentario de teste');
  });

  it('persists disabled node metadata only when requested', () => {
    const sample = getSampleFlows()[0].flow;
    const converted = fromBackendFlow(sample, testDefinitions);
    converted.nodes[0].data.nodeDisabled = true;

    const runtimeFlow = toBackendFlow(sample.id, sample.name, converted.nodes, converted.edges);
    const persistedFlow = toBackendFlow(sample.id, sample.name, converted.nodes, converted.edges, {
      persistUiMetadata: true,
    });

    expect(runtimeFlow.nodes[0].properties['__ui.disabled']).toBeUndefined();
    expect(persistedFlow.nodes[0].properties['__ui.disabled']).toBe(true);
  });

  it('round-trips sticky annotations without converting them into executable nodes', () => {
    const backend: BackendFlow = {
      id: 'sticky-flow',
      name: 'Sticky Flow',
      version: 1,
      variables: [],
      nodes: [
        { id: 'start', typeId: 'trigger.manualStart', position: { x: 0, y: 0 }, properties: {} },
      ],
      connections: [],
      annotations: [
        {
          id: 'sticky-1',
          title: 'Atenção',
          body: 'Capturar -> Aplicar -> Testar',
          color: 'blue',
          position: { x: 200, y: 120 },
          width: 320,
          height: 180,
        },
      ],
    };

    const converted = fromBackendFlow(backend, testDefinitions);
    expect(converted.nodes).toHaveLength(2);
    expect(converted.nodes.find((node) => node.type === 'stickyNote')?.data).toMatchObject({
      title: 'Atenção',
      body: 'Capturar -> Aplicar -> Testar',
      color: 'blue',
      width: 320,
      height: 180,
    });

    const roundTrip = toBackendFlow(backend.id, backend.name, converted.nodes, converted.edges, {
      persistUiMetadata: true,
    });

    expect(roundTrip.nodes.map((node) => node.id)).toEqual(['start']);
    expect(roundTrip.annotations).toEqual([
      expect.objectContaining({
        id: 'sticky-1',
        title: 'Atenção',
        body: 'Capturar -> Aplicar -> Testar',
        color: 'blue',
        position: { x: 200, y: 120 },
        width: 320,
        height: 180,
      }),
    ]);
  });

  it('bypasses a disabled node in runtime view when it has one input and one output edge', () => {
    const converted = fromBackendFlow({
      id: 'disabled-runtime-flow',
      name: 'Disabled Runtime Flow',
      version: 1,
      variables: [],
      nodes: [
        { id: 'trigger', typeId: 'trigger.manualStart', position: { x: 0, y: 0 }, properties: {} },
        { id: 'delay', typeId: 'logic.delay', position: { x: 240, y: 0 }, properties: { milliseconds: 1000, '__ui.disabled': true } },
        { id: 'log', typeId: 'action.logMessage', position: { x: 480, y: 0 }, properties: { message: 'after delay' } },
      ],
      connections: [
        { id: 'edge-trigger-delay', sourceNodeId: 'trigger', sourcePort: 'triggered', targetNodeId: 'delay', targetPort: 'in' },
        { id: 'edge-delay-log', sourceNodeId: 'delay', sourcePort: 'out', targetNodeId: 'log', targetPort: 'in' },
      ],
    }, testDefinitions);

    const runtimeFlow = toBackendFlow('disabled-runtime-flow', 'Disabled Runtime Flow', converted.nodes, converted.edges, {
      runtimeView: true,
    });
    const persistedFlow = toBackendFlow('disabled-runtime-flow', 'Disabled Runtime Flow', converted.nodes, converted.edges, {
      persistUiMetadata: true,
    });

    expect(runtimeFlow.nodes.map((node) => node.id)).toEqual(['trigger', 'log']);
    expect(runtimeFlow.connections).toEqual([
      expect.objectContaining({
        sourceNodeId: 'trigger',
        sourcePort: 'triggered',
        targetNodeId: 'log',
        targetPort: 'in',
      }),
    ]);
    expect(persistedFlow.nodes.map((node) => node.id)).toEqual(['trigger', 'delay', 'log']);
    expect(persistedFlow.connections).toHaveLength(2);
  });

  it('exposes backend variables from fromBackendFlow and preserves them in toBackendFlow when passed in options', () => {
    const backend: BackendFlow = {
      id: 'vars-flow',
      name: 'Vars',
      version: 1,
      variables: [
        { name: 'robloxBlockKey', type: 'string', default: 'X' },
        { name: 'bloqueioAte', type: 'string', default: '' },
      ],
      nodes: [{ id: 't', typeId: 'trigger.manualStart', position: { x: 0, y: 0 }, properties: {} }],
      connections: [],
    };
    const converted = fromBackendFlow(backend, testDefinitions);
    expect(converted.variables).toHaveLength(2);
    expect(converted.variables.map((v) => v.name).sort()).toEqual(['bloqueioAte', 'robloxBlockKey']);

    const out = toBackendFlow(backend.id, backend.name, converted.nodes, converted.edges, {
      variables: converted.variables,
    });
    const sortVars = (list: { name: string }[]) => [...list].sort((a, b) => a.name.localeCompare(b.name));
    expect(sortVars(out.variables)).toEqual(sortVars(backend.variables));
  });
});
