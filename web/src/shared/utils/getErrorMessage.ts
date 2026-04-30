import { ApiError } from "../services/apiClient";

/**
 * Known error codes and their description
 */
const STATUS_MESSAGES: Record<number, string> = {
  429: "Too many requests. Please wait a moment and try again.",
  500: "Server error. Please try again later.",
  503: "Service unavailable. Please try again later.",
};

const DEFAULT_MESSAGE = "Something went wrong. Please try again.";

/**
 * Return nice formatted error message
 * 
 * @param error - Error object from the API
 * @returns - Nicely formatted error message
 */
export function getErrorMessage(error: unknown): string | null {
  // User cancelled - not an error
  if (error instanceof DOMException && error.name === "AbortError") {
    return null;
  }

  if (error instanceof ApiError) {
    return STATUS_MESSAGES[error.status] ?? DEFAULT_MESSAGE;
  }

  return DEFAULT_MESSAGE;
}
