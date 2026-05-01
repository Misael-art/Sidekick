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

describe('PropertyPanel dropdown rendering', () => {
  beforeEach(() => {
    vi.resetModules();
    sendCommandMock.mockReset();
    document.body.innerHTML = '';
    (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
  });

  it('renders all options for a Dropdown property', async () => {
    const React = await import('react');
    const { default: PropertyPanel } = await import('./PropertyPanel');
    const { useFlowStore } = await import('../../store/flowStore');

    useFlowStore.setState({
      flowId: 'flow-dd',
      flowName: 'DD Flow',
      isDirty: false,
      nodes: [
        {
          id: 'n1',
          type: 'actionNode',
          position: { x: 0, y: 0 },
          data: {
            typeId: 'action.mouseClick',
            displayName: 'Mouse Click',
            category: 'Action',
            color: '#22C55E',
            inputPorts: [],
            outputPorts: [],
            properties: [
              {
                id: 'button',
                name: 'Button',
                type: 'Dropdown',
                defaultValue: 'Left',
                options: ['Left', 'Right', 'Middle'],
              },
            ],
            propertyValues: { button: 'Right' },
          },
        },
      ],
      edges: [],
      selectedNodeId: 'n1',
      nodeDefinitions: [],
    });

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(React.createElement(PropertyPanel));
    });

    const select = container.querySelector('select[data-property-id="button"]') as HTMLSelectElement;
    expect(select).toBeTruthy();
    const optionTexts = Array.from(select.options).map((o) => o.value);
    expect(optionTexts).toContain('Left');
    expect(optionTexts).toContain('Right');
    expect(optionTexts).toContain('Middle');
    expect(select.value).toBe('Right');
    expect(select.getAttribute('aria-invalid')).toBe('false');

    await act(async () => {
      root.unmount();
    });
  }, 15_000);

  it('warns and preserves legacy invalid dropdown value', async () => {
    const React = await import('react');
    const { default: PropertyPanel } = await import('./PropertyPanel');
    const { useFlowStore } = await import('../../store/flowStore');

    useFlowStore.setState({
      flowId: 'flow-dd2',
      flowName: 'Legacy Flow',
      isDirty: false,
      nodes: [
        {
          id: 'n2',
          type: 'actionNode',
          position: { x: 0, y: 0 },
          data: {
            typeId: 'action.mouseClick',
            displayName: 'Mouse Click',
            category: 'Action',
            color: '#22C55E',
            inputPorts: [],
            outputPorts: [],
            properties: [
              {
                id: 'button',
                name: 'Button',
                type: 'Dropdown',
                defaultValue: 'Left',
                options: ['Left', 'Right', 'Middle'],
              },
            ],
            propertyValues: { button: 'lef' },
          },
        },
      ],
      edges: [],
      selectedNodeId: 'n2',
      nodeDefinitions: [],
    });

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(React.createElement(PropertyPanel));
    });

    const select = container.querySelector('select[data-property-id="button"]') as HTMLSelectElement;
    expect(select).toBeTruthy();
    expect(select.getAttribute('aria-invalid')).toBe('true');
    expect(select.value).toBe('lef');
    const orphanOption = Array.from(select.options).find((o) => o.value === 'lef');
    expect(orphanOption).toBeTruthy();
    expect(orphanOption!.disabled).toBe(true);
    const warning = container.querySelector('.property-field__warning');
    expect(warning).toBeTruthy();
    expect(warning!.textContent).toContain('lef');

    await act(async () => {
      root.unmount();
    });
  }, 15_000);
});
