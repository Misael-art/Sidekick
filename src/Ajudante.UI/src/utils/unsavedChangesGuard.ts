const UNSAVED_CHANGES_FLAG = '__sidekickHasUnsavedChanges';

export interface UnsavedChangesWindow extends Window {
  __sidekickHasUnsavedChanges?: boolean;
}

export function setUnsavedChangesFlag(
  targetWindow: UnsavedChangesWindow,
  hasUnsavedChanges: boolean,
): void {
  targetWindow[UNSAVED_CHANGES_FLAG] = hasUnsavedChanges;
}

export function hasUnsavedChanges(targetWindow: UnsavedChangesWindow): boolean {
  return targetWindow[UNSAVED_CHANGES_FLAG] === true;
}

export function createBeforeUnloadHandler(targetWindow: UnsavedChangesWindow) {
  return (event: BeforeUnloadEvent): void => {
    if (!hasUnsavedChanges(targetWindow)) {
      return;
    }

    event.preventDefault();
    event.returnValue = '';
  };
}

export function attachBeforeUnloadGuard(targetWindow: UnsavedChangesWindow): () => void {
  const handler = createBeforeUnloadHandler(targetWindow);
  targetWindow.addEventListener('beforeunload', handler);
  return () => {
    targetWindow.removeEventListener('beforeunload', handler);
  };
}
