export type ProcessingEstimate = {
  size: number;
};

export type ProcessingJobResponse = {
  jobId: string;
};

export type JobInfo = {
  jobId: string,
  owner: string,
  requestInput: string
}

export type ProcessedInputEvent = {
  requestId: string;
  id: number;
  isCompleted: boolean;
  progress: number;
  data: string;
};
