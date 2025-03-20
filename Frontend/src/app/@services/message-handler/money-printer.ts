export function printMoney(money?: number) {
  const value = money?.toString();
  if (!value) return '0';

  let index = value.length % 3;
  if (index === 0) index = 3;

  let result: string = value.substring(0, index);
  while (index < value.length) {
    result += ',' + value.substring(index, index + 3);
    index += 3;
  }

  return result + ',000';
}
