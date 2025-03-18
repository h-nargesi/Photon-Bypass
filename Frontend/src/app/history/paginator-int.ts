import { Injectable } from '@angular/core';
import { MatPaginatorIntl } from '@angular/material/paginator';
import { TranslationService } from '../@services';

@Injectable()
export class PaginatorInt extends MatPaginatorIntl {
  constructor(private readonly translation: TranslationService) {
    super();
    this.getAndInitTranslations();
  }

  getAndInitTranslations() {
    this.itemsPerPageLabel = this.translation.translate(
      'global.pagination.items-per-page'
    );
    this.nextPageLabel = this.translation.translate(
      'global.pagination.next-page'
    );
    this.previousPageLabel = this.translation.translate(
      'global.pagination.prev-page'
    );
    this.changes.next();
  }

  override getRangeLabel = (page: number, pageSize: number, length: number) => {
    if (length === 0 || pageSize === 0) {
      return `0 / ${length}`;
    }
    length = Math.max(length, 0);
    const startIndex = page * pageSize;
    const endIndex =
      startIndex < length
        ? Math.min(startIndex + pageSize, length)
        : startIndex + pageSize;
    return `${startIndex + 1} - ${endIndex} / ${length}`;
  };
}
