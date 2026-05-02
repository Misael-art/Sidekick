import type { NodeDefinition } from './bridge/types';

/** Dev-mode sample definitions so the palette stays usable without the desktop host.
 *  Keep these aligned with the real backend registry TypeIds. */
export function getDevNodeDefinitions(): NodeDefinition[] {
  const flowIn = { id: 'in', name: 'In', dataType: 'Flow' as const };
  const flowOut = { id: 'out', name: 'Out', dataType: 'Flow' as const };
  const notFoundOut = { id: 'notFound', name: 'Not Found', dataType: 'Flow' as const };
  const triggeredOut = { id: 'triggered', name: 'Triggered', dataType: 'Flow' as const };

  const define = (
    typeId: string,
    displayName: string,
    category: NodeDefinition['category'],
    description: string,
    properties: NodeDefinition['properties'] = [],
    outputPorts: NodeDefinition['outputPorts'] = [flowOut],
    inputPorts: NodeDefinition['inputPorts'] = category === 'Trigger' ? [] : [flowIn],
  ): NodeDefinition => ({
    typeId,
    displayName,
    category,
    description,
    color: category === 'Trigger' ? '#EF4444' : category === 'Logic' ? '#EAB308' : '#22C55E',
    inputPorts,
    outputPorts,
    properties,
  });

  const selectorProps = (defaultControlType = ''): NodeDefinition['properties'] => [
    { id: 'windowTitle', name: 'Window Title', type: 'String', defaultValue: '' },
    { id: 'windowTitleMatch', name: 'Window Title Match', type: 'Dropdown', defaultValue: 'equals', options: ['equals', 'contains', 'regex'] },
    { id: 'processName', name: 'Process Name', type: 'String', defaultValue: '' },
    { id: 'processPath', name: 'Process Path', type: 'FilePath', defaultValue: '' },
    { id: 'automationId', name: 'Automation ID', type: 'String', defaultValue: '' },
    { id: 'elementName', name: 'Element Name', type: 'String', defaultValue: '' },
    { id: 'controlType', name: 'Control Type', type: 'String', defaultValue: defaultControlType },
    { id: 'timeoutMs', name: 'Timeout (ms)', type: 'Integer', defaultValue: 5000 },
  ];

  const triggerGuardProps = (): NodeDefinition['properties'] => [
    { id: 'intervalMs', name: 'Poll Interval (ms)', type: 'Integer', defaultValue: 1000 },
    { id: 'cooldownMs', name: 'Cooldown (ms)', type: 'Integer', defaultValue: 5000 },
    { id: 'debounceMs', name: 'Debounce (ms)', type: 'Integer', defaultValue: 250 },
    { id: 'maxRepeat', name: 'Max Repeat', type: 'Integer', defaultValue: 0 },
  ];

  const overlayProps = (): NodeDefinition['properties'] => [
    { id: 'durationMs', name: 'Timer (ms)', type: 'Integer', defaultValue: 1000 },
    { id: 'waitForClose', name: 'Wait For Timer', type: 'Boolean', defaultValue: true },
    { id: 'plane', name: 'Plane', type: 'Dropdown', defaultValue: 'foreground', options: ['foreground', 'normal'] },
    { id: 'fullScreen', name: 'Full Screen', type: 'Boolean', defaultValue: true },
    { id: 'x', name: 'X', type: 'Integer', defaultValue: 0 },
    { id: 'y', name: 'Y', type: 'Integer', defaultValue: 0 },
    { id: 'width', name: 'Width', type: 'Integer', defaultValue: 640 },
    { id: 'height', name: 'Height', type: 'Integer', defaultValue: 360 },
    { id: 'opacity', name: 'Opacity', type: 'Float', defaultValue: 0.9 },
    { id: 'clickThrough', name: 'Click Through', type: 'Boolean', defaultValue: true },
    { id: 'motion', name: 'Motion', type: 'Dropdown', defaultValue: 'none', options: ['none', 'slideUp', 'slideDown', 'slideLeft', 'slideRight'] },
    { id: 'fadeInMs', name: 'Fade In (ms)', type: 'Integer', defaultValue: 120 },
    { id: 'fadeOutMs', name: 'Fade Out (ms)', type: 'Integer', defaultValue: 120 },
  ];

  const overlayOut = [
    flowOut,
    { id: 'overlayKind', name: 'Overlay Kind', dataType: 'String' as const },
    { id: 'durationMs', name: 'Duration (ms)', dataType: 'Number' as const },
  ];
  const keyboardKeys = [
    'Return', 'Tab', 'Escape', 'Space', 'Backspace', 'Delete',
    'Up', 'Down', 'Left', 'Right',
    'F1', 'F2', 'F3', 'F4', 'F5', 'F6', 'F7', 'F8', 'F9', 'F10', 'F11', 'F12',
    'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M',
    'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
  ];

  return [
    define('trigger.manualStart', 'Start Manual', 'Trigger', 'Explicit flow entry point for manually started automations', [], [triggeredOut], []),
    define('trigger.hotkey', 'Hotkey Trigger', 'Trigger', 'Starts the flow when a hotkey is pressed', [
      { id: 'key', name: 'Key', type: 'String', defaultValue: 'F9' },
      { id: 'modifiers', name: 'Modifiers', type: 'String', defaultValue: 'Ctrl' },
    ], [triggeredOut], []),
    define('trigger.filesystem', 'Filesystem', 'Trigger', 'Triggers on file system changes', [
      { id: 'path', name: 'Watch Path', type: 'FolderPath', defaultValue: '' },
      { id: 'filter', name: 'Filter', type: 'String', defaultValue: '*.*' },
      { id: 'eventType', name: 'Event Type', type: 'Dropdown', defaultValue: 'Created', options: ['Created', 'Changed', 'Deleted'] },
    ], [triggeredOut], []),
    define('trigger.windowEvent', 'Window Event', 'Trigger', 'Triggers on a window event (open, close, focus)', [
      { id: 'windowTitle', name: 'Window Title', type: 'String', defaultValue: '' },
      { id: 'eventType', name: 'Event Type', type: 'Dropdown', defaultValue: 'Opened', options: ['Opened', 'Closed', 'Focused'] },
    ], [triggeredOut], []),
    define('trigger.imageDetected', 'Image Detected', 'Trigger', 'Triggers when an image is detected on screen', [
      { id: 'templatePath', name: 'Template Image', type: 'ImageTemplate', defaultValue: '' },
      { id: 'confidence', name: 'Confidence', type: 'Float', defaultValue: 0.8 },
    ], [triggeredOut, { id: 'location', name: 'Location', dataType: 'Point' }], []),
    define('trigger.pixelChange', 'Pixel Change', 'Trigger', 'Triggers when pixels change in a region', [
      { id: 'region', name: 'Region', type: 'Point', defaultValue: '' },
      { id: 'threshold', name: 'Threshold', type: 'Float', defaultValue: 0.1 },
    ], [triggeredOut], []),
    define('trigger.desktopElementAppeared', 'Desktop Element Appeared', 'Trigger', 'Triggers when a desktop UI element appears', [
      ...selectorProps('button'),
      ...triggerGuardProps(),
    ], [triggeredOut, { id: 'found', name: 'Found', dataType: 'Any' }], []),
    define('trigger.desktopElementTextChanged', 'Desktop Text Changed', 'Trigger', 'Triggers when text on a desktop element changes', [
      ...selectorProps('text'),
      { id: 'intervalMs', name: 'Poll Interval (ms)', type: 'Integer', defaultValue: 1000 },
      { id: 'cooldownMs', name: 'Cooldown (ms)', type: 'Integer', defaultValue: 1000 },
      { id: 'fireInitial', name: 'Fire Initial Value', type: 'Boolean', defaultValue: false },
    ], [
      triggeredOut,
      { id: 'oldText', name: 'Old Text', dataType: 'String' },
      { id: 'newText', name: 'New Text', dataType: 'String' },
    ], []),
    define('trigger.scheduleTime', 'Schedule Time', 'Trigger', 'Triggers once per day at a local time', [
      { id: 'timeOfDay', name: 'Time of Day', type: 'String', defaultValue: '09:00' },
      { id: 'pollIntervalMs', name: 'Poll Interval (ms)', type: 'Integer', defaultValue: 1000 },
    ], [triggeredOut], []),
    define('trigger.interval', 'Interval', 'Trigger', 'Triggers repeatedly at a fixed interval', [
      { id: 'intervalMs', name: 'Interval (ms)', type: 'Integer', defaultValue: 60000 },
      { id: 'fireImmediately', name: 'Fire Immediately', type: 'Boolean', defaultValue: false },
      { id: 'maxRepeat', name: 'Max Repeat', type: 'Integer', defaultValue: 0 },
    ], [triggeredOut], []),
    define('trigger.processEvent', 'Process Event', 'Trigger', 'Triggers when a process starts or stops', [
      { id: 'processName', name: 'Process Name', type: 'String', defaultValue: '' },
      { id: 'processPath', name: 'Process Path', type: 'FilePath', defaultValue: '' },
      { id: 'eventType', name: 'Event Type', type: 'Dropdown', defaultValue: 'started', options: ['started', 'stopped'] },
      { id: 'intervalMs', name: 'Poll Interval (ms)', type: 'Integer', defaultValue: 1000 },
      { id: 'cooldownMs', name: 'Cooldown (ms)', type: 'Integer', defaultValue: 1000 },
    ], [triggeredOut], []),
    define('logic.ifElse', 'If / Else', 'Logic', 'Branch based on a condition', [
      { id: 'left', name: 'Left Value', type: 'String', defaultValue: '' },
      { id: 'operator', name: 'Operator', type: 'Dropdown', defaultValue: 'equals', options: ['equals', 'notEquals', 'contains', 'greaterThan', 'lessThan'] },
      { id: 'right', name: 'Right Value', type: 'String', defaultValue: '' },
    ], [
      { id: 'true', name: 'True', dataType: 'Flow' },
      { id: 'false', name: 'False', dataType: 'Flow' },
    ]),
    define('logic.delay', 'Delay', 'Logic', 'Wait for a specified duration', [
      { id: 'delayMs', name: 'Delay (ms)', type: 'Integer', defaultValue: 1000 },
    ]),
    define('logic.compareText', 'Compare Text', 'Logic', 'Compare two text values', [
      { id: 'left', name: 'Left Text', type: 'String', defaultValue: '' },
      { id: 'right', name: 'Right Text', type: 'String', defaultValue: '' },
      { id: 'caseSensitive', name: 'Case Sensitive', type: 'Boolean', defaultValue: false },
    ], [
      { id: 'match', name: 'Match', dataType: 'Flow' },
      { id: 'noMatch', name: 'No Match', dataType: 'Flow' },
    ]),
    define('logic.loop', 'Loop', 'Logic', 'Repeat a section a set number of times', [
      { id: 'count', name: 'Count', type: 'Integer', defaultValue: 10 },
    ], [
      { id: 'body', name: 'Body', dataType: 'Flow' },
      { id: 'done', name: 'Done', dataType: 'Flow' },
      { id: 'index', name: 'Index', dataType: 'Number' },
    ]),
    define('logic.setVariable', 'Set Variable', 'Logic', 'Set a flow variable value', [
      { id: 'variableName', name: 'Variable Name', type: 'String', defaultValue: '' },
      { id: 'value', name: 'Value', type: 'String', defaultValue: '' },
    ]),
    define('logic.getVariable', 'Get Variable', 'Logic', 'Read a flow variable value', [
      { id: 'variableName', name: 'Variable Name', type: 'String', defaultValue: '' },
    ], [
      flowOut,
      { id: 'value', name: 'Value', dataType: 'Any' },
    ]),
    define('logic.cooldown', 'Cooldown', 'Logic', 'Routes execution through cooldown when a run is too soon', [
      { id: 'key', name: 'Key', type: 'String', defaultValue: 'default' },
      { id: 'cooldownMs', name: 'Cooldown (ms)', type: 'Integer', defaultValue: 5000 },
    ], [
      { id: 'passthrough', name: 'Pass', dataType: 'Flow' },
      { id: 'cooldown', name: 'Cooldown', dataType: 'Flow' },
      { id: 'remainingMs', name: 'Remaining (ms)', dataType: 'Number' },
    ]),
    define('logic.textTemplate', 'Text Template', 'Logic', 'Builds text from variables and node outputs', [
      { id: 'template', name: 'Template', type: 'String', defaultValue: '' },
      { id: 'storeInVariable', name: 'Store In Variable', type: 'String', defaultValue: '' },
    ], [
      flowOut,
      { id: 'text', name: 'Text', dataType: 'String' },
    ]),
    define('logic.filterTextLines', 'Filter Text Lines', 'Logic', 'Filters text lines using a keyword or mode', [
      { id: 'input', name: 'Input', type: 'String', defaultValue: '' },
      { id: 'keyword', name: 'Keyword', type: 'String', defaultValue: '' },
      { id: 'mode', name: 'Mode', type: 'Dropdown', defaultValue: 'contains', options: ['contains', 'notContains'] },
    ], [
      flowOut,
      { id: 'text', name: 'Text', dataType: 'String' },
    ]),
    define('logic.retryFlow', 'Retry Flow', 'Logic', 'Counts retry attempts and routes to retry or give up', [
      { id: 'counterVariable', name: 'Counter Variable', type: 'String', defaultValue: 'retryCount' },
      { id: 'maxAttempts', name: 'Max Attempts', type: 'Integer', defaultValue: 3 },
      { id: 'delayMs', name: 'Delay (ms)', type: 'Integer', defaultValue: 0 },
    ], [
      { id: 'retry', name: 'Retry', dataType: 'Flow' },
      { id: 'giveUp', name: 'Give Up', dataType: 'Flow' },
      { id: 'attempt', name: 'Attempt', dataType: 'Number' },
    ]),
    define('logic.conditionGroup', 'Condition Group', 'Logic', 'Evaluates nested ANY/ALL condition groups with equals/contains/regex/greater/less/exists/changed operators', [
      { id: 'mode', name: 'Mode', type: 'Dropdown', defaultValue: 'ALL', options: ['ALL', 'ANY'] },
      { id: 'conditionsJson', name: 'Conditions JSON', type: 'String', defaultValue: '[]' },
      { id: 'storeInVariable', name: 'Store In Variable', type: 'String', defaultValue: '' },
    ], [
      { id: 'true', name: 'True', dataType: 'Flow' },
      { id: 'false', name: 'False', dataType: 'Flow' },
      { id: 'result', name: 'Result', dataType: 'Boolean' },
    ]),
    define('action.logMessage', 'Log', 'Action', 'Writes a message to node outputs and optionally to a variable', [
      { id: 'message', name: 'Message', type: 'String', defaultValue: '' },
      { id: 'level', name: 'Level', type: 'Dropdown', defaultValue: 'info', options: ['info', 'warning', 'error', 'debug'] },
      { id: 'storeInVariable', name: 'Store In Variable', type: 'String', defaultValue: '' },
    ], [
      flowOut,
      { id: 'message', name: 'Message', dataType: 'String' },
    ]),
    define('action.readClipboard', 'Read Clipboard', 'Action', 'Reads text from the clipboard', [
      { id: 'storeInVariable', name: 'Store In Variable', type: 'String', defaultValue: '' },
    ], [
      flowOut,
      { id: 'text', name: 'Text', dataType: 'String' },
    ]),
    define('action.writeClipboard', 'Write Clipboard', 'Action', 'Writes text to the clipboard', [
      { id: 'text', name: 'Text', type: 'String', defaultValue: '' },
    ]),
    define('action.readFile', 'Read File', 'Action', 'Reads text content from a file', [
      { id: 'filePath', name: 'File Path', type: 'FilePath', defaultValue: '' },
      { id: 'storeInVariable', name: 'Store In Variable', type: 'String', defaultValue: '' },
    ], [
      flowOut,
      { id: 'content', name: 'Content', dataType: 'String' },
      { id: 'filePath', name: 'File Path', dataType: 'String' },
    ]),
    define('action.writeFile', 'Write File', 'Action', 'Writes text content to a file', [
      { id: 'filePath', name: 'File Path', type: 'FilePath', defaultValue: '' },
      { id: 'content', name: 'Content', type: 'String', defaultValue: '' },
      { id: 'append', name: 'Append', type: 'Boolean', defaultValue: false },
    ], [
      flowOut,
      { id: 'filePath', name: 'File Path', dataType: 'String' },
    ]),
    define('action.listFiles', 'List Files', 'Action', 'Lists files in a directory', [
      { id: 'folderPath', name: 'Folder Path', type: 'FolderPath', defaultValue: '' },
      { id: 'searchPattern', name: 'Search Pattern', type: 'String', defaultValue: '*.*' },
      { id: 'recursive', name: 'Recursive', type: 'Boolean', defaultValue: false },
      { id: 'storeInVariable', name: 'Store In Variable', type: 'String', defaultValue: '' },
    ], [
      flowOut,
      { id: 'files', name: 'Files', dataType: 'Any' },
    ]),
    define('action.moveFile', 'Move File', 'Action', 'Moves or renames a file', [
      { id: 'sourcePath', name: 'Source Path', type: 'FilePath', defaultValue: '' },
      { id: 'destinationPath', name: 'Destination Path', type: 'FilePath', defaultValue: '' },
      { id: 'overwrite', name: 'Overwrite', type: 'Boolean', defaultValue: false },
    ]),
    define('action.jsonExtract', 'JSON Extract', 'Action', 'Extracts a value from JSON using a path', [
      { id: 'json', name: 'JSON', type: 'String', defaultValue: '' },
      { id: 'path', name: 'Path', type: 'String', defaultValue: '$' },
      { id: 'storeInVariable', name: 'Store In Variable', type: 'String', defaultValue: '' },
    ], [
      flowOut,
      { id: 'value', name: 'Value', dataType: 'Any' },
    ]),
    define('action.readCsv', 'Read CSV', 'Action', 'Reads rows from a CSV file', [
      { id: 'filePath', name: 'File Path', type: 'FilePath', defaultValue: '' },
      { id: 'hasHeaders', name: 'Has Headers', type: 'Boolean', defaultValue: true },
      { id: 'delimiter', name: 'Delimiter', type: 'String', defaultValue: ',' },
      { id: 'storeInVariable', name: 'Store In Variable', type: 'String', defaultValue: '' },
    ], [
      flowOut,
      { id: 'rows', name: 'Rows', dataType: 'Any' },
    ]),
    define('action.writeCsv', 'Write CSV', 'Action', 'Writes rows to a CSV file', [
      { id: 'filePath', name: 'File Path', type: 'FilePath', defaultValue: '' },
      { id: 'rowsJson', name: 'Rows JSON', type: 'String', defaultValue: '[]' },
      { id: 'delimiter', name: 'Delimiter', type: 'String', defaultValue: ',' },
      { id: 'includeHeaders', name: 'Include Headers', type: 'Boolean', defaultValue: true },
    ]),
    define('action.httpRequest', 'HTTP Request', 'Action', 'Makes an HTTP request and captures the response', [
      { id: 'url', name: 'URL', type: 'String', defaultValue: '' },
      { id: 'method', name: 'Method', type: 'Dropdown', defaultValue: 'GET', options: ['GET', 'POST', 'PUT', 'DELETE'] },
      { id: 'headersJson', name: 'Headers JSON', type: 'String', defaultValue: '{}' },
      { id: 'body', name: 'Body', type: 'String', defaultValue: '' },
      { id: 'storeBodyInVariable', name: 'Store Body In Variable', type: 'String', defaultValue: '' },
    ], [
      { id: 'success', name: 'Success', dataType: 'Flow' },
      { id: 'error', name: 'Error', dataType: 'Flow' },
      { id: 'body', name: 'Body', dataType: 'String' },
      { id: 'statusCode', name: 'Status Code', dataType: 'Number' },
    ]),
    define('action.mouseClick', 'Mouse Click', 'Action', 'Click at a screen position', [
      { id: 'x', name: 'X', type: 'Integer', defaultValue: 0 },
      { id: 'y', name: 'Y', type: 'Integer', defaultValue: 0 },
      { id: 'button', name: 'Button', type: 'Dropdown', defaultValue: 'Left', options: ['Left', 'Right', 'Middle'] },
      { id: 'clickType', name: 'Click Type', type: 'Dropdown', defaultValue: 'Single', options: ['Single', 'Double'] },
    ]),
    define('action.mouseMove', 'Mouse Move', 'Action', 'Move the mouse to a position', [
      { id: 'x', name: 'X', type: 'Integer', defaultValue: 0 },
      { id: 'y', name: 'Y', type: 'Integer', defaultValue: 0 },
      { id: 'duration', name: 'Duration (ms)', type: 'Integer', defaultValue: 0 },
    ]),
    define('action.mouseDrag', 'Mouse Drag', 'Action', 'Drag from one position to another', [
      { id: 'fromX', name: 'From X', type: 'Integer', defaultValue: 0 },
      { id: 'fromY', name: 'From Y', type: 'Integer', defaultValue: 0 },
      { id: 'toX', name: 'To X', type: 'Integer', defaultValue: 0 },
      { id: 'toY', name: 'To Y', type: 'Integer', defaultValue: 0 },
      { id: 'duration', name: 'Duration (ms)', type: 'Integer', defaultValue: 200 },
    ]),
    define('action.keyboardType', 'Keyboard Type', 'Action', 'Types text via keyboard', [
      { id: 'text', name: 'Text', type: 'String', defaultValue: '' },
      { id: 'delayPerChar', name: 'Delay per Char (ms)', type: 'Integer', defaultValue: 50 },
    ]),
    define('action.keyboardPress', 'Keyboard Press', 'Action', 'Send a keyboard shortcut or key press', [
      { id: 'key', name: 'Key', type: 'Dropdown', defaultValue: 'Return', options: keyboardKeys },
      { id: 'modifiers', name: 'Modifiers', type: 'Dropdown', defaultValue: 'None', options: ['None', 'Ctrl', 'Shift', 'Alt'] },
    ]),
    define('action.openProgram', 'Open Program', 'Action', 'Launch an external program', [
      { id: 'path', name: 'Program Path', type: 'FilePath', defaultValue: '' },
      { id: 'args', name: 'Arguments', type: 'String', defaultValue: '' },
    ]),
    define('action.killProcess', 'Kill Process', 'Action', 'Terminate a running process', [
      { id: 'processName', name: 'Process Name', type: 'String', defaultValue: '' },
    ]),
    define('action.playSound', 'Play Sound', 'Action', 'Play an audio file', [
      { id: 'soundFile', name: 'Sound File', type: 'FilePath', defaultValue: '' },
      { id: 'systemSound', name: 'System Sound', type: 'Dropdown', defaultValue: 'exclamation', options: ['exclamation', 'asterisk', 'beep', 'hand', 'question'] },
    ]),
    define('action.deleteFile', 'Delete File', 'Action', 'Delete a file from disk', [
      { id: 'filePath', name: 'File Path', type: 'FilePath', defaultValue: '' },
    ]),
    define('action.desktopWaitElement', 'Desktop Wait Element', 'Action', 'Waits for a desktop UI element with process-aware selectors', [
      ...selectorProps(),
    ], [
      flowOut,
      notFoundOut,
      { id: 'found', name: 'Found', dataType: 'Any' },
    ]),
    define('action.desktopClickElement', 'Desktop Click Element', 'Action', 'Clicks a desktop UI element, using coordinates only as fallback', [
      ...selectorProps('button'),
      { id: 'clickType', name: 'Click Type', type: 'Dropdown', defaultValue: 'single', options: ['single', 'double'] },
    ], [
      flowOut,
      notFoundOut,
      { id: 'clickedName', name: 'Clicked Name', dataType: 'String' },
      { id: 'fallbackUsed', name: 'Fallback Used', dataType: 'Boolean' },
    ]),
    define('action.desktopReadElementText', 'Desktop Read Element Text', 'Action', 'Reads text from a desktop UI element', [
      ...selectorProps('text'),
      { id: 'storeInVariable', name: 'Store In Variable', type: 'String', defaultValue: '' },
    ], [
      flowOut,
      notFoundOut,
      { id: 'text', name: 'Text', dataType: 'String' },
    ]),
    define('action.captureScreenshot', 'Capture Screenshot', 'Action', 'Captures desktop screenshot by monitor, region, active window, or selector target', [
      { id: 'target', name: 'Target', type: 'Dropdown', defaultValue: 'fullDesktop', options: ['fullDesktop', 'monitor', 'region', 'activeWindow', 'windowSelector'] },
      { id: 'outputPath', name: 'Output Path', type: 'FilePath', defaultValue: '' },
      { id: 'outputFolder', name: 'Output Folder', type: 'FolderPath', defaultValue: '' },
      { id: 'filenameTemplate', name: 'Filename Template', type: 'String', defaultValue: 'screenshot_{{timestamp}}' },
      { id: 'format', name: 'Format', type: 'Dropdown', defaultValue: 'png', options: ['png', 'jpg', 'bmp'] },
      { id: 'quality', name: 'JPG Quality', type: 'Integer', defaultValue: 90 },
      { id: 'effect', name: 'Effect', type: 'Dropdown', defaultValue: 'none', options: ['none', 'grayscale', 'blur', 'highlightCursor'] },
    ], [
      flowOut,
      { id: 'error', name: 'Error', dataType: 'Flow' },
      { id: 'filePath', name: 'File Path', dataType: 'String' },
      { id: 'width', name: 'Width', dataType: 'Number' },
      { id: 'height', name: 'Height', dataType: 'Number' },
      { id: 'target', name: 'Target', dataType: 'String' },
    ]),
    define('action.recordDesktop', 'Record Desktop', 'Action', 'Records desktop frames via ScreenCapture + Emgu VideoWriter', [
      { id: 'target', name: 'Target', type: 'Dropdown', defaultValue: 'fullDesktop', options: ['fullDesktop', 'monitor', 'region', 'windowSelector'] },
      { id: 'durationMs', name: 'Duration (ms)', type: 'Integer', defaultValue: 5000 },
      { id: 'fps', name: 'FPS', type: 'Integer', defaultValue: 12 },
      { id: 'outputFolder', name: 'Output Folder', type: 'FolderPath', defaultValue: '' },
      { id: 'filenameTemplate', name: 'Filename Template', type: 'String', defaultValue: 'desktop_record_{{timestamp}}' },
      { id: 'codec', name: 'Codec', type: 'String', defaultValue: 'MJPG' },
      { id: 'format', name: 'Format', type: 'Dropdown', defaultValue: 'avi', options: ['avi', 'mp4'] },
    ], [
      flowOut,
      { id: 'error', name: 'Error', dataType: 'Flow' },
      { id: 'filePath', name: 'File Path', dataType: 'String' },
      { id: 'durationMs', name: 'Duration', dataType: 'Number' },
      { id: 'framesWritten', name: 'Frames', dataType: 'Number' },
      { id: 'fps', name: 'FPS', dataType: 'Number' },
    ]),
    define('action.recordCamera', 'Record Camera', 'Action', 'Records camera video via Emgu VideoCapture + VideoWriter', [
      { id: 'cameraIndex', name: 'Camera Index', type: 'Integer', defaultValue: 0 },
      { id: 'cameraNameFilter', name: 'Camera Name Filter', type: 'String', defaultValue: '' },
      { id: 'width', name: 'Width', type: 'Integer', defaultValue: 1280 },
      { id: 'height', name: 'Height', type: 'Integer', defaultValue: 720 },
      { id: 'fps', name: 'FPS', type: 'Integer', defaultValue: 24 },
      { id: 'durationMs', name: 'Duration (ms)', type: 'Integer', defaultValue: 5000 },
      { id: 'outputFolder', name: 'Output Folder', type: 'FolderPath', defaultValue: '' },
      { id: 'filenameTemplate', name: 'Filename Template', type: 'String', defaultValue: 'camera_record_{{timestamp}}' },
      { id: 'overlayTimestamp', name: 'Overlay Timestamp', type: 'Boolean', defaultValue: true },
    ], [
      flowOut,
      { id: 'error', name: 'Error', dataType: 'Flow' },
      { id: 'filePath', name: 'File Path', dataType: 'String' },
      { id: 'cameraName', name: 'Camera Name', dataType: 'String' },
      { id: 'framesWritten', name: 'Frames', dataType: 'Number' },
    ]),
    define('action.clickImageMatch', 'Click Image Match', 'Action', 'Finds an image on screen and clicks the match center', [
      { id: 'templateImage', name: 'Template Image', type: 'ImageTemplate', defaultValue: '' },
      { id: 'templatePath', name: 'Template Path', type: 'FilePath', defaultValue: '' },
      { id: 'threshold', name: 'Threshold', type: 'Float', defaultValue: 0.8 },
      { id: 'timeoutMs', name: 'Timeout (ms)', type: 'Integer', defaultValue: 5000 },
      { id: 'intervalMs', name: 'Poll Interval (ms)', type: 'Integer', defaultValue: 500 },
      { id: 'clickType', name: 'Click Type', type: 'Dropdown', defaultValue: 'single', options: ['single', 'double'] },
    ], [
      flowOut,
      notFoundOut,
      { id: 'x', name: 'X', dataType: 'Number' },
      { id: 'y', name: 'Y', dataType: 'Number' },
      { id: 'confidence', name: 'Confidence', dataType: 'Number' },
    ]),
    define('action.overlayColor', 'Overlay Solid Color', 'Action', 'Shows a customizable foreground color overlay on the screen', [
      { id: 'color', name: 'Color', type: 'Color', defaultValue: '#000000' },
      ...overlayProps(),
    ], overlayOut),
    define('action.overlayImage', 'Overlay Image', 'Action', 'Shows an image overlay with fit, background, motion, timer, and fullscreen controls', [
      { id: 'imagePath', name: 'Image Path', type: 'FilePath', defaultValue: '' },
      { id: 'fit', name: 'Fit', type: 'Dropdown', defaultValue: 'contain', options: ['contain', 'cover', 'stretch', 'none'] },
      { id: 'backgroundColor', name: 'Background', type: 'Color', defaultValue: '#000000' },
      ...overlayProps(),
    ], overlayOut),
    define('action.overlayText', 'Overlay Text', 'Action', 'Shows fully customizable text on top of the desktop', [
      { id: 'text', name: 'Text', type: 'String', defaultValue: 'Sidekick' },
      { id: 'fontFamily', name: 'Font Family', type: 'String', defaultValue: 'Segoe UI' },
      { id: 'fontSize', name: 'Font Size', type: 'Float', defaultValue: 48 },
      { id: 'textColor', name: 'Text Color', type: 'Color', defaultValue: '#FFFFFF' },
      { id: 'backgroundColor', name: 'Background', type: 'Color', defaultValue: '#000000' },
      { id: 'horizontalAlign', name: 'Horizontal Align', type: 'Dropdown', defaultValue: 'center', options: ['left', 'center', 'right', 'stretch'] },
      { id: 'verticalAlign', name: 'Vertical Align', type: 'Dropdown', defaultValue: 'center', options: ['top', 'center', 'bottom', 'stretch'] },
      { id: 'effect', name: 'Text Effect', type: 'Dropdown', defaultValue: 'shadow', options: ['none', 'shadow', 'outline'] },
      ...overlayProps(),
    ], overlayOut),
    define('action.consoleSetDirectory', 'Console Set Directory', 'Action', 'Sets the working directory variable used by console command nodes', [
      { id: 'workingDirectory', name: 'Working Directory', type: 'FolderPath', defaultValue: '' },
      { id: 'variableName', name: 'Variable Name', type: 'String', defaultValue: 'pwd' },
      { id: 'createIfMissing', name: 'Create If Missing', type: 'Boolean', defaultValue: false },
    ], [
      flowOut,
      { id: 'workingDirectory', name: 'Working Directory', dataType: 'String' },
    ]),
    define('action.consoleCommand', 'Console Command', 'Action', 'Runs a command with working directory, shell, timeout, stdout, and stderr control', [
      { id: 'shell', name: 'Shell', type: 'Dropdown', defaultValue: 'direct', options: ['direct', 'cmd', 'powershell'] },
      { id: 'command', name: 'Command', type: 'String', defaultValue: '' },
      { id: 'arguments', name: 'Arguments', type: 'String', defaultValue: '' },
      { id: 'workingDirectory', name: 'Working Directory', type: 'FolderPath', defaultValue: '{{pwd}}' },
      { id: 'timeoutMs', name: 'Timeout (ms)', type: 'Integer', defaultValue: 30000 },
      { id: 'captureOutput', name: 'Capture Output', type: 'Boolean', defaultValue: true },
      { id: 'failOnNonZeroExit', name: 'Fail On Non-zero Exit', type: 'Boolean', defaultValue: true },
      { id: 'storeStdoutInVariable', name: 'Store Stdout In Variable', type: 'String', defaultValue: '' },
      { id: 'storeStderrInVariable', name: 'Store Stderr In Variable', type: 'String', defaultValue: '' },
      { id: 'storeExitCodeInVariable', name: 'Store Exit Code In Variable', type: 'String', defaultValue: '' },
    ], [
      flowOut,
      { id: 'error', name: 'Error', dataType: 'Flow' },
      { id: 'exitCode', name: 'Exit Code', dataType: 'Number' },
      { id: 'stdout', name: 'Stdout', dataType: 'String' },
      { id: 'stderr', name: 'Stderr', dataType: 'String' },
      { id: 'workingDirectory', name: 'Working Directory', dataType: 'String' },
    ]),
    define('action.systemAudio', 'System Audio', 'Action', 'Controls speaker volume and microphone mute state', [
      { id: 'operation', name: 'Operation', type: 'Dropdown', defaultValue: 'increaseOutputVolume', options: ['getState', 'setOutputVolume', 'increaseOutputVolume', 'decreaseOutputVolume', 'muteOutput', 'unmuteOutput', 'toggleOutputMute', 'setMicrophoneVolume', 'muteMicrophone', 'unmuteMicrophone', 'toggleMicrophoneMute'] },
      { id: 'percent', name: 'Percent', type: 'Integer', defaultValue: 50 },
      { id: 'stepPercent', name: 'Step Percent', type: 'Integer', defaultValue: 5 },
      { id: 'storeSummaryInVariable', name: 'Store Summary In Variable', type: 'String', defaultValue: '' },
    ], [
      flowOut,
      { id: 'error', name: 'Error', dataType: 'Flow' },
      { id: 'summary', name: 'Summary', dataType: 'String' },
    ]),
    define('action.hardwareDevice', 'Hardware Device', 'Action', 'Enables, disables, or lists camera, microphone, and Wi-Fi devices', [
      { id: 'operation', name: 'Operation', type: 'Dropdown', defaultValue: 'listDevices', options: ['listDevices', 'enableCamera', 'disableCamera', 'enableMicrophoneDevice', 'disableMicrophoneDevice', 'enableWifi', 'disableWifi'] },
      { id: 'nameFilter', name: 'Name Filter', type: 'String', defaultValue: '' },
      { id: 'allowSystemChanges', name: 'Allow System Changes', type: 'Boolean', defaultValue: false },
    ], [
      flowOut,
      { id: 'error', name: 'Error', dataType: 'Flow' },
      { id: 'summary', name: 'Summary', dataType: 'String' },
    ]),
    define('action.systemPower', 'System Power', 'Action', 'Locks, sleeps, hibernates, restarts, or shuts down the computer with safety guards', [
      { id: 'operation', name: 'Operation', type: 'Dropdown', defaultValue: 'lock', options: ['lock', 'sleep', 'hibernate', 'shutdown', 'restart', 'logoff', 'cancelShutdown'] },
      { id: 'delaySeconds', name: 'Delay Seconds', type: 'Integer', defaultValue: 30 },
      { id: 'forceApps', name: 'Force Apps', type: 'Boolean', defaultValue: false },
      { id: 'safetyPhrase', name: 'Safety Phrase', type: 'String', defaultValue: '' },
    ], [
      flowOut,
      { id: 'error', name: 'Error', dataType: 'Flow' },
      { id: 'summary', name: 'Summary', dataType: 'String' },
    ]),
    define('action.displaySettings', 'Display Settings', 'Action', 'Describes monitors or changes resolution, rotation, and screen layout', [
      { id: 'operation', name: 'Operation', type: 'Dropdown', defaultValue: 'describe', options: ['describe', 'setResolution', 'setOrientation', 'setPosition', 'setResolutionAndLayout'] },
      { id: 'deviceName', name: 'Device Name', type: 'String', defaultValue: '' },
      { id: 'width', name: 'Width', type: 'Integer', defaultValue: 1920 },
      { id: 'height', name: 'Height', type: 'Integer', defaultValue: 1080 },
      { id: 'refreshRate', name: 'Refresh Rate', type: 'Integer', defaultValue: 0 },
      { id: 'orientation', name: 'Orientation', type: 'Dropdown', defaultValue: 'landscape', options: ['landscape', 'portrait', 'landscapeFlipped', 'portraitFlipped'] },
      { id: 'positionX', name: 'Position X', type: 'Integer', defaultValue: 0 },
      { id: 'positionY', name: 'Position Y', type: 'Integer', defaultValue: 0 },
      { id: 'allowSystemChanges', name: 'Allow System Changes', type: 'Boolean', defaultValue: false },
    ], [
      flowOut,
      { id: 'error', name: 'Error', dataType: 'Flow' },
      { id: 'summary', name: 'Summary', dataType: 'String' },
    ]),
    define('action.windowControl', 'Window Control', 'Action', 'Focuses, brings forward, minimizes, maximizes, or restores a desktop window', [
      ...selectorProps(),
      { id: 'operation', name: 'Operation', type: 'Dropdown', defaultValue: 'focus', options: ['focus', 'bringToFront', 'minimize', 'maximize', 'restore', 'close'] },
    ], [
      flowOut,
      notFoundOut,
    ]),
    define('action.waitProcess', 'Wait Process', 'Action', 'Waits for a process to start or stop', [
      { id: 'processName', name: 'Process Name', type: 'String', defaultValue: '' },
      { id: 'processPath', name: 'Process Path', type: 'FilePath', defaultValue: '' },
      { id: 'eventType', name: 'Event Type', type: 'Dropdown', defaultValue: 'started', options: ['started', 'stopped'] },
      { id: 'timeoutMs', name: 'Timeout (ms)', type: 'Integer', defaultValue: 30000 },
      { id: 'intervalMs', name: 'Poll Interval (ms)', type: 'Integer', defaultValue: 1000 },
    ], [
      flowOut,
      { id: 'timeout', name: 'Timeout', dataType: 'Flow' },
      { id: 'processId', name: 'Process ID', dataType: 'Number' },
    ]),
    define('action.browserOpenUrl', 'Browser Open URL', 'Action', 'Opens a URL in the browser', [
      { id: 'url', name: 'URL', type: 'String', defaultValue: '' },
      { id: 'browserPath', name: 'Browser Path', type: 'FilePath', defaultValue: '' },
    ]),
    define('action.browserClick', 'Browser Click', 'Action', 'Clicks a browser element found by locator', [
      { id: 'windowTitle', name: 'Window Title', type: 'String', defaultValue: '' },
      { id: 'automationId', name: 'Automation Id', type: 'String', defaultValue: '' },
      { id: 'elementName', name: 'Element Name', type: 'String', defaultValue: '' },
      { id: 'controlType', name: 'Control Type', type: 'String', defaultValue: '' },
      { id: 'timeoutMs', name: 'Timeout (ms)', type: 'Integer', defaultValue: 5000 },
    ]),
    define('action.browserType', 'Browser Type', 'Action', 'Types text into a browser element', [
      { id: 'windowTitle', name: 'Window Title', type: 'String', defaultValue: '' },
      { id: 'automationId', name: 'Automation Id', type: 'String', defaultValue: '' },
      { id: 'elementName', name: 'Element Name', type: 'String', defaultValue: '' },
      { id: 'controlType', name: 'Control Type', type: 'String', defaultValue: '' },
      { id: 'text', name: 'Text', type: 'String', defaultValue: '' },
      { id: 'timeoutMs', name: 'Timeout (ms)', type: 'Integer', defaultValue: 5000 },
    ]),
    define('action.browserWaitElement', 'Browser Wait Element', 'Action', 'Waits until a browser element is available', [
      { id: 'windowTitle', name: 'Window Title', type: 'String', defaultValue: '' },
      { id: 'automationId', name: 'Automation Id', type: 'String', defaultValue: '' },
      { id: 'elementName', name: 'Element Name', type: 'String', defaultValue: '' },
      { id: 'controlType', name: 'Control Type', type: 'String', defaultValue: '' },
      { id: 'timeoutMs', name: 'Timeout (ms)', type: 'Integer', defaultValue: 5000 },
    ]),
    define('action.browserExtractText', 'Browser Extract Text', 'Action', 'Reads text from a browser element', [
      { id: 'windowTitle', name: 'Window Title', type: 'String', defaultValue: '' },
      { id: 'automationId', name: 'Automation Id', type: 'String', defaultValue: '' },
      { id: 'elementName', name: 'Element Name', type: 'String', defaultValue: '' },
      { id: 'controlType', name: 'Control Type', type: 'String', defaultValue: '' },
      { id: 'timeoutMs', name: 'Timeout (ms)', type: 'Integer', defaultValue: 5000 },
      { id: 'storeInVariable', name: 'Store In Variable', type: 'String', defaultValue: '' },
    ], [
      flowOut,
      { id: 'text', name: 'Text', dataType: 'String' },
    ]),
    define('action.sendEmail', 'Send Email', 'Action', 'Sends an email through SMTP or a pickup directory', [
      { id: 'from', name: 'From', type: 'String', defaultValue: '' },
      { id: 'to', name: 'To', type: 'String', defaultValue: '' },
      { id: 'subject', name: 'Subject', type: 'String', defaultValue: '' },
      { id: 'body', name: 'Body', type: 'String', defaultValue: '' },
      { id: 'host', name: 'SMTP Host', type: 'String', defaultValue: '' },
      { id: 'port', name: 'SMTP Port', type: 'Integer', defaultValue: 25 },
      { id: 'username', name: 'Username', type: 'String', defaultValue: '' },
      { id: 'password', name: 'Password', type: 'String', defaultValue: '' },
      { id: 'enableSsl', name: 'Enable SSL', type: 'Boolean', defaultValue: false },
      { id: 'pickupDirectory', name: 'Pickup Directory', type: 'FolderPath', defaultValue: '' },
      { id: 'attachments', name: 'Attachments', type: 'String', defaultValue: '' },
    ], [
      { id: 'success', name: 'Success', dataType: 'Flow' },
      { id: 'error', name: 'Error', dataType: 'Flow' },
      { id: 'subject', name: 'Subject', dataType: 'String' },
    ]),
    define('action.requireAdmin', 'Require Admin', 'Action', 'Checks admin status and branches safely', [], [
      { id: 'admin', name: 'Admin', dataType: 'Flow' },
      { id: 'notAdmin', name: 'Not Admin', dataType: 'Flow' },
      { id: 'denied', name: 'Denied', dataType: 'Flow' },
      { id: 'error', name: 'Error', dataType: 'Flow' },
    ]),
    define('action.restartAsAdmin', 'Restart as Admin', 'Action', 'Restarts Sidekick through UAC when confirmed', [
      { id: 'confirmRestart', name: 'Confirm Restart', type: 'Boolean', defaultValue: false },
      { id: 'dryRun', name: 'Dry Run', type: 'Boolean', defaultValue: true },
    ]),
    define('action.taskbarShow', 'Taskbar Show', 'Action', 'Shows the Windows taskbar', [{ id: 'dryRun', name: 'Dry Run', type: 'Boolean', defaultValue: true }]),
    define('action.taskbarHide', 'Taskbar Hide', 'Action', 'Hides the Windows taskbar', [{ id: 'dryRun', name: 'Dry Run', type: 'Boolean', defaultValue: true }]),
    define('action.taskbarSetAlignment', 'Taskbar Set Alignment', 'Action', 'Sets taskbar alignment where supported', [
      { id: 'alignment', name: 'Alignment', type: 'Dropdown', defaultValue: 'center', options: ['left', 'center'] },
      { id: 'dryRun', name: 'Dry Run', type: 'Boolean', defaultValue: true },
    ]),
    define('action.taskbarPinApp', 'Taskbar Pin App', 'Action', 'Safe taskbar pin support with documented Windows limits', [
      { id: 'appPath', name: 'App Path', type: 'FilePath', defaultValue: '' },
      { id: 'dryRun', name: 'Dry Run', type: 'Boolean', defaultValue: true },
    ]),
    define('action.taskbarUnpinApp', 'Taskbar Unpin App', 'Action', 'Safe taskbar unpin support with documented Windows limits', [
      { id: 'appPath', name: 'App Path', type: 'FilePath', defaultValue: '' },
      { id: 'dryRun', name: 'Dry Run', type: 'Boolean', defaultValue: true },
    ]),
    define('action.taskbarOpenPinnedApp', 'Taskbar Open Pinned App', 'Action', 'Opens an app path or explains taskbar limitations', [
      { id: 'appPath', name: 'App Path', type: 'FilePath', defaultValue: '' },
      { id: 'dryRun', name: 'Dry Run', type: 'Boolean', defaultValue: true },
    ]),
    define('action.windowsThemeSetMode', 'Windows Theme Set Mode', 'Action', 'Sets light or dark mode with safety controls', [
      { id: 'mode', name: 'Mode', type: 'Dropdown', defaultValue: 'dark', options: ['light', 'dark', 'system'] },
      { id: 'dryRun', name: 'Dry Run', type: 'Boolean', defaultValue: true },
    ]),
    define('action.windowsAccentColor', 'Windows Accent Color', 'Action', 'Prepares accent color changes safely', [
      { id: 'color', name: 'Color', type: 'Color', defaultValue: '#58A6FF' },
      { id: 'dryRun', name: 'Dry Run', type: 'Boolean', defaultValue: true },
    ]),
    define('action.windowsHighContrast', 'Windows High Contrast', 'Action', 'Documents high-contrast automation limits safely', [
      { id: 'enabled', name: 'Enabled', type: 'Boolean', defaultValue: false },
      { id: 'dryRun', name: 'Dry Run', type: 'Boolean', defaultValue: true },
    ]),
    define('action.wallpaperSetImage', 'Wallpaper Set Image', 'Action', 'Sets wallpaper image with dry-run and backup output', [
      { id: 'imagePath', name: 'Image Path', type: 'FilePath', defaultValue: '' },
      { id: 'style', name: 'Style', type: 'Dropdown', defaultValue: 'fill', options: ['fill', 'fit', 'stretch', 'tile', 'center', 'span'] },
      { id: 'dryRun', name: 'Dry Run', type: 'Boolean', defaultValue: true },
    ]),
    define('action.wallpaperSetColor', 'Wallpaper Set Color', 'Action', 'Sets wallpaper background color safely', [
      { id: 'color', name: 'Color', type: 'Color', defaultValue: '#000000' },
      { id: 'dryRun', name: 'Dry Run', type: 'Boolean', defaultValue: true },
    ]),
    define('action.wallpaperRestorePrevious', 'Wallpaper Restore Previous', 'Action', 'Restores a previous wallpaper path', [
      { id: 'previousWallpaper', name: 'Previous Wallpaper', type: 'FilePath', defaultValue: '' },
      { id: 'dryRun', name: 'Dry Run', type: 'Boolean', defaultValue: true },
    ]),
    define('action.desktopIconsShowHide', 'Desktop Icons Show/Hide', 'Action', 'Shows or hides desktop icons with safe defaults', [
      { id: 'visible', name: 'Visible', type: 'Boolean', defaultValue: true },
      { id: 'dryRun', name: 'Dry Run', type: 'Boolean', defaultValue: true },
    ]),
    define('action.desktopRefresh', 'Desktop Refresh', 'Action', 'Refreshes Explorer desktop icons', [{ id: 'dryRun', name: 'Dry Run', type: 'Boolean', defaultValue: true }]),
    define('action.desktopOpenFolder', 'Desktop Open Folder', 'Action', 'Opens a folder in Explorer', [
      { id: 'path', name: 'Path', type: 'FolderPath', defaultValue: '' },
    ]),
    define('action.desktopCreateShortcut', 'Desktop Create Shortcut', 'Action', 'Creates a desktop shortcut', [
      { id: 'targetPath', name: 'Target Path', type: 'FilePath', defaultValue: '' },
      { id: 'shortcutName', name: 'Shortcut Name', type: 'String', defaultValue: 'Sidekick Shortcut' },
      { id: 'dryRun', name: 'Dry Run', type: 'Boolean', defaultValue: true },
    ]),
    define('action.explorerOpenPath', 'Explorer Open Path', 'Action', 'Opens a path in Explorer', [
      { id: 'path', name: 'Path', type: 'String', defaultValue: '' },
    ]),
    define('action.explorerSelectFile', 'Explorer Select File', 'Action', 'Selects a file in Explorer', [
      { id: 'filePath', name: 'File Path', type: 'FilePath', defaultValue: '' },
    ]),
    define('action.explorerRestart', 'Explorer Restart', 'Action', 'Restarts explorer.exe with dry-run by default', [{ id: 'dryRun', name: 'Dry Run', type: 'Boolean', defaultValue: true }]),
    define('action.explorerSetView', 'Explorer Set View', 'Action', 'Documents Explorer view automation limits', [
      { id: 'view', name: 'View', type: 'Dropdown', defaultValue: 'details', options: ['details', 'icons', 'list'] },
      { id: 'dryRun', name: 'Dry Run', type: 'Boolean', defaultValue: true },
    ]),
    define('action.restorePointCreate', 'Restore Point Create', 'Action', 'Creates or dry-runs a restore point request', [
      { id: 'description', name: 'Description', type: 'String', defaultValue: 'Sidekick restore point' },
      { id: 'dryRun', name: 'Dry Run', type: 'Boolean', defaultValue: true },
    ]),
    define('action.restorePointList', 'Restore Point List', 'Action', 'Lists restore point support status', [{ id: 'dryRun', name: 'Dry Run', type: 'Boolean', defaultValue: true }]),
    define('action.restorePointOpenSystemRestore', 'Open System Restore', 'Action', 'Opens System Restore UI', [{ id: 'dryRun', name: 'Dry Run', type: 'Boolean', defaultValue: true }]),
    define('action.installApp', 'Install App', 'Action', 'Installs apps through winget, MSI, EXE or URL with dry-run', [
      { id: 'sourceType', name: 'Source Type', type: 'Dropdown', defaultValue: 'winget', options: ['winget', 'msi', 'exe', 'url', 'msix', 'store'] },
      { id: 'packageId', name: 'Package Id', type: 'String', defaultValue: '' },
      { id: 'url', name: 'URL / Installer Path', type: 'String', defaultValue: '' },
      { id: 'checksum', name: 'SHA256 Checksum', type: 'String', defaultValue: '' },
      { id: 'dryRun', name: 'Dry Run', type: 'Boolean', defaultValue: true },
    ]),
    define('action.downloadFile', 'Download File', 'Action', 'Downloads a file with checksum support', [
      { id: 'url', name: 'URL', type: 'String', defaultValue: '' },
      { id: 'outputPath', name: 'Output Path', type: 'FilePath', defaultValue: '' },
      { id: 'checksum', name: 'SHA256 Checksum', type: 'String', defaultValue: '' },
      { id: 'dryRun', name: 'Dry Run', type: 'Boolean', defaultValue: true },
    ]),
    define('action.verifyChecksum', 'Verify Checksum', 'Action', 'Verifies SHA256 checksum', [
      { id: 'filePath', name: 'File Path', type: 'FilePath', defaultValue: '' },
      { id: 'checksum', name: 'SHA256 Checksum', type: 'String', defaultValue: '' },
    ]),
    define('action.installWinget', 'Install Winget', 'Action', 'Installs a winget package', [
      { id: 'packageId', name: 'Package Id', type: 'String', defaultValue: '' },
      { id: 'dryRun', name: 'Dry Run', type: 'Boolean', defaultValue: true },
    ]),
    define('action.installMsix', 'Install MSIX', 'Action', 'Prepares MSIX install', [{ id: 'dryRun', name: 'Dry Run', type: 'Boolean', defaultValue: true }]),
    define('action.installMsi', 'Install MSI', 'Action', 'Installs an MSI package', [{ id: 'url', name: 'MSI Path', type: 'String', defaultValue: '' }, { id: 'dryRun', name: 'Dry Run', type: 'Boolean', defaultValue: true }]),
    define('action.installExe', 'Install EXE', 'Action', 'Installs an EXE package', [{ id: 'url', name: 'EXE Path', type: 'String', defaultValue: '' }, { id: 'dryRun', name: 'Dry Run', type: 'Boolean', defaultValue: true }]),
    define('action.checkAppInstalled', 'Check App Installed', 'Action', 'Checks expected process/path', [
      { id: 'expectedProcessName', name: 'Expected Process', type: 'String', defaultValue: '' },
      { id: 'expectedPath', name: 'Expected Path', type: 'String', defaultValue: '' },
    ], [
      { id: 'installed', name: 'Installed', dataType: 'Flow' },
      { id: 'notInstalled', name: 'Not Installed', dataType: 'Flow' },
    ]),
    define('action.persistState', 'Persist State', 'Action', 'Persists small flow state', [
      { id: 'key', name: 'Key', type: 'String', defaultValue: '' },
      { id: 'value', name: 'Value', type: 'String', defaultValue: '' },
      { id: 'expiresAtLocal', name: 'Expires At Local', type: 'String', defaultValue: '' },
    ]),
    define('action.readState', 'Read State', 'Action', 'Reads persisted flow state', [
      { id: 'key', name: 'Key', type: 'String', defaultValue: '' },
      { id: 'storeInVariable', name: 'Store In Variable', type: 'String', defaultValue: '' },
    ], [
      { id: 'found', name: 'Found', dataType: 'Flow' },
      { id: 'missing', name: 'Missing', dataType: 'Flow' },
    ]),
    define('logic.untilDateTime', 'Until Date/Time', 'Logic', 'Routes before/after local time', [
      { id: 'untilLocalTime', name: 'Until Local Time', type: 'String', defaultValue: '00:00' },
    ], [
      { id: 'before', name: 'Before', dataType: 'Flow' },
      { id: 'after', name: 'After', dataType: 'Flow' },
    ]),
    define('logic.dailyReset', 'Daily Reset', 'Logic', 'Routes reset/notReset by local date', [
      { id: 'lastDate', name: 'Last Date', type: 'String', defaultValue: '' },
    ], [
      { id: 'reset', name: 'Reset', dataType: 'Flow' },
      { id: 'notReset', name: 'Not Reset', dataType: 'Flow' },
    ]),
    define('action.showNotification', 'Show Notification', 'Action', 'Shows a desktop notification on Windows', [
      { id: 'title', name: 'Title', type: 'String', defaultValue: '' },
      { id: 'message', name: 'Message', type: 'String', defaultValue: '' },
      { id: 'timeoutMs', name: 'Timeout (ms)', type: 'Integer', defaultValue: 3000 },
    ], [
      flowOut,
      { id: 'title', name: 'Title', dataType: 'String' },
    ]),
  ].sort((a, b) => {
    if (a.category !== b.category) {
      const order = { Trigger: 0, Logic: 1, Action: 2 };
      return order[a.category] - order[b.category];
    }

    return a.displayName.localeCompare(b.displayName);
  });
}
