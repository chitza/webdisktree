import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';

export interface ScanProgressEvent {
  scanId: string;
  filesScanned: number;
  dirsScanned: number;
  bytesScanned: number;
  currentPath: string;
}

export interface ScanTerminalEvent {
  scanId: string;
  errorMessage?: string;
}

@Injectable({ providedIn: 'root' })
export class ScanProgressService {
  private connection: signalR.HubConnection | null = null;
  private connectPromise: Promise<void> | null = null;

  readonly progress$ = new Subject<ScanProgressEvent>();
  readonly completed$ = new Subject<ScanTerminalEvent>();
  readonly failed$ = new Subject<ScanTerminalEvent>();
  readonly cancelled$ = new Subject<ScanTerminalEvent>();

  private ensureConnected(): Promise<void> {
    if (!this.connection) {
      this.connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/scan-progress')
        .withAutomaticReconnect()
        .build();

      this.connection.on('ScanProgress', (event: ScanProgressEvent) => this.progress$.next(event));
      this.connection.on('ScanCompleted', (event: ScanTerminalEvent) => this.completed$.next(event));
      this.connection.on('ScanFailed', (event: ScanTerminalEvent) => this.failed$.next(event));
      this.connection.on('ScanCancelled', (event: ScanTerminalEvent) => this.cancelled$.next(event));
    }

    if (!this.connectPromise) {
      this.connectPromise = this.connection.start().catch((err) => {
        this.connectPromise = null;
        throw err;
      });
    }

    return this.connectPromise;
  }

  async joinScan(scanId: string): Promise<void> {
    await this.ensureConnected();
    await this.connection!.invoke('JoinScanGroup', scanId);
  }

  async leaveScan(scanId: string): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      return;
    }
    await this.connection.invoke('LeaveScanGroup', scanId);
  }
}
