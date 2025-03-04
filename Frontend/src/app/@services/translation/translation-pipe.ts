import { Pipe, PipeTransform } from '@angular/core';
import { TranslationService } from './translation-service';

@Pipe({
  name: 'translate',
  pure: false,
})
export class TranslationPipe implements PipeTransform {
  constructor(private readonly translation: TranslationService) {}

  public transform(path: string, params?: string[]): string {
    return this.translation.translate(path, params);
  }
}
