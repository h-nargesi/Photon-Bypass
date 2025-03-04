import { Injectable } from '@angular/core';
import * as _home_page from '../../../../public/texts/home-page.json';
import * as _global from '../../../../public/texts/global.json';

@Injectable({ providedIn: 'root' })
export class TranslationService {
  private static readonly texts: Map<string, any> = new Map<string, any>();

  constructor() {
    if (TranslationService.texts.size < 1) {
      TranslationService.initialize();
    } else {
      console.warn(
        `Translation Servcie was initialized (size:${TranslationService.texts.size}).`
      );
    }
  }

  public translate(path: string, params?: string[]): string {
    return TranslationService.parse(path, params);
  }

  public static translate(path: string, params?: string[]): string {
    if (TranslationService.texts.size < 1) {
      console.log(`Translation Servcie first request is static: ${path}.`);
      TranslationService.initialize();
    }
    return TranslationService.parse(path, params);
  }

  private static parse(path: string, params?: string[]): string {
    if (!path || !path.length) return path;

    let result = TranslationService.get(path);

    if (params) {
      let i = 0;
      while (i < params.length) {
        result = result.replace(`{${i}}`, TranslationService.get(params[i]));
        i++;
      }
    }

    return result;
  }

  private static get(path: string): string {
    if (!path || !path.length) return path;

    const sections = path.split('.');

    let section = sections.shift() ?? '';
    if (!TranslationService.texts.has(section)) return path;
    let value = TranslationService.texts.get(section);

    while (sections.length) {
      section = sections.shift() ?? '';
      if (value !== null && typeof value === 'object' && section in value) {
        value = value[section];
      } else return path;
    }

    if (Array.isArray(value)) {
      value = value.join("\n");
    }

    return value;
  }

  private static initialize(): void {
    TranslationService.texts.set('home-page', _home_page);
    TranslationService.texts.set('global', _global);
    console.log('Translation Servcie is initialized.');
  }
}
