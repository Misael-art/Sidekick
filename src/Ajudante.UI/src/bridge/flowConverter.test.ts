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

      expect(converted.nodes, sample.fileName).toHaveLength(sample.flow.nodes.length);
      expect(converted.edges, sample.fileName).toHaveLength(sample.flow.connections.length);
      expect(converted.nodes.map((node) => node.data.typeId), sample.fileName).toEqual(
        sample.flow.nodes.map((node) => node.typeId),
      );
    }
  });

  it('round-trips every demo flow through the frontend converter', () => {
    for (const sample of getSampleFlows()) {
      const converted = fromBackendFlow(sample.flow, testDefinitions);
      const roundTrip = toBackendFlow(sample.flow.id, sample.flow.name, converted.nodes, converted.edges);

      expect(roundTrip.id, sample.fileName).toBe(sample.flow.id);
      expect(roundTrip.name, sample.fileName).toBe(sample.flow.name);
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
});
