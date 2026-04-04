const DEFAULT_LOCALE = 'es-MX';

export type FrequencyOption = 'hourly' | 'every10' | 'every15' | 'every30' | 'onceDaily';

export interface CronSchedule {
  days: number[];
  frequency: FrequencyOption;
  specificTime: string;
}

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

export function parseCronToSchedule(cron: string): CronSchedule | null {
  if (!cron) return null;
  const parts = cron.trim().split(/\s+/);
  if (parts.length !== 5) return null;

  const [minute, hour, , , dayOfWeek] = parts;
  const days = dayOfWeek === '*'
    ? [0, 1, 2, 3, 4, 5, 6]
    : dayOfWeek.split(',').map(Number).filter(d => d >= 0 && d <= 6);

  if (days.length === 0) return null;

  if (minute === '0' && hour === '*') return { days, frequency: 'hourly', specificTime: '00:00' };
  if (minute === '*/10' && hour === '*') return { days, frequency: 'every10', specificTime: '00:00' };
  if (minute === '*/15' && hour === '*') return { days, frequency: 'every15', specificTime: '00:00' };
  if (minute === '*/30' && hour === '*') return { days, frequency: 'every30', specificTime: '00:00' };

  if (/^\d+$/.test(minute) && /^\d+$/.test(hour)) {
    return {
      days,
      frequency: 'onceDaily',
      specificTime: `${hour.padStart(2, '0')}:${minute.padStart(2, '0')}`
    };
  }

  return null;
}

export function describeCron(cron: string, timeZoneId = 'Etc/UTC'): string {
  const config = parseCronToSchedule(cron);
  if (!config) return cron || 'Manual only';

  const days = config.days.length === 7
    ? 'Every day'
    : config.days.map(d => ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'][d]).join(', ');

  const frequency = config.frequency === 'hourly'
    ? 'every hour'
    : config.frequency === 'every10'
      ? 'every 10 min'
      : config.frequency === 'every15'
        ? 'every 15 min'
        : config.frequency === 'every30'
          ? 'every 30 min'
          : `at ${config.specificTime}`;

  return `${days} \u00B7 ${frequency} in ${timeZoneId} time`;
}

type FormatVariant = 'short' | 'medium' | 'date';

export function formatUtcShorthand(
  value: string | undefined | null,
  userTimeZone: string,
  variant: FormatVariant
): string {
  return formatUtcInTimeZone(
    value,
    userTimeZone,
    variant === 'short'
      ? { dateStyle: 'short', timeStyle: 'short' }
      : variant === 'medium'
        ? { dateStyle: 'medium', timeStyle: 'short' }
        : { dateStyle: 'medium' }
  );
}

export function formatUtcWithBoxContextShorthand(
  value: string | undefined | null,
  userTimeZone: string,
  boxTimeZoneId: string | undefined,
  variant: 'short' | 'medium'
): string {
  return formatUtcWithZoneContext(
    value,
    userTimeZone,
    boxTimeZoneId,
    variant === 'short'
      ? { dateStyle: 'short', timeStyle: 'short' }
      : { dateStyle: 'medium', timeStyle: 'short' },
    { timeStyle: 'short' }
  );
}