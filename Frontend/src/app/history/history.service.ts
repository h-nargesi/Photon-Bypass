import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { HistoryRecord } from '../@models';
import { ApiBaseService, LocalStorageService, wait } from '../@services';
import { HistoryComponent } from './history.component';

const COLORS: string[] = [
  'success',
  'info',
  'warning',
  'danger',
];

const UNITS: string[] = [
  'تومان',
  'گیگابایت',
  'روز',
];

const NAMES: string[] = [
  'Maia',
  'Asher',
  'Olivia',
  'Atticus',
  'Amelia',
  'Jack',
  'Charlotte',
  'Theodore',
  'Isla',
  'Oliver',
  'Isabella',
  'Jasper',
  'Cora',
  'Levi',
  'Violet',
  'Arthur',
  'Mia',
  'Thomas',
  'Elizabeth',
];

@Injectable({ providedIn: HistoryComponent })
export class HistoryService extends ApiBaseService {
  load(): Observable<HistoryRecord[]> {
    if (LocalStorageService.UseAPI) {
      return this.getData<HistoryRecord[]>(`${ACOUNT_API_URL}/history`);
    }

    const data = Array.from({ length: 100 }, (_, k) => createNewRecord(k));

    return wait(data, 200);
  }
}

const ACOUNT_API_URL: string = '';

function createNewRecord(id: number): HistoryRecord {
  const timeEvent = new Date().getTime() - id * 84000000 - Math.random() * 84000000;

  const name =
    NAMES[Math.round(Math.random() * (NAMES.length - 1))] + ' ' +
    NAMES[Math.round(Math.random() * (NAMES.length - 1))].charAt(0) + '.';

  return {
    id: id,
    eventTime: timeEvent,
    eventTimeTitle: new Date(timeEvent).toDateString(),
    title: name,
    value: Math.floor(Math.random() * 5000),
    unit: UNITS[Math.round(Math.random() * (UNITS.length - 1))],
    color: COLORS[Math.round(Math.random() * (COLORS.length - 1))],
  };
}
