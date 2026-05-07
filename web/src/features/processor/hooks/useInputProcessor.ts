import { useCallback, useEffect, useRef, useState } from "react";
import { inputProcessorServices } from "../services/inputProcessorServices";
import { getErrorMessage } from "../../../shared/utils/getErrorMessage";
import { buildUrl } from "../../../shared/services/apiClient";
import type { ProcessedInputEvent } from "../types/inputProcessor.types";

export function userInputProcessor() {
  const jobStorageKey = "processingJob";

  const [response, setResponse] = useState("");
  const [progress, setProgress] = useState(0);
  const [isProcessing, setIsProcessing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const abortControllerRef = useRef<AbortController | null>(null);
  const sourceRef = useRef<EventSource | null>(null);
  const inputTextRef = useRef<string>("");

  /**
   * Reset state
   */
  const resetState = useCallback(() => {
    // reset state
    setResponse("");
    setProgress(0);
    setError(null);
    setIsProcessing(true);
  }, []);

  useEffect(() => {
    const currentJob = sessionStorage.getItem(jobStorageKey);

    // no current job exists in this current session
    if (!currentJob) return;

    // event source has already started (double use effect call)
    if (sourceRef.current) return;

    resetState();
    const { text, jobId } = JSON.parse(currentJob);
    // stream the events
    streamEvents(text, jobId);
  }, []);

  /**
   * Stream the events
   */
  const streamEvents = useCallback((text: string, jobId: string) => {
    // get the events/data as the BG job processes it
    const sseUrl = buildUrl(`InputProcessor/${jobId}/stream`);
    sourceRef.current = new EventSource(sseUrl.toString());

    inputTextRef.current = text;

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
        sessionStorage.clear();
      }
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
      // save the job ID
      sessionStorage.setItem(jobStorageKey, JSON.stringify({ jobId, text }));
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
   * Process user input
   */
  const processInputOld = useCallback(async (text: string) => {
    // reset state
    setResponse("");
    setProgress(0);
    setError(null);
    setIsProcessing(true);

    const abortController = new AbortController();
    abortControllerRef.current = abortController;

    try {
      // get the estimate
      const size = await inputProcessorServices.getProcessingEst(text, abortController.signal);

      // start the stream
      const reader = await inputProcessorServices.startProcessingStream(text, abortController.signal);

      // Read the stream
      const decoder = new TextDecoder();
      let currentCharacter = 0;

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        const decodedText = decoder.decode(value);
        setResponse((prev) => prev + decodedText);

        currentCharacter++;
        if (size > 0) {
          setProgress(Math.ceil((currentCharacter * 100) / size));
        }
      }
    } catch (err) {
      const message = getErrorMessage(err);

      // null means user cancelled - not an error
      if (message === null) return;

      setError(message);
      console.error("Processing error:", err);
    } finally {
      setIsProcessing(false);
      abortControllerRef.current = null;
    }
  }, []);

  /**
   * Cancel the processing
   */
  const cancel = useCallback(() => {
    // stop the processing
    setIsProcessing(false);
    // send the abort signal
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
    inputTextRef
  };
}
