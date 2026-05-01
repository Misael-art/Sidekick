export function isValidDropdownValue(value: unknown, options: readonly string[] | null | undefined): boolean {
  if (value === null || value === undefined || value === '') {
    return true;
  }
  if (typeof value !== 'string') {
    return false;
  }
  if (!options || options.length === 0) {
    return true;
  }
  return options.includes(value);
}

export function describeInvalidDropdown(value: unknown, options: readonly string[] | null | undefined): string | null {
  if (isValidDropdownValue(value, options)) {
    return null;
  }
  const list = options && options.length > 0 ? options.join(', ') : '(none)';
  return `Valor invalido "${String(value)}". Esperado um de: ${list}`;
}
