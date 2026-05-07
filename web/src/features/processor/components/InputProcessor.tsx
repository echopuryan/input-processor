import { Input, Button, Text } from "@mantine/core";
import { useState } from "react";

import styles from "./InputProcessor.module.css";

import { userInputProcessor } from "../hooks/useInputProcessor";

import ProgressBar from "../../progress-bar/components/ProgressBar";

/**
 * Component for input processing
 */
function InputProcessor() {
  // string to process
  const [text, setText] = useState<string>("");

  const { response, progress, isProcessing, error, processInput, cancel, inputTextRef } = userInputProcessor();

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

    // inputTextRef.current = text;
    await processInput(text);
  };

  /**
   * Handles button clicks
   * @param event - Cancel btn click event
   */
  const onCancelBtnClick = async(event: React.MouseEvent<HTMLButtonElement>) => {
    event.preventDefault();

    await cancel();
  };

  return (
    <div className={styles.wrapper}>
      <div className={styles.container}>
        <h1>Process Your Text</h1>
        <div className={styles.input}>
          <Input placeholder="Input component" value={text} onChange={onInputChange} disabled={isProcessing} loading={isProcessing} />
          <ProgressBar progress={progress} />
        </div>

        {error && (
          <Text size="md" c="red">
            {error}
          </Text>
        )}
        <section className={styles.actions}>
          <Button variant="outline" disabled={isProcessing || !text} onClick={onProcessClick} loading={isProcessing}>
            Process
          </Button>
          <Button variant="outline" color="red" onClick={onCancelBtnClick} disabled={!isProcessing}>
            Cancel
          </Button>
        </section>
      </div>
      <div>
        {response ? (
          <div className={styles.response}>
            <Text>Processing input:</Text>
            <Text c="orange">'{inputTextRef.current}'</Text>
            <Text>{"-->"}</Text>
            <Text c="blue">'{response}'</Text>
          </div>
        ) : null}
      </div>
    </div>
  );
}

export default InputProcessor;
