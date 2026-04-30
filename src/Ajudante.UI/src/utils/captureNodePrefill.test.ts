import { describe, expect, it } from 'vitest';
import {
  createMiraSelectorOverrides,
  createSnipTemplateOverrides,
} from './captureNodePrefill';
import type { CapturedElement, CapturedRegion } from '../bridge/types';

describe('captureNodePrefill', () => {
  it('maps a Mira capture to desktop selector properties', () => {
    const capture: CapturedElement = {
      automationId: 'continue-button',
      name: 'Continue',
      className: 'Button',
      controlType: 'button',
      boundingRect: { x: 100, y: 200, width: 80, height: 28 },
      processId: 1234,
      processName: 'Trae',
      processPath: 'C:\\Users\\misae\\AppData\\Local\\Programs\\Trae\\Trae.exe',
      windowTitle: 'Trae',
    };

    expect(createMiraSelectorOverrides(capture)).toMatchObject({
      windowTitle: 'Trae',
      windowTitleMatch: 'contains',
      processName: 'Trae',
      processPath: 'C:\\Users\\misae\\AppData\\Local\\Programs\\Trae\\Trae.exe',
      automationId: 'continue-button',
      elementName: 'Continue',
      controlType: 'button',
    });
  });

  it('maps a Snip capture to the image template property used by clickImageMatch', () => {
    const capture: CapturedRegion = {
      image: 'base64-png',
      bounds: { x: 10, y: 20, width: 120, height: 32 },
      asset: {
        id: 'snip-1',
        kind: 'snip',
        version: 1,
        createdAt: '2026-04-29T00:00:00.000Z',
        updatedAt: '2026-04-29T00:00:00.000Z',
        displayName: 'Continue button image',
        tags: [],
        notes: null,
        source: { processName: 'Trae', processId: 1234, windowTitle: 'Trae' },
        captureBounds: { x: 10, y: 20, width: 120, height: 32 },
        content: {
          imagePath: 'assets/snip-1.png',
          ocrText: null,
          ocrConfidence: null,
        },
      },
    };

    expect(createSnipTemplateOverrides(capture)).toEqual({
      templateImage: {
        kind: 'snipAsset',
        assetId: 'snip-1',
        displayName: 'Continue button image',
        imagePath: 'assets/snip-1.png',
        imageBase64: 'base64-png',
      },
    });
  });
});
