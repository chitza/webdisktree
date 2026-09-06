import { Component, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'app-unpin-scan-dialog',
  imports: [MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Unpin scan?</h2>
    <mat-dialog-content>
      <p>Unpin the scan of "{{ data.rootPath }}"?</p>
      <p>Deleting this scan will no longer require the extra pinned-scan acknowledgement.</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button [mat-dialog-close]="false">Cancel</button>
      <button mat-flat-button [mat-dialog-close]="true">Unpin</button>
    </mat-dialog-actions>
  `,
})
export class UnpinScanDialog {
  readonly data = inject<{ rootPath: string }>(MAT_DIALOG_DATA);
}
