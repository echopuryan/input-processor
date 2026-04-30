import { Input, Button, Progress, Text } from "@mantine/core";
import { useState } from "react";

import styles from "./InputProcessor.module.css";

import { userInputProcessor } from "../hooks/useInputProcessor";

/**
 * Component for input processing
 */
function InputProcessor() {
  // string to process
  const [text, setText] = useState<string>("");

  const { response, progress, isProcessing, error, processInput, cancel } = userInputProcessor();

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

    await processInput(text);
  };

  /**
   * Handles button clicks
   * @param event - Cancel btn click event
   */
  const onCancelBtnClick = (event: React.MouseEvent<HTMLButtonElement>) => {
    event.preventDefault();

    cancel();
  };

  return (
    <div className={styles.container}>
      <h1>Process Your Text</h1>
      <div className={styles.input}>
        <Input placeholder="Input component" value={text} onChange={onInputChange} disabled={isProcessing} loading={isProcessing} />
        <Progress value={progress} className={styles.progress} />
      </div>

      {error && <Text size="md">{error}</Text>}
      <section className={styles.actions}>
        <Button variant="outline" disabled={isProcessing || !text} onClick={onProcessClick} loading={isProcessing}>
          Process
        </Button>
        <Button variant="outline" color="red" onClick={onCancelBtnClick} disabled={!isProcessing}>
          Cancel
        </Button>
      </section>
      <>
        {response ? (
          <div className={styles.response}>
            <span>
              <Text>Processing input:</Text> <Text c="orange"> '{text}' </Text>
            </span>
            <Text c="blue">'{response}'</Text>
          </div>
        ) : null}
      </>
    </div>
  );
}

export default InputProcessor;
