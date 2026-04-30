// @vitest-environment jsdom

import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const sendCommandMock = vi.fn();

vi.mock('../../bridge/bridge', () => ({
  sendCommand: sendCommandMock,
}));

describe('PropertyPanel Mira selector binding', () => {
  beforeEach(async () => {
    vi.resetModules();
    sendCommandMock.mockReset();
    document.body.innerHTML = '';
    (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
  });

  it('applies a saved Mira asset to selector-based browser fields', async () => {
    const React = await import('react');
    const { default: PropertyPanel } = await import('./PropertyPanel');
    const { useFlowStore } = await import('../../store/flowStore');
    const { useAppStore } = await import('../../store/appStore');

    useFlowStore.setState({
      flowId: 'flow-browser',
      flowName: 'Browser Flow',
      isDirty: false,
      nodes: [
        {
          id: 'browser-node',
          type: 'actionNode',
          position: { x: 100, y: 100 },
          data: {
            typeId: 'action.browserClick',
            displayName: 'Browser Click',
            category: 'Action',
            color: '#58a6ff',
            inputPorts: [],
            outputPorts: [],
            properties: [
              { id: 'windowTitle', name: 'Window Title', type: 'String', defaultValue: '' },
              { id: 'automationId', name: 'Automation Id', type: 'String', defaultValue: '' },
              { id: 'elementName', name: 'Element Name', type: 'String', defaultValue: '' },
              { id: 'controlType', name: 'Control Type', type: 'String', defaultValue: '' },
              { id: 'timeoutMs', name: 'Timeout (ms)', type: 'Integer', defaultValue: 5000 },
            ],
            propertyValues: {
              windowTitle: '',
              automationId: '',
              elementName: '',
              controlType: '',
              timeoutMs: 5000,
            },
          },
        },
      ],
      edges: [],
      selectedNodeId: 'browser-node',
      nodeDefinitions: [],
    });

    useAppStore.setState({
      capturedElement: null,
      inspectionAssets: [
        {
          id: 'asset-search-box',
          kind: 'inspection',
          version: 1,
          createdAt: '2026-04-26T10:00:00.000Z',
          updatedAt: '2026-04-26T10:05:00.000Z',
          displayName: 'Search box',
          tags: ['portal', 'search'],
          notes: null,
          source: {
            processName: 'msedge',
            processId: 1111,
            windowTitle: 'Portal',
          },
          locator: {
            strategy: 'selectorPreferred',
            selector: {
              windowTitle: 'Portal',
              automationId: 'search-box',
              name: 'Search',
              className: 'Edit',
              controlType: 'Edit',
            },
            relativeBounds: { x: 10, y: 10, width: 80, height: 20 },
            absoluteBounds: { x: 100, y: 100, width: 80, height: 20 },
          },
          content: {
            name: 'Search',
            automationId: 'search-box',
            className: 'Edit',
            controlType: 'Edit',
            hostScreenWidth: 1920,
            hostScreenHeight: 1080,
          },
        },
      ],
      userMessage: null,
      logs: [],
    });

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(React.createElement(PropertyPanel));
    });

    const browseButton = Array.from(container.querySelectorAll('button'))
      .find((button) => button.textContent?.includes('Browse Mira'));
    expect(browseButton).toBeTruthy();

    await act(async () => {
      browseButton!.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });

    const assetButton = Array.from(container.querySelectorAll('button'))
      .find((button) => button.textContent?.includes('Search box'));
    expect(assetButton).toBeTruthy();

    await act(async () => {
      assetButton!.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });

    expect(useFlowStore.getState().nodes[0].data.propertyValues).toMatchObject({
      windowTitle: 'Portal',
      automationId: 'search-box',
      elementName: 'Search',
      controlType: 'Edit',
    });
    expect(useAppStore.getState().userMessage?.text).toContain('Seletor Mira aplicado');

    await act(async () => {
      root.unmount();
    });
  }, 15_000);
});
