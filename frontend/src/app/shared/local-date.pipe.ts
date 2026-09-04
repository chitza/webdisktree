import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'localDate' })
export class LocalDatePipe implements PipeTransform {
  // Passing no locale to Intl.DateTimeFormat resolves to the browser's own locale, unlike
  // Angular's DatePipe which always formats using the app's fixed LOCALE_ID (en-US).
  private readonly formatter = new Intl.DateTimeFormat(undefined, {
    dateStyle: 'short',
    timeStyle: 'short',
  });

  transform(value: string | Date | null | undefined): string {
    if (!value) return '';
    const date = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(date.getTime())) return '';
    return this.formatter.format(date);
  }
}
