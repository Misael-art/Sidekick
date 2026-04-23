import { describe, expect, it, vi } from 'vitest';
import {
  attachBeforeUnloadGuard,
  createBeforeUnloadHandler,
  hasUnsavedChanges,
  setUnsavedChangesFlag,
} from './unsavedChangesGuard';

describe('unsavedChangesGuard', () => {
  it('stores and reads the unsaved changes flag on window', () => {
    setUnsavedChangesFlag(window, true);
    expect(hasUnsavedChanges(window)).toBe(true);

    setUnsavedChangesFlag(window, false);
    expect(hasUnsavedChanges(window)).toBe(false);
  });

  it('blocks beforeunload when there are unsaved changes', () => {
    setUnsavedChangesFlag(window, true);
    const handler = createBeforeUnloadHandler(window);
    const preventDefault = vi.fn();
    const event = {
      preventDefault,
      returnValue: undefined,
    } as unknown as BeforeUnloadEvent;

    handler(event);

    expect(preventDefault).toHaveBeenCalledOnce();
    expect(event.returnValue).toBe('');
  });

  it('does not block beforeunload when there are no unsaved changes', () => {
    setUnsavedChangesFlag(window, false);
    const handler = createBeforeUnloadHandler(window);
    const preventDefault = vi.fn();
    const event = {
      preventDefault,
      returnValue: undefined,
    } as unknown as BeforeUnloadEvent;

    handler(event);

    expect(preventDefault).not.toHaveBeenCalled();
    expect(event.returnValue).toBeUndefined();
  });

  it('registers and unregisters the beforeunload listener', () => {
    const addEventListenerSpy = vi.spyOn(window, 'addEventListener');
    const removeEventListenerSpy = vi.spyOn(window, 'removeEventListener');

    const dispose = attachBeforeUnloadGuard(window);

    expect(addEventListenerSpy).toHaveBeenCalledWith('beforeunload', expect.any(Function));

    const registeredHandler = addEventListenerSpy.mock.calls.find(
      ([eventName]) => eventName === 'beforeunload',
    )?.[1];

    dispose();

    expect(removeEventListenerSpy).toHaveBeenCalledWith('beforeunload', registeredHandler);
  });
});
