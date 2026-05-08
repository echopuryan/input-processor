import { useCallback, useEffect, useRef, useState } from "react";
import { inputProcessorServices } from "../services/inputProcessorServices";
import { getErrorMessage } from "../../../shared/utils/getErrorMessage";
import { buildUrl } from "../../../shared/services/apiClient";
import type { ProcessedInputEvent } from "../types/inputProcessor.types";

export function userInputProcessor() {
  const [response, setResponse] = useState("");
  const [progress, setProgress] = useState(0);
  const [isProcessing, setIsProcessing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const abortControllerRef = useRef<AbortController | null>(null);
  const sourceRef = useRef<EventSource | null>(null);
  const inputTextRef = useRef<string>("");
  const currentJobId = useRef<string>("");

  /**
   * Reset state
   */
  const resetState = useCallback(() => {
    // reset state
    setResponse("");
    setProgress(0);
    setError(null);
    setIsProcessing(false);
    currentJobId.current = "";
  }, []);

  useEffect(() => {
    const reconnect = async () => {
      const abortController = new AbortController();
      abortControllerRef.current = abortController;

      try {
        const currentJob = await inputProcessorServices.getPendingJobId(abortController.signal);
        // no active job found for the current user
        if (!currentJob || !currentJob?.jobId) return;
        // event source has already started (double use effect call)
        if (sourceRef.current) return;

        const { requestInput: text, jobId } = currentJob;
        // stream the events.
        streamEvents(text, jobId);
      } catch (err) {
        console.error("Failed to check for active jobs:", err);
      }
    };

    reconnect();
  }, []);

  /**
   * Stream the events
   */
  const streamEvents = useCallback((text: string, jobId: string) => {
    currentJobId.current = jobId;
    // get the events/data as the BG job processes it
    const sseUrl = buildUrl(`InputProcessor/${jobId}/stream`);
    sourceRef.current = new EventSource(sseUrl.toString());

    inputTextRef.current = text;

    setIsProcessing(true);

    // process each message
    sourceRef.current.onmessage = (event: MessageEvent) => {
      const data = JSON.parse(event.data) as ProcessedInputEvent;
      // set the response
      setResponse((prev) => prev + data.data);
      // set the progress
      setProgress(data.progress);

      // once job is done, close it
      if (data.isCompleted) {
        console.info(`Input '${text}' job ID: ${jobId} finished processing. Closing the stream...`);
        sourceRef.current?.close();
        setIsProcessing(false);
      }
    };

    sourceRef.current.onerror = async (event: Event) => {
      console.error(event);
      // stop the processing
      setIsProcessing(false);
      // send the abort signal (not very useful here since we cancel the job separately)
      abortControllerRef.current?.abort();
      // close the stream (will not stop the job)
      sourceRef?.current?.close();
    };
  }, []);

  /**
   * Main function to start the processing job and get the events
   */
  const processInput = useCallback(async (text: string) => {
    resetState();

    const abortController = new AbortController();
    abortControllerRef.current = abortController;

    try {
      // start the BG job and get the JOB id
      const jobId = await inputProcessorServices.startProcessingJob(text, abortController.signal);
      // stream the events
      streamEvents(text, jobId);
    } catch (err) {
      const message = getErrorMessage(err);

      // null means user cancelled - not an error
      if (message === null) return;

      setError(message);
      console.error("Processing error:", err);
    }

    return () => {
      sourceRef.current?.close();
      setIsProcessing(false);
      abortControllerRef.current = null;
    };
  }, []);

  /**
   * Cancel the processing
   */
  const cancel = useCallback(async () => {
    // cancel the job
    if (currentJobId.current) await inputProcessorServices.cancel(currentJobId.current);
    // stop the processing
    setIsProcessing(false);
    // send the abort signal (not very useful here since we cancel the job separately)
    abortControllerRef.current?.abort();
    // close the stream (will not stop the job)
    sourceRef?.current?.close();
  }, []);

  return {
    response,
    progress,
    isProcessing,
    error,
    processInput,
    cancel,
    inputTextRef,
  };
}
