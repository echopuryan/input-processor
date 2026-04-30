import { ApiError } from "@/shared/services/apiClient";
import { getErrorMessage } from "./getErrorMessage";

describe("getErrorMessage", () => {
  it("returns null for AbortError", () => {
    const error = new DOMException("Aborted", "AbortError");
    expect(getErrorMessage(error)).toBeNull();
  });

  it("returns rate limit message for 429", () => {
    const error = new ApiError(429, "Too Many Requests");
    expect(getErrorMessage(error)).toBe(
      "Too many requests. Please wait a moment and try again."
    );
  });

  it("returns server error message for 500", () => {
    const error = new ApiError(500, "Internal Server Error");
    expect(getErrorMessage(error)).toBe(
      "Server error. Please try again later."
    );
  });

  it("returns default message for unknown ApiError status", () => {
    const error = new ApiError(418, "The answer is 42");
    expect(getErrorMessage(error)).toBe(
      "Something went wrong. Please try again."
    );
  });

  it("returns default message for generic errors", () => {
    expect(getErrorMessage(new Error("random"))).toBe(
      "Something went wrong. Please try again."
    );
  });
});