import { apiClient } from "../../../shared/services/apiClient";
import type { ProcessingEstimate, ProcessingJobResponse } from "../types/inputProcessor.types";

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

  //#region legacy functions
  /**
   * Starts processing and returns the stream
   * @param text - User text to process
   * @param signal - Abort controller to cancel the API call
   */
  startProcessingStream: async (text: string, signal: AbortSignal): Promise<ReadableStreamDefaultReader<Uint8Array>> => {
    return apiClient.postStream("InputProcessor", {
      body: { userInput: text },
      signal,
    });
  },

  /**
   * Gets estimated time (currently it's setting the progress bar)
   *
   * @param text - user input
   * @param abortController - For fetch/task cancellation
   */
  getProcessingEst: async (text: string, signal: AbortSignal): Promise<number> => {
    const data = await apiClient.get<ProcessingEstimate>("InputProcessor/estimate", { input: text }, { signal });
    return data.size;
  },
  //#endregion

  /**
   * Cancel the processing (will cancel the pending API request)
   * @param abortController - Abort controller to cancel the API call
   */
  cancel: (abortController: AbortController | null) => {
    abortController?.abort();
  },
};
