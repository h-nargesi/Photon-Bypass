const money_formatter = new Intl.NumberFormat('fa-IR');

export function printMoney(money?: number) {
  const value = Number.isFinite(money) ? money! * 1000 : 0;

  return money_formatter.format(value);
}
