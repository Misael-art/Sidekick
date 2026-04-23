import type { NodeDefinition } from './bridge/types';

/** Dev-mode sample definitions so the palette stays usable without the desktop host.
 *  Keep these aligned with the real backend registry TypeIds. */
export function getDevNodeDefinitions(): NodeDefinition[] {
  const flowIn = { id: 'in', name: 'In', dataType: 'Flow' as const };
  const flowOut = { id: 'out', name: 'Out', dataType: 'Flow' as const };
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
      { id: 'clicks', name: 'Clicks', type: 'Integer', defaultValue: 1 },
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
      { id: 'hotkey', name: 'Hotkey', type: 'Hotkey', defaultValue: '' },
    ]),
    define('action.openProgram', 'Open Program', 'Action', 'Launch an external program', [
      { id: 'path', name: 'Program Path', type: 'FilePath', defaultValue: '' },
      { id: 'args', name: 'Arguments', type: 'String', defaultValue: '' },
    ]),
    define('action.killProcess', 'Kill Process', 'Action', 'Terminate a running process', [
      { id: 'processName', name: 'Process Name', type: 'String', defaultValue: '' },
    ]),
    define('action.playSound', 'Play Sound', 'Action', 'Play an audio file', [
      { id: 'filePath', name: 'Sound File', type: 'FilePath', defaultValue: '' },
    ]),
    define('action.deleteFile', 'Delete File', 'Action', 'Delete a file from disk', [
      { id: 'filePath', name: 'File Path', type: 'FilePath', defaultValue: '' },
    ]),
    define('action.browserOpenUrl', 'Browser Open URL', 'Action', 'Opens a URL in the browser', [
      { id: 'url', name: 'URL', type: 'String', defaultValue: '' },
      { id: 'browserPath', name: 'Browser Path', type: 'FilePath', defaultValue: '' },
    ]),
    define('action.browserClick', 'Browser Click', 'Action', 'Clicks a browser element found by locator', [
      { id: 'windowTitle', name: 'Window Title', type: 'String', defaultValue: '' },
      { id: 'automationId', name: 'Automation Id', type: 'String', defaultValue: '' },
      { id: 'name', name: 'Name', type: 'String', defaultValue: '' },
      { id: 'timeoutMs', name: 'Timeout (ms)', type: 'Integer', defaultValue: 3000 },
    ]),
    define('action.browserType', 'Browser Type', 'Action', 'Types text into a browser element', [
      { id: 'windowTitle', name: 'Window Title', type: 'String', defaultValue: '' },
      { id: 'automationId', name: 'Automation Id', type: 'String', defaultValue: '' },
      { id: 'name', name: 'Name', type: 'String', defaultValue: '' },
      { id: 'text', name: 'Text', type: 'String', defaultValue: '' },
      { id: 'timeoutMs', name: 'Timeout (ms)', type: 'Integer', defaultValue: 3000 },
    ]),
    define('action.browserWaitElement', 'Browser Wait Element', 'Action', 'Waits until a browser element is available', [
      { id: 'windowTitle', name: 'Window Title', type: 'String', defaultValue: '' },
      { id: 'automationId', name: 'Automation Id', type: 'String', defaultValue: '' },
      { id: 'name', name: 'Name', type: 'String', defaultValue: '' },
      { id: 'timeoutMs', name: 'Timeout (ms)', type: 'Integer', defaultValue: 3000 },
    ]),
    define('action.browserExtractText', 'Browser Extract Text', 'Action', 'Reads text from a browser element', [
      { id: 'windowTitle', name: 'Window Title', type: 'String', defaultValue: '' },
      { id: 'automationId', name: 'Automation Id', type: 'String', defaultValue: '' },
      { id: 'name', name: 'Name', type: 'String', defaultValue: '' },
      { id: 'timeoutMs', name: 'Timeout (ms)', type: 'Integer', defaultValue: 3000 },
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
