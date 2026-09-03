import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'formatCount' })
export class FormatCountPipe implements PipeTransform {
  transform(value: number | null | undefined): string {
    if (value === null || value === undefined || Number.isNaN(value)) return '—';
    return value.toLocaleString('de-DE');
  }
}
