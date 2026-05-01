import { describe, expect, it } from 'vitest';
import { describeInvalidDropdown, isValidDropdownValue } from './propertyValidation';

describe('isValidDropdownValue', () => {
  const options = ['Left', 'Right', 'Middle'];

  it('accepts empty value', () => {
    expect(isValidDropdownValue('', options)).toBe(true);
    expect(isValidDropdownValue(null, options)).toBe(true);
    expect(isValidDropdownValue(undefined, options)).toBe(true);
  });

  it('accepts value present in options', () => {
    expect(isValidDropdownValue('Left', options)).toBe(true);
  });

  it('rejects value not in options', () => {
    expect(isValidDropdownValue('lef', options)).toBe(false);
    expect(isValidDropdownValue('foo', options)).toBe(false);
  });

  it('rejects non-string non-empty value', () => {
    expect(isValidDropdownValue(123 as unknown, options)).toBe(false);
  });

  it('treats missing or empty options as permissive', () => {
    expect(isValidDropdownValue('anything', null)).toBe(true);
    expect(isValidDropdownValue('anything', [])).toBe(true);
  });
});

describe('describeInvalidDropdown', () => {
  it('returns null when valid', () => {
    expect(describeInvalidDropdown('Left', ['Left', 'Right'])).toBeNull();
  });

  it('returns descriptive message when invalid', () => {
    const msg = describeInvalidDropdown('lef', ['Left', 'Right']);
    expect(msg).toContain('lef');
    expect(msg).toContain('Left');
    expect(msg).toContain('Right');
  });
});
