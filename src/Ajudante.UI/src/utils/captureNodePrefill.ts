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
