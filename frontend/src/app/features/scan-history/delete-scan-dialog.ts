import { Component, inject, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';

@Component({
  selector: 'app-delete-scan-dialog',
  imports: [MatDialogModule, MatButtonModule, MatCheckboxModule],
  template: `
    <h2 mat-dialog-title>{{ data.isPinned ? 'Delete pinned scan?' : 'Delete scan?' }}</h2>
    <mat-dialog-content>
      <p>Delete the scan of "{{ data.rootPath }}"? This cannot be undone.</p>
      <p>This deletes the saved scan. Your scanned files stay on disk.</p>
      @if (data.isPinned) {
        <mat-checkbox (change)="acknowledged.set($event.checked)">
          I understand this scan is pinned and want to delete it.
        </mat-checkbox>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button [mat-dialog-close]="false">Cancel</button>
      <button mat-flat-button [mat-dialog-close]="true"
        [disabled]="data.isPinned && !acknowledged()">Delete scan</button>
    </mat-dialog-actions>
  `,
})
export class DeleteScanDialog {
  readonly data = inject<{ rootPath: string; isPinned: boolean }>(MAT_DIALOG_DATA);
  readonly acknowledged = signal(false);
}
