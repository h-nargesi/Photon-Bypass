import { from, Observable } from 'rxjs';

export class LocalStorageService {
  public static set(path: string[], value: any) {
    if (!(path?.length ?? false)) return;
    if (value === undefined) value = null;
    localStorage.setItem(path.join('|'), JSON.stringify(value));
  }

  public static get(path: string[]): any {
    if (!(path?.length ?? false)) return undefined;
    const json = localStorage.getItem(path.join('|'));
    if (!json) return undefined;
    return JSON.parse(json);
  }

  public static readonly UseAPI: boolean = false;
}

export const wait = <M>(
  result: M,
  min: number = 1000,
  length: number = 3000
): Observable<M> => {
  return from(
    new Promise<M>((resolve) => {
      setTimeout(
        () => resolve(result),
        Math.floor(Math.random() * (length + 1) + min)
      );
    })
  );
};
