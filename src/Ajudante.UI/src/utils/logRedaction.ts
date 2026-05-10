const keyValueSecretPattern = /\b(password|passwd|pwd|token|api[_-]?key|secret|client[_-]?secret)\b(\s*[:=]\s*)([^\s;,&]+)/gi;
const bearerPattern = /\b(authorization\s*:\s*bearer\s+)([^\s;,&]+)/gi;
const openAiKeyPattern = /\bsk-(?:proj-|live-)?[A-Za-z0-9_-]{6,}\b/g;

export function redactLogMessage(message: string): string {
  return message
    .replace(keyValueSecretPattern, '$1$2***')
    .replace(bearerPattern, '$1***')
    .replace(openAiKeyPattern, 'sk-***');
}
