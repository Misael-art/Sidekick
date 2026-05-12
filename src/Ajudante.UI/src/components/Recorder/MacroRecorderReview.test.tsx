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

  it('summarizes consecutive pauses as a single time lapse in the review timeline', async () => {
    const React = await import('react');
    const { default: MacroRecorderReview } = await import('./MacroRecorderReview');
    const { useAppStore } = await import('../../store/appStore');

    const draft: GuidedAutomationDraft = {
      id: 'draft-pauses',
      sessionId: 'session-pauses',
      displayName: 'Pausas agrupadas',
      isDraft: true,
      startedAt: new Date(0).toISOString(),
      stoppedAt: new Date(6000).toISOString(),
      events: [
        {
          id: 'pause-1',
          kind: 'pause',
          timestamp: new Date(1000).toISOString(),
          label: 'Pausa',
          text: { value: null, length: 1500, isRedacted: false },
          privacy: { isRedacted: false, mode: 'default', reason: '' },
        },
        {
          id: 'pause-2',
          kind: 'pause',
          timestamp: new Date(2600).toISOString(),
          label: 'Pausa',
          text: { value: null, length: 1700, isRedacted: false },
          privacy: { isRedacted: false, mode: 'default', reason: '' },
        },
      ],
      suggestedNodes: [],
      suggestedConnections: [],
      warnings: [],
      limitations: [],
      score: 84,
    };

    useAppStore.setState({ guidedDraft: draft, userMessage: null });

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    act(() => {
      root.render(React.createElement(MacroRecorderReview));
    });

    expect(container.textContent).toContain('Time lapse');
    expect(container.textContent).toContain('2 pausas');
    expect(container.textContent).toContain('3200 ms');
    expect(container.querySelectorAll('.macro-review__event')).toHaveLength(1);

    act(() => {
      root.unmount();
    });
  }, 15_000);

  it('makes robustness, fragile signals, removal and restore explicit before applying', async () => {
    const React = await import('react');
    const { default: MacroRecorderReview } = await import('./MacroRecorderReview');
    const { useAppStore } = await import('../../store/appStore');

    const draft: GuidedAutomationDraft = {
      id: 'draft-robustness',
      sessionId: 'session-robustness',
      displayName: 'Revisao robustez',
      isDraft: true,
      events: [
        {
          id: 'coord-click',
          kind: 'mouseClick',
          timestamp: new Date(100).toISOString(),
          mouse: { x: 920, y: 540, button: 'left' },
          privacy: { isRedacted: false, mode: 'default', reason: '' },
          confidence: 0.35,
          warnings: ['Usa coordenada absoluta'],
        },
        {
          id: 'secret-input',
          kind: 'redactedInput',
          timestamp: new Date(200).toISOString(),
          text: { value: null, length: 8, isRedacted: true },
          privacy: { isRedacted: true, mode: 'redactSensitive', reason: 'Texto redigido para revisao' },
          confidence: 0.92,
          warnings: [],
        },
      ],
      suggestedNodes: [],
      suggestedConnections: [],
      warnings: ['Seletor fraco em 1 passo'],
      limitations: [],
      score: 52,
    };

    useAppStore.setState({ guidedDraft: draft, userMessage: null });

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    act(() => {
      root.render(React.createElement(MacroRecorderReview));
    });

    expect(container.textContent).toContain('Robustez 52/100');
    expect(container.textContent).toContain('Coordenada absoluta');
    expect(container.textContent).toContain('Texto redigido');
    expect(container.textContent).toContain('Seletor fraco');

    const removeFirst = Array.from(container.querySelectorAll('button'))
      .find((button) => button.textContent?.includes('Remover')) as HTMLButtonElement | undefined;
    expect(removeFirst).toBeTruthy();

    act(() => {
      removeFirst!.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });

    expect(container.textContent).toContain('1 passo removido');
    const restoreButton = Array.from(container.querySelectorAll('button'))
      .find((button) => button.textContent?.includes('Restaurar passo')) as HTMLButtonElement | undefined;
    expect(restoreButton).toBeTruthy();

    act(() => {
      restoreButton!.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });

    expect(container.querySelectorAll('.macro-review__event')).toHaveLength(2);

    act(() => {
      root.unmount();
    });
  }, 15_000);

  it('lets the user drag the review window away from the canvas area', async () => {
    const React = await import('react');
    const { default: MacroRecorderReview } = await import('./MacroRecorderReview');
    const { useAppStore } = await import('../../store/appStore');

    const draft: GuidedAutomationDraft = {
      id: 'draft-drag',
      sessionId: 'session-drag',
      displayName: 'Mover painel',
      isDraft: true,
      events: [
        {
          id: 'evt-1',
          kind: 'mouseClick',
          timestamp: new Date(100).toISOString(),
          mouse: { x: 10, y: 20, button: 'left' },
          privacy: { isRedacted: false, mode: 'default', reason: '' },
        },
      ],
      suggestedNodes: [],
      suggestedConnections: [],
      warnings: [],
      limitations: [],
      score: 90,
    };

    useAppStore.setState({ guidedDraft: draft, userMessage: null });

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    act(() => {
      root.render(React.createElement(MacroRecorderReview));
    });

    const shell = container.querySelector('.macro-review') as HTMLElement | null;
    const header = container.querySelector('.macro-review__header') as HTMLElement | null;
    expect(shell).toBeTruthy();
    expect(header).toBeTruthy();

    act(() => {
      header!.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, clientX: 600, clientY: 320 }));
      window.dispatchEvent(new MouseEvent('mousemove', { bubbles: true, clientX: 420, clientY: 180 }));
      window.dispatchEvent(new MouseEvent('mouseup', { bubbles: true }));
    });

    expect(shell!.style.left).toBe('420px');
    expect(shell!.style.top).toBe('180px');
    expect(shell!.className).toContain('macro-review--free');

    act(() => {
      root.unmount();
    });
  }, 15_000);
});
