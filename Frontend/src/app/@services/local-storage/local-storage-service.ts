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
}
