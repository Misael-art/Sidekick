import { useState, useMemo, type DragEvent } from 'react';
import { useFlowStore } from '../../store/flowStore';
import { useAppStore } from '../../store/appStore';
import { useTranslation } from '../../i18n';
import {
  getNodeCapabilityTier,
  getNodeProductCategory,
  getProductCategoryColor,
  getProductCategoryIcon,
  getProductCategoryOrder,
  type ProductCapabilityTier,
  type ProductNodeCategory,
} from '../../utils/nodeProductCategory';

const capabilityLabels: Record<ProductCapabilityTier, string> = {
  Core: 'Core',
  Labs: 'Labs / Avancado',
};

const productCategoryLabels: Record<ProductNodeCategory, string> = {
  Trigger: 'Gatilhos',
  Desktop: 'Desktop',
  Window: 'Janelas',
  Hardware: 'Hardware',
  Media: 'Midia',
  Console: 'Console',
  Logic: 'Logica',
  Data: 'Dados',
  Utility: 'Utilitarios',
};

export default function NodePalette() {
  const nodeDefinitions = useFlowStore((s) => s.nodeDefinitions);
  const isPaletteOpen = useAppStore((s) => s.isPaletteOpen);
  const togglePalette = useAppStore((s) => s.togglePalette);
  const [filter, setFilter] = useState('');
  const [showLabs, setShowLabs] = useState(false);
  const { t } = useTranslation();

  const normalizedFilter = filter.trim().toLocaleLowerCase('pt-BR');
  const isSearching = normalizedFilter.length > 0;

  const grouped = useMemo(() => {
    const filtered = nodeDefinitions.filter((definition) => {
      const productCategory = getNodeProductCategory(definition);
      const capability = getNodeCapabilityTier(definition);
      const haystack = [
        definition.displayName,
        definition.category,
        definition.description,
        definition.typeId,
        productCategory,
        productCategoryLabels[productCategory],
        capability,
        capabilityLabels[capability],
      ].join(' ').toLocaleLowerCase('pt-BR');

      return haystack.includes(normalizedFilter);
    });

    const createCategoryGroups = () => getProductCategoryOrder().reduce<Record<ProductNodeCategory, typeof filtered>>((acc, category) => {
      acc[category] = [];
      return acc;
    }, {} as Record<ProductNodeCategory, typeof filtered>);

    const groups: Record<ProductCapabilityTier, Record<ProductNodeCategory, typeof filtered>> = {
      Core: createCategoryGroups(),
      Labs: createCategoryGroups(),
    };

    for (const definition of filtered) {
      const tier = getNodeCapabilityTier(definition);
      const category = getNodeProductCategory(definition);
      groups[tier][category].push(definition);
    }

    for (const tier of ['Core', 'Labs'] satisfies ProductCapabilityTier[]) {
      for (const category of getProductCategoryOrder()) {
        groups[tier][category].sort((a, b) => a.displayName.localeCompare(b.displayName, 'pt-BR'));
      }
    }

    return {
      groups,
      coreCount: Object.values(groups.Core).reduce((total, items) => total + items.length, 0),
      labsCount: Object.values(groups.Labs).reduce((total, items) => total + items.length, 0),
      totalMatches: filtered.length,
      totalLabs: nodeDefinitions.filter((definition) => getNodeCapabilityTier(definition) === 'Labs').length,
    };
  }, [nodeDefinitions, normalizedFilter]);

  const onDragStart = (event: DragEvent, typeId: string) => {
    event.dataTransfer.setData('application/ajudante-node', typeId);
    event.dataTransfer.effectAllowed = 'move';
  };

  const renderCategoryGroup = (
    tier: ProductCapabilityTier,
    category: ProductNodeCategory,
    items: (typeof nodeDefinitions),
  ) => {
    if (items.length === 0) return null;

    return (
      <div key={`${tier}-${category}`} className="node-palette__group">
        <div
          className="node-palette__group-header"
          style={{ borderLeftColor: getProductCategoryColor(category) }}
        >
          <span className="node-palette__group-icon">{getProductCategoryIcon(category)}</span>
          <span>{productCategoryLabels[category]}</span>
          <span className="node-palette__count">{items.length}</span>
        </div>
        {items.map((definition) => (
          <div
            key={definition.typeId}
            className="node-palette__item"
            draggable
            onDragStart={(event) => onDragStart(event, definition.typeId)}
            title={definition.description}
          >
            <div
              className="node-palette__item-dot"
              style={{ backgroundColor: getProductCategoryColor(category) }}
            />
            <div className="node-palette__item-info">
              <div className="node-palette__item-name">{definition.displayName}</div>
              <div className="node-palette__item-desc">{definition.description}</div>
            </div>
          </div>
        ))}
      </div>
    );
  };

  if (!isPaletteOpen) {
    return (
      <div className="node-palette node-palette--collapsed">
        <button
          className="node-palette__toggle"
          onClick={togglePalette}
          title="Abrir palette"
        >
          &rsaquo;
        </button>
      </div>
    );
  }

  const shouldShowLabsItems = showLabs || isSearching;

  return (
    <div className="node-palette">
      <div className="node-palette__header">
        <span className="node-palette__title">{t('palette.title')}</span>
        <button
          className="node-palette__toggle"
          onClick={togglePalette}
          title="Recolher palette"
        >
          &lsaquo;
        </button>
      </div>

      <div className="node-palette__search">
        <input
          type="text"
          placeholder={t('palette.search')}
          value={filter}
          onChange={(event) => setFilter(event.currentTarget.value)}
          onInput={(event) => setFilter(event.currentTarget.value)}
          className="node-palette__input"
        />
      </div>

      <div className="node-palette__list">
        {grouped.totalMatches === 0 ? (
          <div className="node-palette__empty">
            {t('palette.empty')}
          </div>
        ) : (
          <>
            <section className="node-palette__section" aria-label={capabilityLabels.Core}>
              <div className="node-palette__section-header">
                <span className="node-palette__section-title">{capabilityLabels.Core}</span>
                <span className="node-palette__section-count">{grouped.coreCount}</span>
              </div>
              {getProductCategoryOrder().map((category) => renderCategoryGroup('Core', category, grouped.groups.Core[category]))}
            </section>

            <section className="node-palette__section node-palette__section--labs" aria-label={capabilityLabels.Labs}>
              <button
                type="button"
                className="node-palette__section-header node-palette__section-header--button"
                onClick={() => setShowLabs((value) => !value)}
                aria-expanded={shouldShowLabsItems}
              >
                <span className="node-palette__section-title">{capabilityLabels.Labs}</span>
                <span className="node-palette__section-count">{isSearching ? grouped.labsCount : grouped.totalLabs}</span>
              </button>

              {!shouldShowLabsItems ? (
                <button
                  type="button"
                  className="node-palette__labs-teaser"
                  onClick={() => setShowLabs(true)}
                >
                  {grouped.totalLabs} avancado(s). Expandir Labs.
                </button>
              ) : (
                getProductCategoryOrder().map((category) => renderCategoryGroup('Labs', category, grouped.groups.Labs[category]))
              )}
            </section>
          </>
        )}
      </div>
    </div>
  );
}
