import { useFlowStore } from '../../store/flowStore';
import type { PropertyDefinition } from '../../bridge/types';

export default function PropertyPanel() {
  const selectedNodeId = useFlowStore((s) => s.selectedNodeId);
  const nodes = useFlowStore((s) => s.nodes);
  const updateNodeProperty = useFlowStore((s) => s.updateNodeProperty);
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
      {data.properties.length > 0 && (
        <div className="property-panel__section">
          <div className="property-panel__section-title">Properties</div>
          <div className="property-panel__fields">
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

    case 'Dropdown':
      return (
        <div className="property-field">
          {label}
          <select
            value={value ?? ''}
            onChange={(e) => onChange(e.target.value)}
            className="property-field__select"
          >
            <option value="">-- Select --</option>
            {(options ?? []).map((opt) => (
              <option key={opt} value={opt}>
                {opt}
              </option>
            ))}
          </select>
        </div>
      );

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

    // String, Hotkey, ImageTemplate and default
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
