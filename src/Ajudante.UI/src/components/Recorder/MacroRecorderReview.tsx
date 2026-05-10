import { useEffect, useMemo, useState } from 'react';
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
    selectorStrength: capture.selectorStrength,
    selectorStrategy: capture.selectorStrategy,
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

  useEffect(() => {
    setEvents(guidedDraft?.events ?? []);
    setDiagnostics({});
    setError('');
  }, [guidedDraft?.id]);

  const warnings = useMemo(() => {
    const eventWarningList = events.flatMap(eventWarnings);
    return [...(guidedDraft?.warnings ?? []), ...eventWarningList]
      .filter((value, index, all) => value && all.indexOf(value) === index);
  }, [events, guidedDraft?.warnings]);

  if (!guidedDraft) {
    return null;
  }

  const removeEvent = (eventId: string) => {
    setEvents((current) => current.filter((event) => event.id !== eventId));
    setDiagnostics((current) => {
      const next = { ...current };
      delete next[eventId];
      return next;
    });
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
    <div className="macro-review" role="dialog" aria-modal="false" aria-labelledby="macro-review-title">
      <div className="macro-review__panel">
        <header className="macro-review__header">
          <div>
            <span className="macro-review__eyebrow">Recorder</span>
            <h2 id="macro-review-title" className="macro-review__title">Revisar gravacao</h2>
            <p className="macro-review__subtitle">
              {guidedDraft.displayName} - {events.length} evento(s) - score {guidedDraft.score ?? 'n/d'}
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
            ) : events.map((event, index) => (
              <article key={event.id} className="macro-review__event">
                <div className="macro-review__event-index">{index + 1}</div>
                <div className="macro-review__event-copy">
                  <strong>{event.kind}</strong>
                  <span>{describeEvent(event)}</span>
                  <small>
                    {formatTimestamp(event.timestamp)}
                    {event.window?.windowTitle ? ` - ${event.window.windowTitle}` : ''}
                  </small>
                  {eventWarnings(event).map((warning) => (
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
                  onClick={() => removeEvent(event.id)}
                >
                  Remover
                </button>
              </article>
            ))}
          </section>

          <aside className="macro-review__preview" aria-label="Previa de nodes sugeridos">
            <strong>Previa</strong>
            <span>{guidedDraft.suggestedNodes?.length ?? 0} node(s) sugerido(s)</span>
            <span>{guidedDraft.suggestedConnections?.length ?? 0} conexao(oes)</span>
            <span>{guidedDraft.limitations?.[0] ?? 'Importacao sempre desarmada.'}</span>
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
