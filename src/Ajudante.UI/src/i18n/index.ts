import { create } from 'zustand';
import { en } from './en';
import { ptBR } from './pt-BR';
import type { Dictionary, Locale } from './types';

const STORAGE_KEY = 'sidekick.locale';

const dictionaries: Record<Locale, Dictionary> = {
  'pt-BR': ptBR,
  en,
};

function detectInitialLocale(): Locale {
  try {
    const stored = typeof localStorage !== 'undefined' ? localStorage.getItem(STORAGE_KEY) : null;
    if (stored === 'pt-BR' || stored === 'en') return stored;
  } catch {
    // ignore storage failures
  }
  if (typeof navigator !== 'undefined' && navigator.language?.toLowerCase().startsWith('pt')) {
    return 'pt-BR';
  }
  return 'pt-BR';
}

interface LocaleState {
  locale: Locale;
  setLocale: (locale: Locale) => void;
  toggleLocale: () => void;
}

export const useLocaleStore = create<LocaleState>((set, get) => ({
  locale: detectInitialLocale(),
  setLocale: (locale) => {
    try {
      if (typeof localStorage !== 'undefined') localStorage.setItem(STORAGE_KEY, locale);
    } catch {
      // ignore storage failures
    }
    set({ locale });
  },
  toggleLocale: () => {
    const next: Locale = get().locale === 'pt-BR' ? 'en' : 'pt-BR';
    get().setLocale(next);
  },
}));

export function getLocale(): Locale {
  return useLocaleStore.getState().locale;
}

export function setLocale(locale: Locale): void {
  useLocaleStore.getState().setLocale(locale);
}

export function translate(key: string, locale?: Locale, params?: Record<string, string | number>): string {
  const active = locale ?? getLocale();
  const primary = dictionaries[active];
  const fallbackLocale: Locale = active === 'pt-BR' ? 'en' : 'pt-BR';
  const fallback = dictionaries[fallbackLocale];

  let value = primary[key] ?? fallback[key] ?? key;
  if (params) {
    for (const [k, v] of Object.entries(params)) {
      value = value.replace(new RegExp(`\\{\\{${k}\\}\\}`, 'g'), String(v));
    }
  }
  return value;
}

export function useTranslation() {
  const locale = useLocaleStore((s) => s.locale);
  const t = (key: string, params?: Record<string, string | number>) => translate(key, locale, params);
  return { t, locale };
}

export function clearLocaleStorageForTesting(): void {
  try {
    if (typeof localStorage !== 'undefined') localStorage.removeItem(STORAGE_KEY);
  } catch {
    // ignore
  }
}

export type { Locale, Dictionary };
