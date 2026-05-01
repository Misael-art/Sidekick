import type { Dictionary } from './types';

export const en: Dictionary = {
  // Toolbar
  'toolbar.new': 'New',
  'toolbar.save': 'Save',
  'toolbar.load': 'Load',
  'toolbar.run': 'Run',
  'toolbar.stop': 'Stop',
  'toolbar.mira': 'Inspect Element',
  'toolbar.snip': 'Capture Region',
  'toolbar.flowName': 'Flow name',
  'toolbar.untitled': 'Untitled flow',
  'toolbar.language': 'Language',
  'toolbar.languageSwitch': 'Switch language',

  // Palette
  'palette.title': 'Nodes',
  'palette.search': 'Search nodes…',
  'palette.empty': 'No nodes match your search.',
  'palette.triggers': 'Triggers',
  'palette.logic': 'Logic',
  'palette.actions': 'Actions',

  // Property panel
  'props.title': 'Properties',
  'props.empty': 'Select a node to edit its properties.',
  'props.identifier': 'Identifier',
  'props.invalidValue': 'Stored value is not in the allowed list. Pick a valid option.',

  // Status bar
  'status.idle': 'Idle',
  'status.running': 'Running',
  'status.completed': 'Completed',
  'status.error': 'Error',
  'status.logs': 'Logs',
  'status.expand': 'Expand logs',
  'status.collapse': 'Collapse logs',
  'status.clear': 'Clear logs',

  // App
  'app.ready': 'Sidekick is ready.',
  'app.loadingDefinitions': 'Loading node catalog…',
  'app.bridgeUnavailable': 'Bridge is not available — running in browser preview.',
};
