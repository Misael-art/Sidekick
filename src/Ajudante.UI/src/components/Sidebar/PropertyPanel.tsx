import { useMemo, useState } from 'react';
import { sendCommand } from '../../bridge/bridge';
import type {
  CapturedElement,
  ImageTemplateValue,
  InspectionAsset,
  PropertyDefinition,
  SnipAsset,
  SnipAssetTemplatePayload,
} from '../../bridge/types';
import { useAppStore } from '../../store/appStore';
import { useFlowStore } from '../../store/flowStore';
import { describeInvalidDropdown, isValidDropdownValue } from '../../utils/propertyValidation';

export default function PropertyPanel() {
  const selectedNodeId = useFlowStore((s) => s.selectedNodeId);
  const nodes = useFlowStore((s) => s.nodes);
  const updateNodeProperty = useFlowStore((s) => s.updateNodeProperty);
  const updateNodeProperties = useFlowStore((s) => s.updateNodeProperties);
  const updateNodeMetadata = useFlowStore((s) => s.updateNodeMetadata);
  const removeNode = useFlowStore((s) => s.removeNode);

  const selectedNode = nodes.find((n) => n.id === selectedNodeId);

  if (!selectedNode) {
    return (
      <div className="property-panel property-panel--empty">
        <div className="property-panel__placeholder">
          Select a node to view its properties
        </div>
      </div>
    );
  }

  const { data } = selectedNode;
  const selectorPropertyIds = getInspectionSelectorPropertyIds(data.properties);

  const handleChange = (propertyId: string, value: any) => {
    updateNodeProperty(selectedNode.id, propertyId, value);
  };

  return (
    <div className="property-panel">
      {/* Node info header */}
      <div className="property-panel__header">
        <div
          className="property-panel__color-bar"
          style={{ backgroundColor: data.color }}
        />
        <div className="property-panel__info">
          <div className="property-panel__node-name">{data.displayName}</div>
          <div className="property-panel__node-category">{data.category}</div>
        </div>
        <button
          className="property-panel__delete-btn"
          onClick={() => removeNode(selectedNode.id)}
          title="Delete node"
        >
          &#x2715;
        </button>
      </div>

      {/* Ports info */}
      <div className="property-panel__section">
        <div className="property-panel__section-title">Ports</div>
        {data.inputPorts.length > 0 && (
          <div className="property-panel__ports">
            <span className="property-panel__port-label">In:</span>
            {data.inputPorts.map((p) => (
              <span key={p.id} className="property-panel__port-tag">
                {p.name} <small>({p.dataType})</small>
              </span>
            ))}
          </div>
        )}
        {data.outputPorts.length > 0 && (
          <div className="property-panel__ports">
            <span className="property-panel__port-label">Out:</span>
            {data.outputPorts.map((p) => (
              <span key={p.id} className="property-panel__port-tag">
                {p.name} <small>({p.dataType})</small>
              </span>
            ))}
          </div>
        )}
      </div>

      {/* Properties form */}
      <div className="property-panel__section">
        <div className="property-panel__section-title">Identificacao</div>
        <div className="property-panel__fields">
          <div className="property-field">
            <label className="property-field__label" title="Nome amigavel exibido no card do node">
              Nome de identificacao
            </label>
            <input
              type="text"
              className="property-field__input"
              value={data.nodeAlias ?? ''}
              onChange={(event) => updateNodeMetadata(selectedNode.id, { nodeAlias: event.target.value })}
              placeholder={data.displayName}
            />
          </div>
          <div className="property-field">
            <label className="property-field__label" title="Comentario exibido no hover do node">
              Comentario
            </label>
            <textarea
              className="property-field__input property-field__textarea"
              value={data.nodeComment ?? ''}
              onChange={(event) => updateNodeMetadata(selectedNode.id, { nodeComment: event.target.value })}
              placeholder="Descreva o proposito deste node"
              rows={3}
            />
          </div>
        </div>
      </div>

      {/* Properties form */}
      {data.properties.length > 0 && (
        <div className="property-panel__section">
          <div className="property-panel__section-title">Properties</div>
          <div className="property-panel__fields">
            {selectorPropertyIds && (
              <InspectionSelectorField
                nodeId={selectedNode.id}
                nodeName={data.displayName}
                propertyValues={data.propertyValues}
                selectorPropertyIds={selectorPropertyIds}
                onApplyProperties={(propertyValues) => updateNodeProperties(selectedNode.id, propertyValues)}
              />
            )}
            {data.properties.map((prop) => (
              <PropertyField
                key={prop.id}
                definition={prop}
                value={data.propertyValues[prop.id]}
                onChange={(v) => handleChange(prop.id, v)}
              />
            ))}
          </div>
        </div>
      )}

      {/* Node ID (debug) */}
      <div className="property-panel__footer">
        <small>ID: {selectedNode.id}</small>
      </div>
    </div>
  );
}

// ── PropertyField Component ───────────────────────────────────────

interface PropertyFieldProps {
  definition: PropertyDefinition;
  value: any;
  onChange: (value: any) => void;
}

function getErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof Error && error.message.trim()) {
    return error.message;
  }

  return fallback;
}

function normalizeSearchText(value: string): string {
  return value.trim().toLocaleLowerCase('en-US');
}

function formatAssetTimestamp(value?: string): string {
  if (!value) {
    return 'Unknown date';
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleString();
}

interface InspectionSelectorPropertyIds {
  windowTitleId: string;
  windowTitleMatchId?: string;
  processNameId?: string;
  processPathId?: string;
  automationIdId?: string;
  elementNameId?: string;
  controlTypeId?: string;
}

interface InspectionSelection {
  displayName: string;
  windowTitle?: string | null;
  windowTitleMatch?: string | null;
  processName?: string | null;
  processPath?: string | null;
  automationId?: string | null;
  elementName?: string | null;
  controlType?: string | null;
}

function getStringPropertyValue(propertyValues: Record<string, unknown>, propertyId?: string): string {
  if (!propertyId) {
    return '';
  }

  const value = propertyValues[propertyId];
  return typeof value === 'string' ? value : '';
}

function getInspectionSelectorPropertyIds(properties: PropertyDefinition[]): InspectionSelectorPropertyIds | null {
  const windowTitleId = properties.find((property) => property.id === 'windowTitle')?.id;
  const windowTitleMatchId = properties.find((property) => property.id === 'windowTitleMatch')?.id;
  const processNameId = properties.find((property) => property.id === 'processName')?.id;
  const processPathId = properties.find((property) => property.id === 'processPath')?.id;
  const automationIdId = properties.find((property) => property.id === 'automationId')?.id;
  const elementNameId = properties.find((property) => property.id === 'elementName' || property.id === 'name')?.id;
  const controlTypeId = properties.find((property) => property.id === 'controlType')?.id;

  if (!windowTitleId || (!automationIdId && !elementNameId && !controlTypeId)) {
    return null;
  }

  return {
    windowTitleId,
    windowTitleMatchId,
    processNameId,
    processPathId,
    automationIdId,
    elementNameId,
    controlTypeId,
  };
}

function buildInspectionSearchText(asset: InspectionAsset): string {
  return [
    asset.displayName,
    asset.id,
    asset.source.windowTitle ?? '',
    asset.source.processName ?? '',
    asset.locator.selector.automationId ?? '',
    asset.locator.selector.name ?? '',
    asset.locator.selector.className ?? '',
    asset.locator.selector.controlType ?? '',
    ...asset.tags,
  ].join(' ').toLocaleLowerCase('en-US');
}

function createInspectionSelectionFromAsset(asset: InspectionAsset): InspectionSelection {
  return {
    displayName: asset.displayName,
    windowTitle: asset.locator.selector.windowTitle ?? asset.source.windowTitle ?? '',
    windowTitleMatch: 'contains',
    processName: asset.source.processName ?? '',
    processPath: asset.source.processPath ?? '',
    automationId: asset.locator.selector.automationId ?? asset.content.automationId ?? '',
    elementName: asset.locator.selector.name ?? asset.content.name ?? '',
    controlType: asset.locator.selector.controlType ?? asset.content.controlType ?? '',
  };
}

function createInspectionSelectionFromCapturedElement(capturedElement: CapturedElement): InspectionSelection {
  const displayName = capturedElement.asset?.displayName
    ?? capturedElement.name
    ?? capturedElement.automationId
    ?? 'Latest Mira capture';

  return {
    displayName,
    windowTitle: capturedElement.windowTitle,
    windowTitleMatch: 'contains',
    processName: capturedElement.processName ?? capturedElement.asset?.source.processName ?? '',
    processPath: capturedElement.processPath ?? capturedElement.asset?.source.processPath ?? '',
    automationId: capturedElement.automationId,
    elementName: capturedElement.name,
    controlType: capturedElement.controlType,
  };
}

function buildInspectionBindingSummary(
  propertyValues: Record<string, unknown>,
  selectorPropertyIds: InspectionSelectorPropertyIds,
): { title: string; meta: string } {
  const windowTitle = getStringPropertyValue(propertyValues, selectorPropertyIds.windowTitleId);
  const processName = getStringPropertyValue(propertyValues, selectorPropertyIds.processNameId);
  const automationId = getStringPropertyValue(propertyValues, selectorPropertyIds.automationIdId);
  const elementName = getStringPropertyValue(propertyValues, selectorPropertyIds.elementNameId);
  const controlType = getStringPropertyValue(propertyValues, selectorPropertyIds.controlTypeId);

  const title = automationId || elementName || controlType || 'Nenhum seletor Mira aplicado';
  const metaParts = [windowTitle, processName && `Process: ${processName}`, automationId && `AutomationId: ${automationId}`, elementName && `Name: ${elementName}`, controlType && `Type: ${controlType}`]
    .filter(Boolean);

  return {
    title,
    meta: metaParts.length > 0
      ? metaParts.join(' • ')
      : 'Use um ativo Mira salvo ou a captura mais recente para preencher este seletor.',
  };
}

interface InspectionSelectorFieldProps {
  nodeId: string;
  nodeName: string;
  propertyValues: Record<string, unknown>;
  selectorPropertyIds: InspectionSelectorPropertyIds;
  onApplyProperties: (propertyValues: Record<string, unknown>) => void;
}

function InspectionSelectorField({
  nodeName,
  propertyValues,
  selectorPropertyIds,
  onApplyProperties,
}: InspectionSelectorFieldProps) {
  const inspectionAssets = useAppStore((s) => s.inspectionAssets);
  const capturedElement = useAppStore((s) => s.capturedElement);
  const addLog = useAppStore((s) => s.addLog);
  const setUserMessage = useAppStore((s) => s.setUserMessage);
  const [isBrowserOpen, setIsBrowserOpen] = useState(false);
  const [filter, setFilter] = useState('');

  const filteredAssets = useMemo(() => {
    const normalizedFilter = normalizeSearchText(filter);
    if (!normalizedFilter) {
      return inspectionAssets;
    }

    return inspectionAssets.filter((asset) => buildInspectionSearchText(asset).includes(normalizedFilter));
  }, [filter, inspectionAssets]);

  const currentSummary = useMemo(
    () => buildInspectionBindingSummary(propertyValues, selectorPropertyIds),
    [propertyValues, selectorPropertyIds],
  );

  const applySelection = (selection: InspectionSelection) => {
    onApplyProperties({
      [selectorPropertyIds.windowTitleId]: selection.windowTitle ?? '',
      ...(selectorPropertyIds.windowTitleMatchId ? { [selectorPropertyIds.windowTitleMatchId]: selection.windowTitleMatch ?? 'contains' } : {}),
      ...(selectorPropertyIds.processNameId ? { [selectorPropertyIds.processNameId]: selection.processName ?? '' } : {}),
      ...(selectorPropertyIds.processPathId ? { [selectorPropertyIds.processPathId]: selection.processPath ?? '' } : {}),
      ...(selectorPropertyIds.automationIdId ? { [selectorPropertyIds.automationIdId]: selection.automationId ?? '' } : {}),
      ...(selectorPropertyIds.elementNameId ? { [selectorPropertyIds.elementNameId]: selection.elementName ?? '' } : {}),
      ...(selectorPropertyIds.controlTypeId ? { [selectorPropertyIds.controlTypeId]: selection.controlType ?? '' } : {}),
    });

    setUserMessage({ type: 'success', text: `Seletor Mira aplicado em ${nodeName}.` });
  };

  const clearSelection = () => {
    onApplyProperties({
      [selectorPropertyIds.windowTitleId]: '',
      ...(selectorPropertyIds.windowTitleMatchId ? { [selectorPropertyIds.windowTitleMatchId]: 'equals' } : {}),
      ...(selectorPropertyIds.processNameId ? { [selectorPropertyIds.processNameId]: '' } : {}),
      ...(selectorPropertyIds.processPathId ? { [selectorPropertyIds.processPathId]: '' } : {}),
      ...(selectorPropertyIds.automationIdId ? { [selectorPropertyIds.automationIdId]: '' } : {}),
      ...(selectorPropertyIds.elementNameId ? { [selectorPropertyIds.elementNameId]: '' } : {}),
      ...(selectorPropertyIds.controlTypeId ? { [selectorPropertyIds.controlTypeId]: '' } : {}),
    });

    setUserMessage({ type: 'info', text: `Seletor Mira limpo em ${nodeName}.` });
  };

  const handleSelectAsset = (asset: InspectionAsset) => {
    try {
      applySelection(createInspectionSelectionFromAsset(asset));
      setIsBrowserOpen(false);
    } catch (error) {
      const message = getErrorMessage(error, 'Falha ao vincular o ativo Mira selecionado.');
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
      setUserMessage({ type: 'error', text: message });
    }
  };

  const handleUseLatestCapture = () => {
    if (!capturedElement) {
      return;
    }

    applySelection(createInspectionSelectionFromCapturedElement(capturedElement));
  };

  const currentAutomationId = getStringPropertyValue(propertyValues, selectorPropertyIds.automationIdId);
  const latestCapturedTitle = capturedElement
    ? (capturedElement.asset?.displayName
      ?? capturedElement.name
      ?? capturedElement.automationId
      ?? 'Captured element')
    : null;

  return (
    <div className="property-field property-field--inspection">
      <label className="property-field__label">Seletor Mira</label>

      <div className="property-field__snip-current">
        <div className="property-field__snip-current-copy">
          <div className="property-field__snip-current-title">{currentSummary.title}</div>
          <div className="property-field__snip-current-meta">{currentSummary.meta}</div>
        </div>
      </div>

      {capturedElement && (
        <div className="property-field__inspection-latest">
          <div className="property-field__inspection-latest-title">Ultima captura Mira</div>
          <div className="property-field__inspection-latest-meta">
            {latestCapturedTitle}
            {' • '}
            {capturedElement.windowTitle || 'Janela indisponivel'}
          </div>
        </div>
      )}

      <div className="property-field__snip-actions">
        <button
          type="button"
          className="property-field__browse-btn"
          onClick={() => setIsBrowserOpen((state) => !state)}
        >
          {isBrowserOpen ? 'Hide Mira' : 'Browse Mira'}
        </button>
        <button
          type="button"
          className="property-field__browse-btn"
          onClick={handleUseLatestCapture}
          disabled={!capturedElement}
          title={capturedElement ? 'Use a ultima captura feita com Mira' : 'Capture um elemento com Mira primeiro'}
        >
          Use Latest
        </button>
        <button
          type="button"
          className="property-field__browse-btn"
          onClick={clearSelection}
          disabled={!currentSummary.meta || currentSummary.title === 'Nenhum seletor Mira aplicado'}
        >
          Limpar
        </button>
      </div>

      {isBrowserOpen && (
        <div className="property-field__snip-browser">
          <div className="property-field__snip-browser-header">
            <input
              type="text"
              value={filter}
              onChange={(event) => setFilter(event.target.value)}
              className="property-field__input"
              placeholder="Filtrar ativos Mira salvos"
            />
            <span className="property-field__snip-count">
              {filteredAssets.length}/{inspectionAssets.length}
            </span>
          </div>

          {inspectionAssets.length === 0 ? (
            <div className="property-field__snip-empty">
              Nenhum ativo Mira salvo ainda. Use o botao Mira na toolbar para capturar um elemento.
            </div>
          ) : filteredAssets.length === 0 ? (
            <div className="property-field__snip-empty">
              Nenhum ativo Mira corresponde a este filtro.
            </div>
          ) : (
            <div className="property-field__snip-list" role="listbox" aria-label="Ativos Mira salvos">
              {filteredAssets.map((asset) => {
                const assetSelection = createInspectionSelectionFromAsset(asset);
                const isSelected = Boolean(currentAutomationId && assetSelection.automationId && currentAutomationId === assetSelection.automationId);

                return (
                  <button
                    key={asset.id}
                    type="button"
                    className={`property-field__snip-option ${isSelected ? 'property-field__snip-option--selected' : ''}`}
                    onClick={() => handleSelectAsset(asset)}
                  >
                    <span className="property-field__snip-option-title">{asset.displayName}</span>
                    <span className="property-field__snip-option-meta">
                      {asset.source.windowTitle?.trim() || asset.source.processName?.trim() || 'Janela indisponivel'}
                    </span>
                    <span className="property-field__snip-option-meta">
                      Selector {asset.locator.strength ?? 'fraca'} • {asset.locator.strategy}
                      {asset.source.processPath ? ' • processPath' : ''}
                    </span>
                    <span className="property-field__snip-option-meta">
                      {(asset.locator.selector.automationId || asset.locator.selector.name || asset.locator.selector.controlType || asset.id)}
                      {' • '}
                      {formatAssetTimestamp(asset.updatedAt)}
                    </span>
                  </button>
                );
              })}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function readImageTemplateValue(value: unknown): ImageTemplateValue | null {
  if (!value || typeof value !== 'object' || Array.isArray(value)) {
    return null;
  }

  const candidate = value as Partial<ImageTemplateValue>;
  if (typeof candidate.imageBase64 !== 'string' || !candidate.imageBase64.trim()) {
    return null;
  }

  return {
    kind: candidate.kind === 'inline' ? 'inline' : 'snipAsset',
    imageBase64: candidate.imageBase64,
    assetId: typeof candidate.assetId === 'string' ? candidate.assetId : undefined,
    displayName: typeof candidate.displayName === 'string' ? candidate.displayName : undefined,
    imagePath: typeof candidate.imagePath === 'string' ? candidate.imagePath : undefined,
  };
}

function PropertyField({ definition, value, onChange }: PropertyFieldProps) {
  const { type, name, description, options } = definition;

  const label = (
    <label className="property-field__label" title={description ?? ''}>
      {name}
    </label>
  );

  switch (type) {
    case 'Boolean':
      return (
        <div className="property-field property-field--checkbox">
          {label}
          <input
            type="checkbox"
            checked={!!value}
            onChange={(e) => onChange(e.target.checked)}
            className="property-field__checkbox"
          />
        </div>
      );

    case 'Integer':
      return (
        <div className="property-field">
          {label}
          <input
            type="number"
            step={1}
            value={value ?? 0}
            onChange={(e) => onChange(parseInt(e.target.value, 10) || 0)}
            className="property-field__input"
          />
        </div>
      );

    case 'Float':
      return (
        <div className="property-field">
          {label}
          <input
            type="number"
            step={0.1}
            value={value ?? 0}
            onChange={(e) => onChange(parseFloat(e.target.value) || 0)}
            className="property-field__input"
          />
        </div>
      );

    case 'Dropdown': {
      const dropdownOptions = options ?? [];
      const isInvalid = !isValidDropdownValue(value, dropdownOptions);
      const warningMessage = isInvalid ? describeInvalidDropdown(value, dropdownOptions) : null;
      const showOrphan = isInvalid && value !== null && value !== undefined && value !== '';
      return (
        <div className="property-field">
          {label}
          <select
            value={value ?? ''}
            onChange={(e) => onChange(e.target.value)}
            className={`property-field__select${isInvalid ? ' property-field__select--invalid' : ''}`}
            aria-invalid={isInvalid}
            data-property-id={definition.id}
          >
            <option value="">-- Select --</option>
            {dropdownOptions.map((opt) => (
              <option key={opt} value={opt}>
                {opt}
              </option>
            ))}
            {showOrphan && (
              <option key="__legacy" value={String(value)} disabled>
                {String(value)} (invalid)
              </option>
            )}
          </select>
          {warningMessage && (
            <div className="property-field__warning" role="alert">
              {warningMessage}
            </div>
          )}
        </div>
      );
    }

    case 'FilePath':
    case 'FolderPath':
      return (
        <div className="property-field">
          {label}
          <div className="property-field__file-row">
            <input
              type="text"
              value={value ?? ''}
              onChange={(e) => onChange(e.target.value)}
              className="property-field__input"
              placeholder={type === 'FilePath' ? 'Select a file...' : 'Select a folder...'}
            />
            <button
              type="button"
              className="property-field__browse-btn"
              onClick={() => {
                // In WebView2, the host would open a native file dialog
                // For now, this is a placeholder
              }}
              title="Browse"
            >
              ...
            </button>
          </div>
        </div>
      );

    case 'Color':
      return (
        <div className="property-field">
          {label}
          <input
            type="color"
            value={value ?? '#ffffff'}
            onChange={(e) => onChange(e.target.value)}
            className="property-field__color"
          />
        </div>
      );

    case 'Point':
      return (
        <div className="property-field">
          {label}
          <div className="property-field__point-row">
            <input
              type="number"
              placeholder="X"
              value={value?.x ?? 0}
              onChange={(e) =>
                onChange({ ...value, x: parseInt(e.target.value, 10) || 0 })
              }
              className="property-field__input property-field__input--half"
            />
            <input
              type="number"
              placeholder="Y"
              value={value?.y ?? 0}
              onChange={(e) =>
                onChange({ ...value, y: parseInt(e.target.value, 10) || 0 })
              }
              className="property-field__input property-field__input--half"
            />
          </div>
        </div>
      );

    case 'ImageTemplate':
      return <ImageTemplateField definition={definition} value={value} onChange={onChange} />;

    // String, Hotkey and default
    default:
      return (
        <div className="property-field">
          {label}
          <input
            type="text"
            value={value ?? ''}
            onChange={(e) => onChange(e.target.value)}
            className="property-field__input"
            placeholder={description ?? ''}
          />
        </div>
      );
  }
}

function ImageTemplateField({ definition, value, onChange }: PropertyFieldProps) {
  const snipAssets = useAppStore((s) => s.snipAssets);
  const capturedRegion = useAppStore((s) => s.capturedRegion);
  const addLog = useAppStore((s) => s.addLog);
  const setUserMessage = useAppStore((s) => s.setUserMessage);
  const [isBrowserOpen, setIsBrowserOpen] = useState(false);
  const [filter, setFilter] = useState('');
  const [pendingAssetId, setPendingAssetId] = useState<string | null>(null);

  const currentValue = readImageTemplateValue(value);
  const filteredAssets = useMemo(() => {
    const normalizedFilter = normalizeSearchText(filter);
    if (!normalizedFilter) {
      return snipAssets;
    }

    return snipAssets.filter((asset) => {
      const haystack = [
        asset.displayName,
        asset.id,
        asset.source.windowTitle ?? '',
        asset.source.processName ?? '',
      ].join(' ').toLocaleLowerCase('en-US');
      return haystack.includes(normalizedFilter);
    });
  }, [filter, snipAssets]);

  const currentTitle = currentValue?.displayName?.trim()
    || (typeof value === 'string' && value.trim() ? 'Template embutido' : 'Nenhum template selecionado');
  const currentMeta = currentValue?.assetId
    ? `Ativo Snip salvo ${currentValue.assetId}`
    : currentValue?.imageBase64
      ? 'Imagem embutida'
      : definition.description ?? 'Selecione um ativo Snip salvo para vincular este template.';

  const handleSelectAsset = async (asset: SnipAsset) => {
    try {
      setPendingAssetId(asset.id);
      const template = await sendCommand<SnipAssetTemplatePayload>('assets', 'getSnipAssetTemplate', {
        assetId: asset.id,
      });

      onChange({
        kind: 'snipAsset',
        assetId: template.assetId,
        displayName: template.displayName,
        imagePath: template.imagePath,
        imageBase64: template.imageBase64,
      } satisfies ImageTemplateValue);

      setIsBrowserOpen(false);
      setUserMessage({ type: 'success', text: `Ativo Snip "${template.displayName}" vinculado a ${definition.name}.` });
    } catch (error) {
      const message = getErrorMessage(error, 'Falha ao vincular o ativo Snip selecionado.');
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
      setUserMessage({ type: 'error', text: message });
    } finally {
      setPendingAssetId(null);
    }
  };

  const handleUseLatestCapture = () => {
    if (!capturedRegion?.image) {
      return;
    }

    onChange({
      kind: capturedRegion.asset?.id ? 'snipAsset' : 'inline',
      assetId: capturedRegion.asset?.id,
      displayName: capturedRegion.asset?.displayName ?? 'Latest capture',
      imagePath: capturedRegion.asset?.content.imagePath,
      imageBase64: capturedRegion.image,
    } satisfies ImageTemplateValue);

    setUserMessage({ type: 'success', text: `Ultima captura do Snip vinculada a ${definition.name}.` });
  };

  return (
    <div className="property-field">
      <label className="property-field__label" title={definition.description ?? ''}>
        {definition.name}
      </label>

      <div className="property-field__snip-current">
        <div className="property-field__snip-current-copy">
          <div className="property-field__snip-current-title">{currentTitle}</div>
          <div className="property-field__snip-current-meta">{currentMeta}</div>
        </div>
        {currentValue?.imageBase64 && (
          <img
            className="property-field__snip-preview"
            src={`data:image/png;base64,${currentValue.imageBase64}`}
            alt={currentTitle}
          />
        )}
      </div>

      <div className="property-field__snip-actions">
        <button
          type="button"
          className="property-field__browse-btn"
          onClick={() => setIsBrowserOpen((state) => !state)}
        >
          {isBrowserOpen ? 'Hide Snips' : 'Browse Snips'}
        </button>
        <button
          type="button"
          className="property-field__browse-btn"
          onClick={handleUseLatestCapture}
          disabled={!capturedRegion?.image}
          title={capturedRegion?.image ? 'Use the latest region captured with Snip' : 'Capture a region with Snip first'}
        >
          Use Latest
        </button>
        <button
          type="button"
          className="property-field__browse-btn"
          onClick={() => onChange('')}
          disabled={!value}
        >
          Limpar
        </button>
      </div>

      {isBrowserOpen && (
        <div className="property-field__snip-browser">
          <div className="property-field__snip-browser-header">
            <input
              type="text"
              value={filter}
              onChange={(event) => setFilter(event.target.value)}
              className="property-field__input"
              placeholder="Filtrar Snips salvos"
            />
            <span className="property-field__snip-count">
              {filteredAssets.length}/{snipAssets.length}
            </span>
          </div>

          {snipAssets.length === 0 ? (
            <div className="property-field__snip-empty">
              Nenhum ativo Snip salvo ainda. Use o botao Snip na toolbar para capturar uma regiao.
            </div>
          ) : filteredAssets.length === 0 ? (
            <div className="property-field__snip-empty">
              Nenhum ativo Snip corresponde a este filtro.
            </div>
          ) : (
            <div className="property-field__snip-list" role="listbox" aria-label="Ativos Snip salvos">
              {filteredAssets.map((asset) => (
                <button
                  key={asset.id}
                  type="button"
                  className={`property-field__snip-option ${currentValue?.assetId === asset.id ? 'property-field__snip-option--selected' : ''}`}
                  onClick={() => { void handleSelectAsset(asset); }}
                  disabled={pendingAssetId === asset.id}
                >
                  <span className="property-field__snip-option-title">{asset.displayName}</span>
                  <span className="property-field__snip-option-meta">
                    {asset.captureBounds.width} x {asset.captureBounds.height} px • {formatAssetTimestamp(asset.updatedAt)}
                  </span>
                  <span className="property-field__snip-option-meta">
                    {asset.source.windowTitle?.trim() || asset.source.processName?.trim() || asset.id}
                  </span>
                  {pendingAssetId === asset.id && (
                    <span className="property-field__snip-option-status">Vinculando...</span>
                  )}
                </button>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
