import { printMoney } from './money-printer';

describe('printMoney', () => {
  const fa = new Intl.NumberFormat('fa-IR');

  it('multiplies the value by 1000 and formats it in Persian digits', () => {
    // prices are stored in thousands of tomans (L1)
    expect(printMoney(2)).toBe(fa.format(2000));
    expect(printMoney(1)).toBe(fa.format(1000));
  });

  it('formats decimals exactly', () => {
    expect(printMoney(1.5)).toBe(fa.format(1500));
  });

  it('keeps negative balances signed', () => {
    expect(printMoney(-2)).toBe(fa.format(-2000));
  });

  it('renders zero for undefined and NaN instead of throwing', () => {
    expect(printMoney(undefined)).toBe(fa.format(0));
    expect(printMoney(Number.NaN)).toBe(fa.format(0));
  });

  it('renders zero for zero', () => {
    expect(printMoney(0)).toBe(fa.format(0));
  });
});
