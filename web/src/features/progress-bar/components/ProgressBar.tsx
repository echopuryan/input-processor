import styles from "./ProgressBar.module.css";
import { type ProgressBarProps } from "../types/ProgressBarProps";

/**
 * Simple progress bar that takes in a percentage value (from 0 - 100)
 * 
 * @param props - Props containing current progress value
 * @returns - Progress bar component
 */
function ProgressBar(props: ProgressBarProps) {
  const { progress } = props;

  return (
    <div className={styles.container}>
      <div className={styles.fill} style={{ width: `${progress}%` }}></div>
    </div>
  );
}

export default ProgressBar;
