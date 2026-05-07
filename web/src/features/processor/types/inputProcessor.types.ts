export type ProcessingEstimate = {
  size: number;
};

export type ProcessingJobResponse = {
  jobId: string;
};

export type ProcessedInputEvent = {
  requestId: string;
  id: number;
  isCompleted: boolean;
  progress: number;
  data: string;
};
