import { apiClient } from "../../../shared/services/apiClient";
import type { ProcessingJobResponse, JobInfo } from "../types/inputProcessor.types";

export const inputProcessorServices = {
  /**
   * Starts processing and returns the job ID
   *
   * @param text - User text to process
   * @param signal - Abort controller to cancel the API call
   */
  startProcessingJob: async (text: string, signal: AbortSignal): Promise<string> => {
    const response = await apiClient.post<ProcessingJobResponse>("InputProcessor/Process", {
      body: { userInput: text },
      signal,
    });

    return response.jobId;
  },

  /**
   * Returns current user's pending job
   * 
   * @param signal - Abort controller to cancel the API call
   * @returns - Currently logged in user's pending job
   */
  getPendingJobId: async (signal: AbortSignal): Promise<JobInfo | null> => {
    const response = await apiClient.get<JobInfo | null>("InputProcessor/pending-job", undefined, {
      signal,
    });

    return response;
  },

  /**
   * Cancel the long running job
   * @param jobId - Unique ID of the job.
   */
  cancel: (jobId: string) => {
    return apiClient.post(`InputProcessor/${jobId}/cancel`);
  },
};
