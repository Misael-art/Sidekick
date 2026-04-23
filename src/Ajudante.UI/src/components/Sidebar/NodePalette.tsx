import { useState, useMemo, type DragEvent } from 'react';
import { useFlowStore } from '../../store/flowStore';
import { useAppStore } from '../../store/appStore';
import type { NodeCategory } from '../../bridge/types';

const categoryColors: Record<NodeCategory, string> = {
  Trigger: '#EF4444',
  Logic: '#EAB308',
  Action: '#22C55E',
};

const categoryIcons: Record<NodeCategory, string> = {
  Trigger: '\u26A1',
  Logic: '\uD83D\uDD00',
  Action: '\u2699\uFE0F',
};

export default function NodePalette() {
  const nodeDefinitions = useFlowStore((s) => s.nodeDefinitions);
  const isPaletteOpen = useAppStore((s) => s.isPaletteOpen);
  const togglePalette = useAppStore((s) => s.togglePalette);
  const [filter, setFilter] = useState('');

  const grouped = useMemo(() => {
    const lowerFilter = filter.toLowerCase();
    const filtered = nodeDefinitions.filter(
      (d) =>
        d.displayName.toLowerCase().includes(lowerFilter) ||
        d.category.toLowerCase().includes(lowerFilter) ||
        d.description.toLowerCase().includes(lowerFilter) ||
        d.typeId.toLowerCase().includes(lowerFilter),
    );

    const groups: Record<NodeCategory, typeof filtered> = {
      Trigger: [],
      Logic: [],
      Action: [],
    };

    for (const def of filtered) {
      groups[def.category]?.push(def);
    }

    for (const category of Object.keys(groups) as NodeCategory[]) {
      groups[category].sort((a, b) => a.displayName.localeCompare(b.displayName));
    }

    return groups;
  }, [nodeDefinitions, filter]);

  const onDragStart = (event: DragEvent, typeId: string) => {
    event.dataTransfer.setData('application/ajudante-node', typeId);
    event.dataTransfer.effectAllowed = 'move';
  };

  if (!isPaletteOpen) {
    return (
      <div className="node-palette node-palette--collapsed">
        <button
          className="node-palette__toggle"
          onClick={togglePalette}
          title="Open palette"
        >
          &rsaquo;
        </button>
      </div>
    );
  }

  return (
    <div className="node-palette">
      <div className="node-palette__header">
        <span className="node-palette__title">Nodes</span>
        <button
          className="node-palette__toggle"
          onClick={togglePalette}
          title="Collapse palette"
        >
          &lsaquo;
        </button>
      </div>

      <div className="node-palette__search">
        <input
          type="text"
          placeholder="Search nodes..."
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          className="node-palette__input"
        />
      </div>

      <div className="node-palette__list">
        {(['Trigger', 'Logic', 'Action'] as NodeCategory[]).map((category) => {
          const items = grouped[category];
          if (items.length === 0) return null;

          return (
            <div key={category} className="node-palette__group">
              <div
                className="node-palette__group-header"
                style={{ borderLeftColor: categoryColors[category] }}
              >
                <span>{categoryIcons[category]}</span>
                <span>{category}</span>
                <span className="node-palette__count">{items.length}</span>
              </div>
              {items.map((def) => (
                <div
                  key={def.typeId}
                  className="node-palette__item"
                  draggable
                  onDragStart={(e) => onDragStart(e, def.typeId)}
                  title={def.description}
                >
                  <div
                    className="node-palette__item-dot"
                    style={{ backgroundColor: categoryColors[category] }}
                  />
                  <div className="node-palette__item-info">
                    <div className="node-palette__item-name">{def.displayName}</div>
                    <div className="node-palette__item-desc">{def.description}</div>
                  </div>
                </div>
              ))}
            </div>
          );
        })}

        {nodeDefinitions.length === 0 && (
          <div className="node-palette__empty">
            No nodes registered. Connect to the host to load the node registry.
          </div>
        )}
      </div>
    </div>
  );
}
