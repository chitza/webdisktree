import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'scans' },
  {
    path: 'scans/new',
    loadComponent: () => import('./features/scan-start/scan-start').then((m) => m.ScanStart),
  },
  {
    path: 'scans/:id',
    loadComponent: () => import('./features/scan-detail/scan-detail').then((m) => m.ScanDetail),
  },
  {
    path: 'scans',
    loadComponent: () => import('./features/scan-history/scan-history').then((m) => m.ScanHistory),
  },
  {
    path: 'schedules',
    loadComponent: () => import('./features/schedules/schedules').then((m) => m.Schedules),
  },
  {
    path: 'imdb-cache',
    loadComponent: () => import('./features/imdb-cache/imdb-cache').then((m) => m.ImdbCache),
  },
  { path: '**', redirectTo: 'scans' },
];
