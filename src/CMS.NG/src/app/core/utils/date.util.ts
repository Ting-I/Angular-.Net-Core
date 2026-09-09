/**
 * Date helpers for the `date` columns in the schema.
 *
 * Everything here works in **local** components on purpose. `toISOString()` converts to UTC first,
 * which lands a UTC+8 operator on the previous day — the classic off-by-one that makes a course
 * scheduled for the 1st save as the 31st.
 */

/** Serialises a Date to `yyyy-MM-dd` using local components. Never via toISOString(). */
export function toIso(value: Date | null | undefined): string | null {
  if (!(value instanceof Date) || Number.isNaN(value.getTime())) {
    return null;
  }

  const year = value.getFullYear();
  const month = `${value.getMonth() + 1}`.padStart(2, '0');
  const day = `${value.getDate()}`.padStart(2, '0');
  return `${year}-${month}-${day}`;
}

/**
 * Parses `yyyy-MM-dd` into a local Date. Passing the three components to the constructor avoids
 * `new Date('2026-01-01')`, which the spec defines as UTC midnight.
 */
export function fromIso(value: string | null | undefined): Date | null {
  if (!value) {
    return null;
  }

  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value);
  if (!match) {
    return null;
  }

  return new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
}

/**
 * Adds whole years, clamping 29 February onto 28 February in a non-leap target year rather than
 * letting it roll over into March.
 */
export function addYears(value: Date, years: number): Date {
  const result = new Date(value.getFullYear() + years, value.getMonth(), value.getDate());
  if (result.getDate() !== value.getDate()) {
    result.setDate(0);
  }
  return result;
}

/** Adds whole days in local time; the Date constructor normalises overflow across month ends. */
export function addDays(value: Date, days: number): Date {
  return new Date(value.getFullYear(), value.getMonth(), value.getDate() + days);
}

/**
 * Local midnight of the Monday on or before `value`. JavaScript numbers Sunday as 0, so the
 * offset is rotated to make Monday the first day of the week — Sunday belongs to the week that
 * started six days earlier, not to a new one.
 */
export function startOfWeek(value: Date): Date {
  const daysSinceMonday = (value.getDay() + 6) % 7;
  return addDays(value, -daysSinceMonday);
}

/**
 * Formats an API timestamp as `yyyy-MM-dd HH:mm` for display.
 *
 * The API sends 異動時間 with no timezone offset, which `new Date()` parses as local — so the
 * components come back out exactly as the server wrote them. Reading them with `getFullYear()`
 * rather than slicing the string keeps that true for any other shape the wire might carry.
 * Anything unparseable yields null, so a caller renders its own placeholder rather than
 * `Invalid Date`.
 */
export function formatDateTime(value: string | Date | null | undefined): string | null {
  if (value === null || value === undefined || value === '') {
    return null;
  }

  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) {
    return null;
  }

  const time =
    `${date.getHours()}`.padStart(2, '0') + ':' + `${date.getMinutes()}`.padStart(2, '0');
  return `${toIso(date)} ${time}`;
}
