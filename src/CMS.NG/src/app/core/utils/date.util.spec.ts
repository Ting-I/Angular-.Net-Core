import { addYears, fromIso, toIso } from './date.util';

describe('date.util', () => {
  describe('toIso', () => {
    it('serialises a Date using its local components', () => {
      expect(toIso(new Date(2026, 0, 1))).toBe('2026-01-01');
      expect(toIso(new Date(2026, 11, 31))).toBe('2026-12-31');
    });

    /**
     * The case toISOString() gets wrong: late on the 1st in UTC+8 is still the 31st in UTC, so
     * `toISOString().split('T')[0]` would report the previous day — and the previous month.
     */
    it('keeps the local calendar day for a late-evening date', () => {
      const lateEvening = new Date(2026, 0, 1, 23, 30, 0);

      expect(toIso(lateEvening)).toBe('2026-01-01');
    });

    it('zero-pads single-digit months and days', () => {
      expect(toIso(new Date(2026, 8, 7))).toBe('2026-09-07');
    });

    it('returns null for null, undefined, and an invalid Date', () => {
      expect(toIso(null)).toBeNull();
      expect(toIso(undefined)).toBeNull();
      expect(toIso(new Date('nonsense'))).toBeNull();
    });
  });

  describe('fromIso', () => {
    it('parses yyyy-MM-dd into a local Date', () => {
      const parsed = fromIso('2026-01-01')!;

      expect(parsed.getFullYear()).toBe(2026);
      expect(parsed.getMonth()).toBe(0);
      expect(parsed.getDate()).toBe(1);
      // Local midnight, not UTC midnight shifted into the local zone.
      expect(parsed.getHours()).toBe(0);
    });

    it('ignores a time part the API may append', () => {
      const parsed = fromIso('2026-06-15T00:00:00')!;

      expect(parsed.getMonth()).toBe(5);
      expect(parsed.getDate()).toBe(15);
    });

    it('returns null for null, empty, and unparseable input', () => {
      expect(fromIso(null)).toBeNull();
      expect(fromIso(undefined)).toBeNull();
      expect(fromIso('')).toBeNull();
      expect(fromIso('not-a-date')).toBeNull();
    });

    it('round-trips with toIso', () => {
      expect(toIso(fromIso('2026-09-07'))).toBe('2026-09-07');
    });
  });

  describe('addYears', () => {
    it('adds whole years, keeping the month and day', () => {
      expect(toIso(addYears(new Date(2026, 0, 1), 10))).toBe('2036-01-01');
    });

    /** 2028 is a leap year, 2038 is not — the day clamps back rather than rolling into March. */
    it('clamps 29 February onto 28 February in a non-leap target year', () => {
      expect(toIso(addYears(new Date(2028, 1, 29), 10))).toBe('2038-02-28');
    });

    it('keeps 29 February when the target year is also a leap year', () => {
      expect(toIso(addYears(new Date(2028, 1, 29), 4))).toBe('2032-02-29');
    });

    it('does not mutate the input', () => {
      const original = new Date(2026, 0, 1);
      addYears(original, 10);

      expect(original.getFullYear()).toBe(2026);
    });
  });
});
