export interface Schedule {
  id: string;
  rootPath: string;
  cronExpression: string;
  enabled: boolean;
  lastRunAt: string | null;
  nextRunAt: string | null;
}

export interface CreateScheduleRequest {
  rootPath: string;
  cronExpression: string;
  enabled: boolean;
}
