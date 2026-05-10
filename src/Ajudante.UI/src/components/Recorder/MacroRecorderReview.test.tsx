// @vitest-environment jsdom

import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { GuidedAutomationDraft } from '../../bridge/types';

const sendCommandMock = vi.fn();

vi.mock('../../bridge/bridge', () => ({
  sendCommand: sendCommandMock,
}));

describe('MacroRecorderReview', () => {
  beforeEach(() => {
    vi.resetModules();
    sendCommandMock.mockReset();
    document.body.innerHTML = '';
    (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
  });

  it('removes timeline events before converting and applying the draft as unarmed nodes', async () => {
    const React = await import('react');
    const { default: MacroRecorderReview } = await import('./MacroRecorderReview');
    const { useAppStore } = await import('../../store/appStore');
    const { useFlowStore } = await import('../../store/flowStore');
    const { getDevNodeDefinitions } = await import('../../devNodeDefinitions');

    const draft: GuidedAutomationDraft = {
      id: 'draft-session',
      sessionId: 'session',
      displayName: 'Revisar gravacao',
      isDraft: true,
      startedAt: new Date(0).toISOString(),
      stoppedAt: new Date(1000).toISOString(),
      events: [
        {
          id: 'evt-1',
          kind: 'mouseClick',
          timestamp: new Date(100).toISOString(),
          label: 'Clique por coordenada',
          mouse: { x: 10, y: 20, button: 'left' },
          privacy: { isRedacted: false, mode: 'default', reason: '' },
          confidence: 0.4,
          warnings: ['Usa coordenada absoluta'],
        },
        {
          id: 'evt-2',
          kind: 'textInput',
          timestamp: new Date(200).toISOString(),
          label: 'Texto digitado',
          text: { value: 'ok', length: 2, isRedacted: false },
          privacy: { isRedacted: false, mode: 'default', reason: '' },
          confidence: 0.9,
          warnings: [],
        },
      ],
      suggestedNodes: [],
      suggestedConnections: [],
      warnings: ['Revise coordenadas absolutas'],
      limitations: [],
      score: 70,
    };

    const converted: GuidedAutomationDraft = {
      ...draft,
      events: [draft.events[1]],
      suggestedNodes: [
        {
          id: 'draft-step-1',
          typeId: 'action.keyboardType',
          position: { x: 120, y: 160 },
          properties: { text: 'ok', delayBetweenKeys: 0 },
          confidence: 0.9,
          warnings: [],
        },
      ],
      suggestedConnections: [],
      warnings: [],
    };

    sendCommandMock.mockResolvedValue(converted);
    useFlowStore.setState({
      flowId: 'flow-review',
      flowName: 'Review Flow',
      nodes: [],
      edges: [],
      stickyNotes: [],
      selectedNodeId: null,
      nodeDefinitions: getDevNodeDefinitions(),
    });
    useAppStore.setState({
      guidedDraft: draft,
      macroRecorderActive: false,
      userMessage: null,
    });

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    act(() => {
      root.render(React.createElement(MacroRecorderReview));
    });

    expect(container.textContent).toContain('Revisar gravacao');
    expect(container.textContent).toContain('mouseClick');
    expect(container.textContent).toContain('textInput');

    const removeFirst = Array.from(container.querySelectorAll('button'))
      .find((button) => button.textContent?.includes('Remover')) as HTMLButtonElement | undefined;
    expect(removeFirst).toBeTruthy();

    act(() => {
      removeFirst!.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });

    const applyButton = Array.from(container.querySelectorAll('button'))
      .find((button) => button.textContent?.includes('Aplicar rascunho desarmado')) as HTMLButtonElement | undefined;
    expect(applyButton).toBeTruthy();

    await act(async () => {
      applyButton!.dispatchEvent(new MouseEvent('click', { bubbles: true }));
      await Promise.resolve();
    });

    expect(sendCommandMock).toHaveBeenCalledWith(
      'platform',
      'convertMacroDraftToFlow',
      expect.objectContaining({
        draft: expect.objectContaining({
          events: [expect.objectContaining({ id: 'evt-2' })],
        }),
      }),
    );
    expect(useFlowStore.getState().nodes).toHaveLength(1);
    expect(useFlowStore.getState().nodes[0].data.typeId).toBe('action.keyboardType');
    expect(useAppStore.getState().guidedDraft).toBeNull();

    act(() => {
      root.unmount();
    });
  }, 15_000);
});
