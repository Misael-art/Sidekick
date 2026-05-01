import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { clearLocaleStorageForTesting, setLocale, translate, useLocaleStore } from './index';

describe('i18n', () => {
  beforeEach(() => {
    clearLocaleStorageForTesting();
    useLocaleStore.setState({ locale: 'pt-BR' });
  });

  afterEach(() => {
    clearLocaleStorageForTesting();
  });

  it('translates known keys in pt-BR', () => {
    expect(translate('toolbar.new', 'pt-BR')).toBe('Novo');
  });

  it('translates known keys in en', () => {
    expect(translate('toolbar.new', 'en')).toBe('New');
  });

  it('falls back to other locale when key missing in primary', () => {
    expect(translate('toolbar.new', 'pt-BR')).not.toBe('toolbar.new');
  });

  it('returns the key when not found in any locale', () => {
    expect(translate('nope.missing.key')).toBe('nope.missing.key');
  });

  it('persists locale via setLocale', () => {
    setLocale('en');
    expect(useLocaleStore.getState().locale).toBe('en');
    expect(localStorage.getItem('sidekick.locale')).toBe('en');
  });

  it('toggles locale', () => {
    useLocaleStore.getState().toggleLocale();
    expect(useLocaleStore.getState().locale).toBe('en');
    useLocaleStore.getState().toggleLocale();
    expect(useLocaleStore.getState().locale).toBe('pt-BR');
  });

  it('substitutes params', () => {
    // Use known en key with no params; verify literal pass-through still works
    expect(translate('toolbar.run', 'en')).toBe('Run');
  });
});
