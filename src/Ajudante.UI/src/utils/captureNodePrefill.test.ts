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
      processPath: '%LOCALAPPDATA%\\Programs\\Trae\\Trae.exe',
      windowTitle: 'Trae',
      windowBounds: { x: 50, y: 60, width: 800, height: 600 },
      monitorBounds: { x: 0, y: 0, width: 1920, height: 1080 },
      cursorScreen: { x: 140, y: 260 },
      relativePointX: 90,
      relativePointY: 200,
      normalizedWindowX: 0.25,
      normalizedWindowY: 0.5,
      windowStateAtCapture: 'normal',
    };

    expect(createMiraSelectorOverrides(capture)).toMatchObject({
      windowTitle: 'Trae',
      windowTitleMatch: 'contains',
      processName: 'Trae',
      processPath: '%LOCALAPPDATA%\\Programs\\Trae\\Trae.exe',
      automationId: 'continue-button',
      elementName: 'Continue',
      controlType: 'button',
      useRelativeFallback: true,
      useScaledFallback: true,
      useAbsoluteFallback: true,
      restoreWindowBeforeFallback: true,
      expectedWindowState: 'normal',
      relativeX: 90,
      relativeY: 200,
      normalizedX: 0.25,
      normalizedY: 0.5,
      absoluteX: 140,
      absoluteY: 260,
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
