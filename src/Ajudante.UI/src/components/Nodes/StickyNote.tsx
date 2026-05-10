import { memo, useState, useCallback, useEffect, useRef, type CSSProperties, type ReactNode } from 'react';
import { NodeResizer, type NodeProps } from '@xyflow/react';
import type { StickyNoteData } from '../../bridge/flowConverter';
import { useFlowStore } from '../../store/flowStore';

const COLORS: Record<string, { bg: string; border: string; text: string }> = {
  yellow: { bg: '#fef3c7', border: '#facc15', text: '#713f12' },
  green: { bg: '#dcfce7', border: '#4ade80', text: '#14532d' },
  blue: { bg: '#dbeafe', border: '#60a5fa', text: '#1e3a8a' },
  pink: { bg: '#fce7f3', border: '#f472b6', text: '#831843' },
  purple: { bg: '#ede9fe', border: '#a78bfa', text: '#4c1d95' },
};

const COLOR_KEYS = Object.keys(COLORS);

function isSafeLocalImagePath(path: string): boolean {
  const normalized = path.trim().replace(/\\/g, '/');
  if (!normalized || normalized.startsWith('http://') || normalized.startsWith('https://') || normalized.startsWith('data:')) {
    return false;
  }

  return normalized.startsWith('./')
    || normalized.startsWith('../')
    || normalized.startsWith('assets/')
    || normalized.startsWith('/assets/');
}

function renderInlineMarkdown(text: string): ReactNode[] {
  const nodes: ReactNode[] = [];
  const pattern = /(\*\*([^*]+)\*\*|\*([^*]+)\*|\[([^\]]+)\]\(([^)]+)\))/g;
  let cursor = 0;
  let match: RegExpExecArray | null;

  while ((match = pattern.exec(text)) !== null) {
    if (match.index > cursor) {
      nodes.push(text.slice(cursor, match.index));
    }

    if (match[2]) {
      nodes.push(<strong key={`strong-${match.index}`}>{match[2]}</strong>);
    } else if (match[3]) {
      nodes.push(<em key={`em-${match.index}`}>{match[3]}</em>);
    } else if (match[4] && match[5]) {
      const href = match[5].trim();
      if (href.startsWith('javascript:')) {
        nodes.push(match[4]);
      } else {
        nodes.push(
          <a key={`link-${match.index}`} href={href} target="_blank" rel="noreferrer">
            {match[4]}
          </a>,
        );
      }
    }
    cursor = match.index + match[0].length;
  }

  if (cursor < text.length) {
    nodes.push(text.slice(cursor));
  }

  return nodes;
}

function renderSafeMarkdown(markdown: string): ReactNode {
  const lines = markdown.split(/\r?\n/);
  const rendered: ReactNode[] = [];

  for (let i = 0; i < lines.length; i += 1) {
    const line = lines[i].trim();
    if (!line) {
      rendered.push(<div key={`gap-${i}`} style={{ height: 4 }} />);
      continue;
    }

    const imageMatch = line.match(/^!\[([^\]]*)\]\(([^)]+)\)$/);
    if (imageMatch) {
      const alt = imageMatch[1];
      const src = imageMatch[2].trim();
      if (isSafeLocalImagePath(src)) {
        rendered.push(<img key={`img-${i}`} src={src} alt={alt} className="sticky-note__markdown-image" />);
      } else {
        rendered.push(<p key={`img-warn-${i}`}>[imagem bloqueada por seguranca]</p>);
      }
      continue;
    }

    if (line.startsWith('- ') || line.startsWith('* ')) {
      const items: ReactNode[] = [];
      let j = i;
      while (j < lines.length) {
        const itemLine = lines[j].trim();
        if (!(itemLine.startsWith('- ') || itemLine.startsWith('* '))) {
          break;
        }
        items.push(<li key={`li-${j}`}>{renderInlineMarkdown(itemLine.slice(2))}</li>);
        j += 1;
      }
      rendered.push(<ul key={`ul-${i}`}>{items}</ul>);
      i = j - 1;
      continue;
    }

    rendered.push(<p key={`p-${i}`}>{renderInlineMarkdown(line)}</p>);
  }

  return <div className="sticky-note__markdown">{rendered}</div>;
}

function StickyNoteImpl({ id, data, selected }: NodeProps) {
  const sticky = data as StickyNoteData;
  const updateSticky = useFlowStore((s) => s.updateStickyNote);
  const removeSticky = useFlowStore((s) => s.removeStickyNote);

  const [editingTitle, setEditingTitle] = useState(false);
  const [editingBody, setEditingBody] = useState(false);
  const [draftTitle, setDraftTitle] = useState(sticky.title);
  const [draftBody, setDraftBody] = useState(sticky.body);
  const [draftFormat, setDraftFormat] = useState<'plain' | 'markdown'>(sticky.contentFormat === 'markdown' ? 'markdown' : 'plain');
  const titleRef = useRef<HTMLInputElement>(null);
  const bodyRef = useRef<HTMLTextAreaElement>(null);

  useEffect(() => setDraftTitle(sticky.title), [sticky.title]);
  useEffect(() => setDraftBody(sticky.body), [sticky.body]);
  useEffect(() => setDraftFormat(sticky.contentFormat === 'markdown' ? 'markdown' : 'plain'), [sticky.contentFormat]);

  useEffect(() => {
    if (editingTitle) titleRef.current?.focus();
  }, [editingTitle]);
  useEffect(() => {
    if (editingBody) bodyRef.current?.focus();
  }, [editingBody]);

  const palette = COLORS[sticky.color] ?? COLORS.yellow;

  const commitTitle = useCallback(() => {
    if (draftTitle !== sticky.title) updateSticky(id, { title: draftTitle });
    setEditingTitle(false);
  }, [draftTitle, sticky.title, updateSticky, id]);

  const commitBody = useCallback(() => {
    const currentFormat = sticky.contentFormat === 'markdown' ? 'markdown' : 'plain';
    if (draftBody !== sticky.body || draftFormat !== currentFormat) {
      updateSticky(id, { body: draftBody, contentFormat: draftFormat });
    }
    setEditingBody(false);
  }, [draftBody, draftFormat, sticky.body, sticky.contentFormat, updateSticky, id]);

  const cycleColor = useCallback(() => {
    const idx = COLOR_KEYS.indexOf(sticky.color);
    const next = COLOR_KEYS[(idx + 1) % COLOR_KEYS.length];
    updateSticky(id, { color: next });
  }, [sticky.color, updateSticky, id]);

  const style: CSSProperties = {
    width: sticky.width,
    height: sticky.height,
    background: palette.bg,
    border: `2px solid ${palette.border}`,
    borderRadius: 8,
    padding: 10,
    boxShadow: selected ? '0 0 0 2px #2563eb, 0 4px 12px rgba(0,0,0,0.18)' : '0 4px 12px rgba(0,0,0,0.12)',
    color: palette.text,
    display: 'flex',
    flexDirection: 'column',
    gap: 6,
    fontFamily: 'inherit',
  };

  return (
    <>
      <NodeResizer
        color={palette.border}
        isVisible={selected}
        minWidth={160}
        minHeight={120}
        onResize={(_, params) => updateSticky(id, { width: params.width, height: params.height })}
      />
      <div className="sticky-note" style={style} data-color={sticky.color}>
        <div className="sticky-note__header" style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          {editingTitle ? (
            <input
              ref={titleRef}
              value={draftTitle}
              onChange={(e) => setDraftTitle(e.target.value)}
              onBlur={commitTitle}
              onKeyDown={(e) => {
                if (e.key === 'Enter') commitTitle();
                if (e.key === 'Escape') {
                  setDraftTitle(sticky.title);
                  setEditingTitle(false);
                }
              }}
              placeholder="Por quê este passo?"
              className="sticky-note__title-input"
              style={{
                flex: 1,
                background: 'transparent',
                border: 'none',
                outline: 'none',
                fontWeight: 700,
                fontSize: 14,
                color: palette.text,
              }}
            />
          ) : (
            <button
              type="button"
              onClick={() => setEditingTitle(true)}
              className="sticky-note__title"
              style={{
                flex: 1,
                background: 'transparent',
                border: 'none',
                cursor: 'text',
                textAlign: 'left',
                fontWeight: 700,
                fontSize: 14,
                color: palette.text,
                padding: 0,
              }}
            >
              {sticky.title || <span style={{ opacity: 0.55 }}>Por quê este passo?</span>}
            </button>
          )}
          <button
            type="button"
            onClick={cycleColor}
            title="Trocar cor"
            aria-label="Trocar cor"
            className="sticky-note__color-btn"
            style={{
              width: 18,
              height: 18,
              borderRadius: 9,
              background: palette.border,
              border: 'none',
              cursor: 'pointer',
              padding: 0,
            }}
          />
          <button
            type="button"
            onClick={() => {
              const next = draftFormat === 'plain' ? 'markdown' : 'plain';
              setDraftFormat(next);
              updateSticky(id, { contentFormat: next });
            }}
            title={draftFormat === 'markdown' ? 'Modo markdown ativo' : 'Modo texto simples ativo'}
            aria-label="Alternar formato da nota"
            className="sticky-note__format-btn"
            style={{
              border: 'none',
              background: 'transparent',
              cursor: 'pointer',
              color: palette.text,
              opacity: 0.85,
              fontSize: 11,
              fontWeight: 700,
            }}
          >
            {draftFormat === 'markdown' ? 'MD' : 'TXT'}
          </button>
          <button
            type="button"
            onClick={() => removeSticky(id)}
            title="Remover nota"
            aria-label="Remover nota"
            className="sticky-note__remove"
            style={{
              width: 18,
              height: 18,
              borderRadius: 4,
              background: 'transparent',
              border: 'none',
              cursor: 'pointer',
              color: palette.text,
              fontWeight: 700,
              padding: 0,
              opacity: 0.6,
            }}
          >
            x
          </button>
        </div>
        {editingBody ? (
          <textarea
            ref={bodyRef}
            value={draftBody}
            onChange={(e) => setDraftBody(e.target.value)}
            onBlur={commitBody}
            onKeyDown={(e) => {
              if (e.key === 'Escape') {
                setDraftBody(sticky.body);
                setEditingBody(false);
              }
            }}
            placeholder={draftFormat === 'markdown' ? 'Use markdown seguro: **negrito**, *italico*, [link](url), ![img](./assets/x.png)' : 'Resultado esperado, contexto, dica...'}
            className="sticky-note__body-input nodrag"
            style={{
              flex: 1,
              background: 'transparent',
              border: 'none',
              outline: 'none',
              resize: 'none',
              fontSize: 12,
              lineHeight: 1.45,
              color: palette.text,
              fontFamily: 'inherit',
            }}
          />
        ) : (
          <button
            type="button"
            onClick={() => setEditingBody(true)}
            className="sticky-note__body"
            style={{
              flex: 1,
              background: 'transparent',
              border: 'none',
              cursor: 'text',
              textAlign: 'left',
              fontSize: 12,
              lineHeight: 1.45,
              color: palette.text,
              whiteSpace: 'pre-wrap',
              overflow: 'auto',
              padding: 0,
            }}
          >
            {sticky.body
              ? (sticky.contentFormat === 'markdown' ? renderSafeMarkdown(sticky.body) : sticky.body)
              : <span style={{ opacity: 0.55 }}>Resultado esperado, contexto, dica...</span>}
          </button>
        )}
      </div>
    </>
  );
}

export default memo(StickyNoteImpl);
