import type { NodeDefinition } from '../bridge/types';

export type ProductNodeCategory =
  | 'Trigger'
  | 'Desktop'
  | 'Window'
  | 'Hardware'
  | 'Media'
  | 'Console'
  | 'Logic'
  | 'Data'
  | 'Utility';

export type ProductCapabilityTier = 'Core' | 'Labs';

const productCategoryOrder: ProductNodeCategory[] = [
  'Trigger',
  'Desktop',
  'Window',
  'Hardware',
  'Media',
  'Console',
  'Logic',
  'Data',
  'Utility',
];

const productCategoryColors: Record<ProductNodeCategory, string> = {
  Trigger: '#EF4444',
  Desktop: '#22C55E',
  Window: '#38BDF8',
  Hardware: '#F97316',
  Media: '#A855F7',
  Console: '#14B8A6',
  Logic: '#EAB308',
  Data: '#818CF8',
  Utility: '#94A3B8',
};

const productCategoryIcons: Record<ProductNodeCategory, string> = {
  Trigger: '!',
  Desktop: 'D',
  Window: 'W',
  Hardware: 'H',
  Media: 'M',
  Console: '>',
  Logic: '?',
  Data: '{}',
  Utility: '*',
};

export function getProductCategoryOrder(): ProductNodeCategory[] {
  return [...productCategoryOrder];
}

export function getProductCategoryColor(category: ProductNodeCategory): string {
  return productCategoryColors[category];
}

export function getProductCategoryIcon(category: ProductNodeCategory): string {
  return productCategoryIcons[category];
}

export function getNodeCapabilityTier(definition: NodeDefinition): ProductCapabilityTier {
  const typeId = definition.typeId.toLocaleLowerCase('en-US');
  const haystack = `${definition.typeId} ${definition.displayName} ${definition.description}`.toLocaleLowerCase('en-US');

  if (
    typeId.includes('console')
    || typeId.includes('command')
    || typeId.includes('runsavedflow')
    || typeId.includes('listrunnableflows')
    || typeId.includes('systempower')
    || typeId.includes('hardware')
    || typeId.includes('systemaudio')
    || typeId.includes('displaysettings')
    || typeId.includes('restorepoint')
    || typeId.includes('requireadmin')
    || typeId.includes('restartasadmin')
    || typeId.includes('install')
    || typeId.includes('download')
    || typeId.includes('checksum')
    || typeId.includes('deletefile')
    || typeId.includes('killprocess')
    || typeId.includes('cleanfolder')
    || typeId.includes('organizefolder')
    || typeId.includes('batchrename')
    || typeId.includes('archive')
    || typeId.includes('pdf')
    || typeId.includes('documentconvert')
    || typeId.includes('imageconvert')
    || typeId.includes('imageresize')
    || typeId.includes('imagecompress')
    || typeId.includes('videoconvert')
    || typeId.includes('videoextractaudio')
    || typeId.includes('audioconvert')
    || typeId.includes('audionormalize')
    || typeId.includes('recorddesktop')
    || typeId.includes('recordcamera')
    || typeId.includes('taskbar')
    || typeId.includes('wallpaper')
    || typeId.includes('desktopicons')
    || typeId.includes('explorerrestart')
    || typeId.includes('windowstheme')
    || typeId.includes('windowsaccent')
    || typeId.includes('highcontrast')
    || typeId.includes('http')
    || haystack.includes('powershell')
    || haystack.includes('shutdown')
    || haystack.includes('destructive')
    || haystack.includes('installer')
    || haystack.includes('ffmpeg')
    || haystack.includes('libreoffice')
  ) {
    return 'Labs';
  }

  return 'Core';
}

export function getNodeProductCategory(definition: NodeDefinition): ProductNodeCategory {
  const typeId = definition.typeId.toLocaleLowerCase('en-US');
  const haystack = `${definition.typeId} ${definition.displayName} ${definition.description}`.toLocaleLowerCase('en-US');

  if (definition.category === 'Trigger') {
    return 'Trigger';
  }

  if (definition.category === 'Logic') {
    return 'Logic';
  }

  if (
    typeId.includes('capture')
    || typeId.includes('record')
    || typeId.includes('overlay')
    || typeId.includes('sound')
    || haystack.includes('screenshot')
    || haystack.includes('image')
    || haystack.includes('video')
  ) {
    return 'Media';
  }

  if (
    typeId.includes('desktop')
    || typeId.includes('mouse')
    || typeId.includes('keyboard')
    || typeId.includes('browser')
    || haystack.includes('element')
    || haystack.includes('click')
  ) {
    return 'Desktop';
  }

  if (typeId.includes('window') || typeId.includes('process') || haystack.includes('window')) {
    return 'Window';
  }

  if (
    typeId.includes('hardware')
    || typeId.includes('audio')
    || typeId.includes('power')
    || typeId.includes('display')
    || haystack.includes('camera')
    || haystack.includes('wi-fi')
    || haystack.includes('microphone')
  ) {
    return 'Hardware';
  }

  if (typeId.includes('console') || haystack.includes('command') || haystack.includes('powershell')) {
    return 'Console';
  }

  if (
    typeId.includes('json')
    || typeId.includes('csv')
    || typeId.includes('file')
    || typeId.includes('clipboard')
    || typeId.includes('http')
    || typeId.includes('email')
  ) {
    return 'Data';
  }

  return 'Utility';
}
