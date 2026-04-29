import "@mantine/core/styles.css";

import { createTheme, MantineProvider, Input, Button, Progress } from "@mantine/core";

import "./App.css";
import { useRef, useState } from "react";

const theme = createTheme({});

// TODO: move out of
const BASE_API_URL = import.meta.env.VITE_API_URL;

function App() {
  // processing state
  const [isProcessing, setIsProcessing] = useState<boolean>(false);
  // string to process
  const [text, setText] = useState<string>("");
  // processed response
  const [response, setResponse] = useState<string>("");
  // progress of the progress bar
  const [progress, setProgress] = useState<number>(0);

  const inputSizeRef = useRef<number>(-1);
  // for token cancellation
  const abortRef = useRef<AbortController | null>(null);
  /**
   * Handles input changes
   * @param event - Input change event
   */
  const onInputChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    setText(event.currentTarget.value);
  };

  /**
   * Handles button clicks
   * @param event - button click event
   */
  const onProcessClick = async (event: React.MouseEvent<HTMLButtonElement>) => {
    event.preventDefault();

    // set the processing flag
    setIsProcessing(true);
    // reset the state and UI
    setText("");
    setResponse("");
    setProgress(0);
    inputSizeRef.current = -1;

    await processUserInput(text);

    // once processing is done set isProcessing to false
    setIsProcessing(false);
  };

  /**
   * Handles button clicks
   * @param event - Cancel btn click event
   */
  const onCancelBtnClick = (event: React.MouseEvent<HTMLButtonElement>) => {
    event.preventDefault();

    setIsProcessing(false);

    // TODO: create a business logic function for it
    abortRef.current?.abort();
    console.log("Processing cancelled");
  };

  /**
   * Business logic - Process the user input
   * @param text - User text to process
   */
  const processUserInput = async (text: string) => {
    abortRef.current = new AbortController();

    try {
      // get the est
      const size = await getEstTime(text, abortRef.current);
      // update the progress bar size
      inputSizeRef.current = size;

      // start the streaming
      const processUrl = new URL(`InputProcessor`, BASE_API_URL);

      const response = await fetch(processUrl, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          userInput: text,
        }),
        signal: abortRef.current.signal,
      });

      const reader = response.body?.getReader();
      const decoder = new TextDecoder();

      if (!reader) throw new Error("No readable stream");

      let currentCharacter = 0;
      // read the stream until done
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        const decodedText = decoder.decode(value);

        // update the response one character at a time
        setResponse((prev) => prev + decodedText);
        currentCharacter++;

        // update the progress bar
        if (inputSizeRef.current > 0) {
          setProgress(Math.ceil((currentCharacter * 100) / inputSizeRef.current));
        }
      }
    } catch (err) {
      if (err instanceof DOMException && err.name === "AbortError") {
        console.log("Stream cancelled");
      } else {
        console.error("Stream error:", err);
      }
    }
  };

  /**
   * Gets estimated time (currently it's setting the progress bar)
   *
   * @param text - user input
   * @param abortController - For fetch/task cancellation
   */
  const getEstTime = async (text: string, abortController: AbortController): Promise<number> => {
    let result = -1;
    const estUrl = new URL(`InputProcessor/estimate`, BASE_API_URL);
    estUrl.searchParams.append("input", text);

    // get the est from the backend
    const estResponse = await fetch(estUrl, {
      method: "GET",
      headers: {
        "Content-Type": "application/json",
      },
      signal: abortController.signal,
    });

    const estData = (await estResponse.json()) as { size: number };
    result = estData.size;
    return result;
  };

  return (
    <MantineProvider theme={theme} defaultColorScheme="dark">
      <h1>Enter Your Input</h1>
      <Input placeholder="Input component" value={text} onChange={onInputChange} disabled={isProcessing} loading={isProcessing} />
      <Progress value={progress} />
      <section>
        <Button variant="outline" disabled={isProcessing || !text} onClick={onProcessClick} loading={isProcessing}>
          Process
        </Button>
        <Button variant="outline" color="red" onClick={onCancelBtnClick} disabled={!isProcessing}>
          Cancel
        </Button>
      </section>
      <>{response ? <div>Result: '{response}'</div> : null}</>
    </MantineProvider>
  );
}

export default App;
