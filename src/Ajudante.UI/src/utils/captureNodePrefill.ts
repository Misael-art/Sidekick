import type { CapturedElement, CapturedRegion, ImageTemplateValue } from '../bridge/types';

export function createMiraSelectorOverrides(capture: CapturedElement | null | undefined): Record<string, unknown> {
  if (!capture) {
    return {};
  }

  return {
    windowTitle: capture.windowTitle ?? '',
    windowTitleMatch: 'contains',
    processName: capture.processName ?? capture.asset?.source.processName ?? '',
    processPath: capture.processPath ?? capture.asset?.source.processPath ?? '',
    automationId: capture.automationId ?? capture.asset?.locator.selector.automationId ?? '',
    elementName: capture.name ?? capture.asset?.locator.selector.name ?? '',
    controlType: capture.controlType ?? capture.asset?.locator.selector.controlType ?? '',
    fallbackPipeline: 'selector -> relativeWindow -> normalizedWindow -> scaledScreen -> absoluteDesktop -> image',
    useRelativeFallback: capture.asset?.locator.fallback?.useRelativeFallback ?? true,
    useScaledFallback: capture.asset?.locator.fallback?.useScaledFallback ?? true,
    useAbsoluteFallback: capture.asset?.locator.fallback?.useAbsoluteFallback ?? true,
    restoreWindowBeforeFallback: capture.asset?.locator.fallback?.restoreWindowBeforeFallback ?? true,
    expectedWindowState:
      capture.windowStateAtCapture ??
      capture.asset?.locator.fallback?.expectedWindowState ??
      capture.asset?.content.windowStateAtCapture ??
      'normal',
    capturedWindowBounds: JSON.stringify(capture.windowBounds ?? {}),
    capturedScreenBounds: JSON.stringify(capture.monitorBounds ?? {}),
    relativeX:
      capture.relativePointX ??
      capture.asset?.locator.fallback?.relativeX ??
      0,
    relativeY:
      capture.relativePointY ??
      capture.asset?.locator.fallback?.relativeY ??
      0,
    normalizedX:
      capture.normalizedWindowX ??
      capture.asset?.locator.fallback?.normalizedX ??
      0,
    normalizedY:
      capture.normalizedWindowY ??
      capture.asset?.locator.fallback?.normalizedY ??
      0,
    absoluteX:
      capture.cursorScreen?.x ??
      capture.asset?.locator.fallback?.absoluteX ??
      0,
    absoluteY:
      capture.cursorScreen?.y ??
      capture.asset?.locator.fallback?.absoluteY ??
      0,
  };
}

export function createSnipTemplateOverrides(capture: CapturedRegion | null | undefined): Record<string, unknown> {
  if (!capture?.image) {
    return {};
  }

  const template: ImageTemplateValue = {
    kind: capture.asset?.id ? 'snipAsset' : 'inline',
    assetId: capture.asset?.id,
    displayName: capture.asset?.displayName ?? 'Latest Snip capture',
    imagePath: capture.asset?.content.imagePath,
    imageBase64: capture.image,
  };

  return {
    templateImage: template,
  };
}
