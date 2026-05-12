import { useEffect, useMemo, useRef, useState, type MouseEvent } from 'react';
import { sendCommand } from '../../bridge/bridge';
import type {
  CapturedElement,
  GuidedAutomationDraft,
  RecorderElementContext,
  RecorderEvent,
  SelectorDiagnosticResult,
} from '../../bridge/types';
import { useAppStore } from '../../store/appStore';
import { useFlowStore } from '../../store/flowStore';

function formatTimestamp(value?: string): string {
  if (!value) {
    return '';
  }

  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime())
    ? value
    : parsed.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
}

function describeEvent(event: RecorderEvent): string {
  if (event.kind === 'mouseClick' || event.kind === 'mouseDoubleClick') {
    const label = event.kind === 'mouseDoubleClick' ? 'Duplo clique' : 'Clique';
    const target = event.element?.name || event.element?.automationId || event.window?.windowTitle;
    return target ? `${label} em ${target}` : `${label} em ${event.mouse?.x ?? '?'} x ${event.mouse?.y ?? '?'}`;
  }

  if (event.kind === 'mouseDrag') {
    return `Arrastar de ${event.mouse?.startX ?? '?'} x ${event.mouse?.startY ?? '?'} ate ${event.mouse?.endX ?? '?'} x ${event.mouse?.endY ?? '?'}`;
  }

  if (event.kind === 'textInput') {
    return `Texto digitado (${event.text?.length ?? event.text?.value?.length ?? 0} caracteres)`;
  }

  if (event.kind === 'redactedInput') {
    return `Texto redigido (${event.text?.length ?? 0} caracteres)`;
  }

  if (event.kind === 'keyPress' || event.kind === 'hotkey') {
    return `Tecla ${event.keyboard?.key ?? 'capturada'}`;
  }

  if (event.kind === 'pause') {
    return `Pausa ${event.text?.length ?? 0} ms`;
  }

  return event.label || event.kind;
}

function eventWarnings(event: RecorderEvent): string[] {
  const warnings = [...(event.warnings ?? [])];
  if (event.privacy?.isRedacted && event.privacy.reason) {
    warnings.push(event.privacy.reason);
  }
  if ((event.confidence ?? 1) < 0.6) {
    warnings.push('Baixa confianca; revise seletor, janela ou coordenada.');
  }
  return warnings;
}

function collectReviewSignals(events: RecorderEvent[], draftWarnings: string[] = []): string[] {
  const signals = new Set<string>();
  const allWarnings = [...draftWarnings, ...events.flatMap(eventWarnings)].join(' ').toLocaleLowerCase('pt-BR');

  if (allWarnings.includes('coordenada') || events.some((event) => event.mouse?.x != null && !hasEventSelector(event))) {
    signals.add('Coordenada absoluta');
  }

  if (allWarnings.includes('redig') || events.some((event) => event.privacy?.isRedacted || event.kind === 'redactedInput')) {
    signals.add('Texto redigido');
  }

  if (allWarnings.includes('seletor fraco') || events.some((event) => (event.confidence ?? 1) < 0.6)) {
    signals.add('Seletor fraco');
  }

  return Array.from(signals);
}

interface TimelineItem {
  id: string;
  kind: 'event' | 'pauseGroup';
  events: RecorderEvent[];
  totalPauseMs: number;
}

function buildTimelineItems(events: RecorderEvent[]): TimelineItem[] {
  const items: TimelineItem[] = [];
  let pauseGroup: RecorderEvent[] = [];

  const flushPauseGroup = () => {
    if (pauseGroup.length === 0) {
      return;
    }

    items.push({
      id: `pause-group-${pauseGroup.map((event) => event.id).join('-')}`,
      kind: 'pauseGroup',
      events: pauseGroup,
      totalPauseMs: pauseGroup.reduce((sum, event) => sum + (event.text?.length ?? 0), 0),
    });
    pauseGroup = [];
  };

  for (const event of events) {
    if (event.kind === 'pause') {
      pauseGroup.push(event);
      continue;
    }

    flushPauseGroup();
    items.push({ id: event.id, kind: 'event', events: [event], totalPauseMs: 0 });
  }

  flushPauseGroup();
  return items;
}

function describeTimelineItem(item: TimelineItem): string {
  if (item.kind === 'pauseGroup') {
    const count = item.events.length;
    return `${count} pausa${count === 1 ? '' : 's'} - ${item.totalPauseMs} ms`;
  }

  return describeEvent(item.events[0]);
}

function timelineItemWarnings(item: TimelineItem): string[] {
  return item.events.flatMap(eventWarnings)
    .filter((value, index, all) => value && all.indexOf(value) === index);
}

function timelineItemTitle(item: TimelineItem): string {
  return item.kind === 'pauseGroup' ? 'Time lapse' : item.events[0].kind;
}

function hasEventSelector(event: RecorderEvent): boolean {
  return Boolean(
    event.element?.automationId
    || event.element?.name
    || event.element?.controlType
    || event.window?.windowTitle,
  );
}

function buildSelectorRequest(event: RecorderEvent) {
  return {
    windowTitle: event.element?.windowTitle || event.window?.windowTitle || '',
    automationId: event.element?.automationId || '',
    name: event.element?.name || '',
    elementName: event.element?.name || '',
    controlType: event.element?.controlType || '',
    processName: event.element?.processName || event.window?.processName || '',
    processPath: event.element?.processPath || event.window?.processPath || '',
    titleMatch: 'contains',
    timeoutMs: 1000,
    hasRelativeFallback: true,
    hasVisualFallback: false,
  };
}

function toRecorderElement(capture: CapturedElement): RecorderElementContext {
  return {
    automationId: capture.automationId,
    name: capture.name,
    className: capture.className,
    controlType: capture.controlType,
    windowTitle: capture.windowTitle,
    processName: capture.processName,
    processPath: capture.processPath,
    processId: capture.processId,
    bounds: capture.boundingRect,
    windowBounds: capture.windowBounds,
    relativeX: capture.relativePointX,
    relativeY: capture.relativePointY,
    normalizedX: capture.normalizedWindowX,
    normalizedY: capture.normalizedWindowY,
    absoluteX: capture.cursorScreen?.x,
    absoluteY: capture.cursorScreen?.y,
    cursorPixelColor: capture.cursorPixelColor,
    detectedText: capture.detectedText,
    currentText: capture.currentText,
    placeholderText: capture.placeholderText,
    selectorStrength: capture.selectorStrength,
    selectorStrategy: capture.selectorStrategy,
    isBrowserSurface: capture.isBrowserSurface,
    browserUrl: capture.browserUrl,
    browserOrigin: capture.browserOrigin,
    browserDocumentTitle: capture.browserDocumentTitle,
  };
}

function isCompatibleCapture(event: RecorderEvent, capture: CapturedElement): boolean {
  const eventProcess = event.element?.processName || event.window?.processName || '';
  const eventWindow = event.element?.windowTitle || event.window?.windowTitle || '';
  return Boolean(
    (eventProcess && capture.processName && eventProcess.toLowerCase() === capture.processName.toLowerCase())
    || (eventWindow && capture.windowTitle && capture.windowTitle.includes(eventWindow))
    || (event.element?.controlType && capture.controlType && event.element.controlType.toLowerCase() === capture.controlType.toLowerCase()),
  );
}

export default function MacroRecorderReview() {
  const guidedDraft = useAppStore((s) => s.guidedDraft);
  const setGuidedDraft = useAppStore((s) => s.setGuidedDraft);
  const setUserMessage = useAppStore((s) => s.setUserMessage);
  const addLog = useAppStore((s) => s.addLog);
  const capturedElement = useAppStore((s) => s.capturedElement);
  const addNode = useFlowStore((s) => s.addNode);
  const connectNodes = useFlowStore((s) => s.connectNodes);
  const [events, setEvents] = useState<RecorderEvent[]>(guidedDraft?.events ?? []);
  const [isApplying, setIsApplying] = useState(false);
  const [isDiagnosing, setIsDiagnosing] = useState(false);
  const [diagnostics, setDiagnostics] = useState<Record<string, SelectorDiagnosticResult>>({});
  const [error, setError] = useState('');
  const [removedEvents, setRemovedEvents] = useState<RecorderEvent[]>([]);
  const shellRef = useRef<HTMLDivElement | null>(null);
  const stopDragRef = useRef<(() => void) | null>(null);
  const [dialogPosition, setDialogPosition] = useState<{ left: number; top: number } | null>(null);

  useEffect(() => {
    // Review-local edits must reset when a new guided draft is selected.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setEvents(guidedDraft?.events ?? []);
    setDiagnostics({});
    setRemovedEvents([]);
    setError('');
  }, [guidedDraft?.events, guidedDraft?.id]);

  const warnings = useMemo(() => {
    const eventWarningList = events.flatMap(eventWarnings);
    return [...(guidedDraft?.warnings ?? []), ...eventWarningList]
      .filter((value, index, all) => value && all.indexOf(value) === index);
  }, [events, guidedDraft?.warnings]);

  const timelineItems = useMemo(() => buildTimelineItems(events), [events]);
  const reviewSignals = useMemo(() => collectReviewSignals(events, guidedDraft?.warnings), [events, guidedDraft?.warnings]);
  const safetySignals = useMemo(() => {
    const hasSelector = events.some((event) => hasEventSelector(event));
    const hasRelative = events.some((event) => {
      const element = event.element as (RecorderElementContext & Partial<CapturedElement>) | null | undefined;
      return element?.relativeX != null
        || element?.normalizedX != null
        || element?.relativePointX != null
        || element?.normalizedWindowX != null;
    });
    const hasFixed = events.some((event) => {
      const element = event.element as (RecorderElementContext & { absoluteX?: number }) | null | undefined;
      return event.mouse?.x != null || element?.absoluteX != null;
    });
    const hasPixel = events.some((event) => Boolean(event.element?.cursorPixelColor));
    const hasText = events.some((event) => Boolean(event.element?.detectedText || event.element?.name || event.text?.value));
    const hasProcess = events.some((event) => Boolean(event.window?.processName || event.element?.processName));
    const hasBrowser = events.some((event) => Boolean(event.element?.isBrowserSurface || event.element?.browserUrl));
    return [
      hasSelector ? 'Seletor Mira' : '',
      hasRelative ? 'Posicao relativa' : '',
      hasFixed ? 'Posicao fixa' : '',
      hasPixel ? 'Cor do pixel' : '',
      hasText ? 'Texto/elemento' : '',
      hasProcess ? 'Janela/processo' : '',
      hasBrowser ? 'Navegador' : '',
    ].filter(Boolean);
  }, [events]);

  useEffect(() => () => stopDragRef.current?.(), []);

  if (!guidedDraft) {
    return null;
  }

  const removeEvent = (eventId: string) => {
    const removed = events.find((event) => event.id === eventId);
    if (removed) {
      setRemovedEvents((current) => [...current, removed]);
    }
    setEvents((current) => current.filter((event) => event.id !== eventId));
    setDiagnostics((current) => {
      const next = { ...current };
      delete next[eventId];
      return next;
    });
  };

  const removeTimelineItem = (item: TimelineItem) => {
    const ids = new Set(item.events.map((event) => event.id));
    setRemovedEvents((current) => [...current, ...item.events]);
    setEvents((current) => current.filter((event) => !ids.has(event.id)));
    setDiagnostics((current) => {
      const next = { ...current };
      ids.forEach((id) => {
        delete next[id];
      });
      return next;
    });
  };

  const restoreEvent = (eventId: string) => {
    const eventToRestore = removedEvents.find((event) => event.id === eventId);
    if (!eventToRestore) {
      return;
    }

    setRemovedEvents((current) => current.filter((event) => event.id !== eventId));
    setEvents((current) => [...current, eventToRestore]
      .sort((left, right) => left.timestamp.localeCompare(right.timestamp)));
  };

  const startDrag = (event: MouseEvent<HTMLElement>) => {
    if ((event.target as HTMLElement).closest('button')) {
      return;
    }

    const rect = shellRef.current?.getBoundingClientRect();
    const left = dialogPosition?.left ?? rect?.left ?? event.clientX;
    const top = dialogPosition?.top ?? rect?.top ?? event.clientY;
    const usePointerOffset = Boolean(rect && (rect.width > 0 || rect.height > 0));
    const offset = {
      x: usePointerOffset ? event.clientX - left : 0,
      y: usePointerOffset ? event.clientY - top : 0,
    };

    const moveTo = (clientX: number, clientY: number) => {
      const maxLeft = Math.max(8, window.innerWidth - 120);
      const maxTop = Math.max(8, window.innerHeight - 80);
      setDialogPosition({
        left: Math.min(Math.max(8, clientX - offset.x), maxLeft),
        top: Math.min(Math.max(8, clientY - offset.y), maxTop),
      });
    };

    stopDragRef.current?.();
    const handleMove = (moveEvent: globalThis.MouseEvent) => moveTo(moveEvent.clientX, moveEvent.clientY);
    const handleUp = () => {
      window.removeEventListener('mousemove', handleMove);
      window.removeEventListener('mouseup', handleUp);
      stopDragRef.current = null;
    };

    setDialogPosition({ left, top });
    window.addEventListener('mousemove', handleMove);
    window.addEventListener('mouseup', handleUp);
    stopDragRef.current = handleUp;
    event.preventDefault();
  };

  const diagnoseSelectors = async () => {
    const selectableEvents = events.filter(hasEventSelector);
    if (selectableEvents.length === 0) {
      setUserMessage({ type: 'info', text: 'Nenhum evento com seletor para diagnosticar.' });
      return;
    }

    try {
      setIsDiagnosing(true);
      const response = await sendCommand<{ results: SelectorDiagnosticResult[] }>('assets', 'diagnoseSelectorBatch', {
        selectors: selectableEvents.map(buildSelectorRequest),
      });
      const next = selectableEvents.reduce<Record<string, SelectorDiagnosticResult>>((acc, event, index) => {
        const result = response.results?.[index];
        if (result) {
          acc[event.id] = result;
        }
        return acc;
      }, {});
      setDiagnostics(next);
      const weakCount = Object.values(next).filter((result) => result.strength === 'fraca' || result.strength === 'inexistente').length;
      setUserMessage({
        type: weakCount > 0 ? 'info' : 'success',
        text: weakCount > 0
          ? `${weakCount} seletor(es) precisam de reparo ou recaptura Mira.`
          : 'Seletores testados com boa resiliencia.',
      });
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Nao foi possivel diagnosticar seletores.';
      setError(message);
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
    } finally {
      setIsDiagnosing(false);
    }
  };

  const repairWithLatestMira = () => {
    if (!capturedElement) {
      setUserMessage({ type: 'info', text: 'Capture um alvo com Mira antes de reparar em lote.' });
      return;
    }

    const replacement = toRecorderElement(capturedElement);
    let repaired = 0;
    setEvents((current) => current.map((event) => {
      const diagnostic = diagnostics[event.id];
      const needsRepair = diagnostic?.repairAction === 'repairWithLatestCapture'
        || diagnostic?.strength === 'fraca'
        || diagnostic?.strength === 'inexistente';
      if (!needsRepair || !isCompatibleCapture(event, capturedElement)) {
        return event;
      }

      repaired += 1;
      return {
        ...event,
        element: replacement,
        confidence: Math.max(event.confidence ?? 0, 0.86),
        warnings: (event.warnings ?? []).filter((warning) => !warning.includes('seletor')),
      };
    }));

    setUserMessage({
      type: repaired > 0 ? 'success' : 'info',
      text: repaired > 0
        ? `${repaired} evento(s) reparado(s) com a ultima captura Mira.`
        : 'A ultima captura Mira nao parece compativel com os eventos fracos.',
    });
  };

  const applyDraft = async () => {
    try {
      setIsApplying(true);
      setError('');
      const editedDraft: GuidedAutomationDraft = {
        ...guidedDraft,
        events,
      };
      const converted = await sendCommand<GuidedAutomationDraft>('platform', 'convertMacroDraftToFlow', { draft: editedDraft });

      const idMap = new Map<string, string>();
      for (const node of converted.suggestedNodes ?? []) {
        if (node.typeId === 'trigger.manualStart' && idMap.size > 0) {
          continue;
        }

        const newId = addNode(node.typeId, node.position, node.properties);
        if (newId) {
          idMap.set(node.id, newId);
        }
      }

      for (const connection of converted.suggestedConnections ?? []) {
        const source = idMap.get(connection.sourceNodeId);
        const target = idMap.get(connection.targetNodeId);
        if (!source || !target) {
          continue;
        }

        connectNodes({
          source,
          sourceHandle: connection.sourcePort,
          target,
          targetHandle: connection.targetPort,
        });
      }

      setGuidedDraft(null);
      setUserMessage({ type: 'success', text: 'Rascunho aplicado desarmado. Revise propriedades e rode dry-run antes de executar.' });
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Nao foi possivel aplicar o rascunho.';
      setError(message);
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
      setUserMessage({ type: 'error', text: message });
    } finally {
      setIsApplying(false);
    }
  };

  return (
    <div
      ref={shellRef}
      className={`macro-review ${dialogPosition ? 'macro-review--free' : ''}`}
      role="dialog"
      aria-modal="false"
      aria-labelledby="macro-review-title"
      style={dialogPosition ? { left: `${dialogPosition.left}px`, top: `${dialogPosition.top}px`, right: 'auto', bottom: 'auto' } : undefined}
    >
      <div className="macro-review__panel">
        <header className="macro-review__header" onMouseDown={startDrag} title="Arraste para mover">
          <div>
            <span className="macro-review__eyebrow">Recorder</span>
            <h2 id="macro-review-title" className="macro-review__title">Revisar gravacao</h2>
            <p className="macro-review__subtitle">
              {guidedDraft.displayName} - {events.length} evento(s)
            </p>
          </div>
          <button
            type="button"
            className="macro-review__close"
            onClick={() => setGuidedDraft(null)}
            aria-label="Fechar revisao de gravacao"
          >
            x
          </button>
        </header>

        <section className="macro-review__scoreboard" aria-label="Robustez da gravacao">
          <div className="macro-review__score">
            <strong>Robustez {guidedDraft.score ?? 'n/d'}/100</strong>
            <span>Nada sera armado ou executado sem sua revisao.</span>
          </div>
          {reviewSignals.length > 0 && (
            <div className="macro-review__signals" aria-label="Sinais frageis detectados">
              {reviewSignals.map((signal) => (
                <span key={signal}>{signal}</span>
              ))}
            </div>
          )}
        </section>

        {warnings.length > 0 && (
          <section className="macro-review__warnings" aria-label="Avisos da gravacao">
            {warnings.slice(0, 5).map((warning) => (
              <span key={warning}>{warning}</span>
            ))}
          </section>
        )}

        <div className="macro-review__body">
          <section className="macro-review__timeline" aria-label="Timeline gravada">
            {events.length === 0 ? (
              <div className="macro-review__empty">Todos os eventos foram removidos.</div>
            ) : timelineItems.map((item, index) => {
              const event = item.events[0];
              return (
              <article key={item.id} className="macro-review__event">
                <div className="macro-review__event-index">{index + 1}</div>
                <div className="macro-review__event-copy">
                  <strong>{timelineItemTitle(item)}</strong>
                  <span>{describeTimelineItem(item)}</span>
                  <small>
                    {formatTimestamp(event.timestamp)}
                    {event.window?.windowTitle ? ` - ${event.window.windowTitle}` : ''}
                  </small>
                  {timelineItemWarnings(item).map((warning) => (
                    <em key={warning}>{warning}</em>
                  ))}
                  {diagnostics[event.id] && (
                    <em>
                      Selector Doctor: {diagnostics[event.id].strength} - {diagnostics[event.id].fallbackRecommendation}
                    </em>
                  )}
                </div>
                <button
                  type="button"
                  className="macro-review__remove"
                  onClick={() => item.kind === 'pauseGroup' ? removeTimelineItem(item) : removeEvent(event.id)}
                >
                  Remover
                </button>
              </article>
              );
            })}
          </section>

          <aside className="macro-review__preview" aria-label="Previa de nodes sugeridos">
            <strong>Previa</strong>
            <span>{guidedDraft.suggestedNodes?.length ?? 0} node(s) sugerido(s)</span>
            <span>{guidedDraft.suggestedConnections?.length ?? 0} conexao(oes)</span>
            <span>{guidedDraft.limitations?.[0] ?? 'Importacao sempre desarmada.'}</span>
            <div className="macro-review__safety" aria-label="Pontos de seguranca sugeridos">
              <strong>Pontos de seguranca</strong>
              {safetySignals.length === 0 ? (
                <span>Nenhum contexto forte capturado.</span>
              ) : safetySignals.map((signal) => (
                <span key={signal} className="macro-review__chip">{signal}</span>
              ))}
            </div>
            <button
              type="button"
              className="macro-review__btn macro-review__btn--secondary"
              onClick={() => { void diagnoseSelectors(); }}
              disabled={isDiagnosing}
            >
              {isDiagnosing ? 'Testando...' : 'Testar seletores'}
            </button>
            <button
              type="button"
              className="macro-review__btn macro-review__btn--secondary"
              onClick={repairWithLatestMira}
              disabled={!capturedElement || Object.keys(diagnostics).length === 0}
            >
              Reparar com Mira
            </button>
            {removedEvents.length > 0 && (
              <div className="macro-review__removed" aria-label="Passos removidos">
                <strong>
                  {removedEvents.length}
                  {' '}
                  {removedEvents.length === 1 ? 'passo removido' : 'passos removidos'}
                </strong>
                {removedEvents.slice(-3).map((event) => (
                  <button
                    key={event.id}
                    type="button"
                    className="macro-review__btn macro-review__btn--secondary"
                    onClick={() => restoreEvent(event.id)}
                  >
                    Restaurar passo
                  </button>
                ))}
              </div>
            )}
          </aside>
        </div>

        {error && (
          <div className="macro-review__error" role="alert">{error}</div>
        )}

        <footer className="macro-review__actions">
          <button
            type="button"
            className="macro-review__btn macro-review__btn--secondary"
            onClick={() => setEvents(guidedDraft.events)}
            disabled={isApplying}
          >
            Restaurar timeline
          </button>
          <button
            type="button"
            className="macro-review__btn macro-review__btn--secondary"
            onClick={() => setGuidedDraft(null)}
            disabled={isApplying}
          >
            Fechar
          </button>
          <button
            type="button"
            className="macro-review__btn macro-review__btn--primary"
            onClick={() => { void applyDraft(); }}
            disabled={isApplying || events.length === 0}
          >
            {isApplying ? 'Aplicando...' : 'Aplicar rascunho desarmado'}
          </button>
        </footer>
      </div>
    </div>
  );
}
