const DEFAULT_LOCALE = 'es-MX';

export function detectUserTimeZone(): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
  } catch {
    return 'UTC';
  }
}

export function getAvailableTimeZones(preferredTimeZone?: string): string[] {
  const values = typeof Intl.supportedValuesOf === 'function'
    ? Intl.supportedValuesOf('timeZone')
    : [];

  const merged = new Set<string>(['Etc/UTC', preferredTimeZone || '', ...values].filter(Boolean));
  return Array.from(merged).sort((left, right) => left.localeCompare(right));
}

export function formatUtcInTimeZone(
  value: string | Date | null | undefined,
  timeZone: string,
  options: Intl.DateTimeFormatOptions
): string {
  if (!value) return '--';

  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return '--';

  return date.toLocaleString(DEFAULT_LOCALE, {
    timeZone,
    ...options
  });
}

export function formatUtcWithZoneContext(
  value: string | Date | null | undefined,
  userTimeZone: string,
  boxTimeZoneId: string | null | undefined,
  userOptions: Intl.DateTimeFormatOptions,
  boxOptions: Intl.DateTimeFormatOptions = userOptions
): string {
  const userText = formatUtcInTimeZone(value, userTimeZone, userOptions);
  if (!boxTimeZoneId || boxTimeZoneId === userTimeZone) return userText;

  const boxText = formatUtcInTimeZone(value, boxTimeZoneId, boxOptions);
  return `${userText} (${boxText} ${boxTimeZoneId})`;
}

export function getDateKeyInTimeZone(value: string | Date, timeZone: string): string {
  return formatUtcInTimeZone(value, timeZone, {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit'
  });
}