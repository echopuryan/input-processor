import { useCallback, useRef, useState } from "react";
import { inputProcessorServices } from "../services/inputProcessorServices";
import { getErrorMessage } from "../../../shared/utils/getErrorMessage";

export function userInputProcessor() {
  const [response, setResponse] = useState("");
  const [progress, setProgress] = useState(0);
  const [isProcessing, setIsProcessing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const abortControllerRef = useRef<AbortController | null>(null);

  /**
   * Process user input
   */
  const processInput = useCallback(async (text: string) => {
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
  }, []);

  return {
    response,
    progress,
    isProcessing,
    error,
    processInput,
    cancel,
  };
}
