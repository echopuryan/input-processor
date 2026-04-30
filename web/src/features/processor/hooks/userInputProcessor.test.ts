import { renderHook, act } from "@testing-library/react";
import { userInputProcessor } from "./useInputProcessor";
import { inputProcessorServices } from "../services/inputProcessorServices";
import { getErrorMessage } from "../../../shared/utils/getErrorMessage";

// Mock dependencies
vi.mock("../services/inputProcessorServices");
vi.mock("../../../shared/utils/getErrorMessage");

// Helper: creates a mock ReadableStream reader from chunks
function createMockReader(chunks: string[]) {
  let index = 0;
  return {
    read: vi.fn(async () => {
      if (index < chunks.length) {
        const value = new TextEncoder().encode(chunks[index]);
        index++;
        return { done: false, value };
      }
      return { done: true, value: undefined };
    }),
  } as unknown as ReadableStreamDefaultReader<Uint8Array>;
}

describe("userInputProcessor", () => {
  beforeEach(() => {
    vi.mocked(getErrorMessage).mockReturnValue("Something went wrong.");
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  // INITIAL STATE

  describe("initial state", () => {
    it("returns correct default values", () => {
      const { result } = renderHook(() => userInputProcessor());

      expect(result.current.response).toBe("");
      expect(result.current.progress).toBe(0);
      expect(result.current.isProcessing).toBe(false);
      expect(result.current.error).toBeNull();
      expect(result.current.processInput).toBeInstanceOf(Function);
      expect(result.current.cancel).toBeInstanceOf(Function);
    });
  });

  // SUCCESSFUL PROCESSING

  describe("processInput - success", () => {
    it("resets state before processing", async () => {
      const reader = createMockReader(["data"]);
      vi.mocked(inputProcessorServices.getProcessingEst).mockResolvedValue(4);
      vi.mocked(inputProcessorServices.startProcessingStream).mockResolvedValue(reader);

      const { result } = renderHook(() => userInputProcessor());

      // Run first time
      await act(async () => {
        await result.current.processInput("first");
      });

      expect(result.current.response).toBe("data");

      // Run second time — state should reset
      const reader2 = createMockReader(["new"]);
      vi.mocked(inputProcessorServices.getProcessingEst).mockResolvedValue(3);
      vi.mocked(inputProcessorServices.startProcessingStream).mockResolvedValue(reader2);

      await act(async () => {
        await result.current.processInput("second");
      });

      expect(result.current.response).toBe("new");
      expect(result.current.error).toBeNull();
    });

    it("calls getProcessingEst with text and abort signal", async () => {
      const reader = createMockReader([]);
      vi.mocked(inputProcessorServices.getProcessingEst).mockResolvedValue(0);
      vi.mocked(inputProcessorServices.startProcessingStream).mockResolvedValue(reader);

      const { result } = renderHook(() => userInputProcessor());

      await act(async () => {
        await result.current.processInput("hello world");
      });

      expect(inputProcessorServices.getProcessingEst).toHaveBeenCalledWith("hello world", expect.any(AbortSignal));
    });

    it("calls startProcessingStream with text and abort signal", async () => {
      const reader = createMockReader([]);
      vi.mocked(inputProcessorServices.getProcessingEst).mockResolvedValue(0);
      vi.mocked(inputProcessorServices.startProcessingStream).mockResolvedValue(reader);

      const { result } = renderHook(() => userInputProcessor());

      await act(async () => {
        await result.current.processInput("hello world");
      });

      expect(inputProcessorServices.startProcessingStream).toHaveBeenCalledWith("hello world", expect.any(AbortSignal));
    });

    it("accumulates response from stream chunks", async () => {
      const reader = createMockReader(["He", "llo", " World"]);
      vi.mocked(inputProcessorServices.getProcessingEst).mockResolvedValue(10);
      vi.mocked(inputProcessorServices.startProcessingStream).mockResolvedValue(reader);

      const { result } = renderHook(() => userInputProcessor());

      await act(async () => {
        await result.current.processInput("test");
      });

      expect(result.current.response).toBe("Hello World");
    });

    it("calculates progress based on chunks read vs estimated size", async () => {
      const reader = createMockReader(["a", "b", "c", "d"]);
      vi.mocked(inputProcessorServices.getProcessingEst).mockResolvedValue(4);
      vi.mocked(inputProcessorServices.startProcessingStream).mockResolvedValue(reader);

      const { result } = renderHook(() => userInputProcessor());

      await act(async () => {
        await result.current.processInput("test");
      });

      // 4 chunks / 4 estimated = 100%
      expect(result.current.progress).toBe(100);
    });

    it("calculates partial progress correctly", async () => {
      const reader = createMockReader(["a", "b"]);
      vi.mocked(inputProcessorServices.getProcessingEst).mockResolvedValue(4);
      vi.mocked(inputProcessorServices.startProcessingStream).mockResolvedValue(reader);

      const { result } = renderHook(() => userInputProcessor());

      await act(async () => {
        await result.current.processInput("test");
      });

      // 2 chunks / 4 estimated = 50%
      expect(result.current.progress).toBe(50);
    });

    it("uses Math.ceil for progress calculation", async () => {
      const reader = createMockReader(["a"]);
      vi.mocked(inputProcessorServices.getProcessingEst).mockResolvedValue(3);
      vi.mocked(inputProcessorServices.startProcessingStream).mockResolvedValue(reader);

      const { result } = renderHook(() => userInputProcessor());

      await act(async () => {
        await result.current.processInput("test");
      });

      // ceil(1 * 100 / 3) = ceil(33.33) = 34
      expect(result.current.progress).toBe(34);
    });

    it("does not update progress when estimated size is 0", async () => {
      const reader = createMockReader(["a", "b"]);
      vi.mocked(inputProcessorServices.getProcessingEst).mockResolvedValue(0);
      vi.mocked(inputProcessorServices.startProcessingStream).mockResolvedValue(reader);

      const { result } = renderHook(() => userInputProcessor());

      await act(async () => {
        await result.current.processInput("test");
      });

      expect(result.current.progress).toBe(0);
    });

    it("sets isProcessing to false when stream completes", async () => {
      const reader = createMockReader(["done"]);
      vi.mocked(inputProcessorServices.getProcessingEst).mockResolvedValue(4);
      vi.mocked(inputProcessorServices.startProcessingStream).mockResolvedValue(reader);

      const { result } = renderHook(() => userInputProcessor());

      await act(async () => {
        await result.current.processInput("test");
      });

      expect(result.current.isProcessing).toBe(false);
    });
  });

  // ERROR HANDLING

  describe("processInput - errors", () => {
    it("sets error message from getErrorMessage on failure", async () => {
      vi.mocked(inputProcessorServices.getProcessingEst).mockRejectedValue(new Error("Network failure"));
      vi.mocked(getErrorMessage).mockReturnValue("Something went wrong. Please try again.");

      const { result } = renderHook(() => userInputProcessor());

      await act(async () => {
        await result.current.processInput("test");
      });

      expect(result.current.error).toBe("Something went wrong. Please try again.");
      expect(result.current.isProcessing).toBe(false);
    });

    it("passes the caught error to getErrorMessage", async () => {
      const thrownError = new Error("API down");
      vi.mocked(inputProcessorServices.getProcessingEst).mockRejectedValue(thrownError);
      vi.mocked(getErrorMessage).mockReturnValue("error msg");

      const { result } = renderHook(() => userInputProcessor());

      await act(async () => {
        await result.current.processInput("test");
      });

      expect(getErrorMessage).toHaveBeenCalledWith(thrownError);
    });

    it("does not set error when getErrorMessage returns null (user cancelled)", async () => {
      vi.mocked(inputProcessorServices.getProcessingEst).mockRejectedValue(new DOMException("Aborted", "AbortError"));
      vi.mocked(getErrorMessage).mockReturnValue(null);

      const { result } = renderHook(() => userInputProcessor());

      await act(async () => {
        await result.current.processInput("test");
      });

      expect(result.current.error).toBeNull();
    });

    it("handles error from startProcessingStream", async () => {
      vi.mocked(inputProcessorServices.getProcessingEst).mockResolvedValue(5);
      vi.mocked(inputProcessorServices.startProcessingStream).mockRejectedValue(new Error("Stream failed"));
      vi.mocked(getErrorMessage).mockReturnValue("Stream error occurred.");

      const { result } = renderHook(() => userInputProcessor());

      await act(async () => {
        await result.current.processInput("test");
      });

      expect(result.current.error).toBe("Stream error occurred.");
      expect(result.current.isProcessing).toBe(false);
    });

    it("handles error thrown during stream reading", async () => {
      const reader = {
        read: vi
          .fn()
          .mockResolvedValueOnce({ done: false, value: new TextEncoder().encode("ok") })
          .mockRejectedValueOnce(new Error("Stream interrupted")),
      } as unknown as ReadableStreamDefaultReader<Uint8Array>;

      vi.mocked(inputProcessorServices.getProcessingEst).mockResolvedValue(5);
      vi.mocked(inputProcessorServices.startProcessingStream).mockResolvedValue(reader);
      vi.mocked(getErrorMessage).mockReturnValue("Connection lost.");

      const { result } = renderHook(() => userInputProcessor());

      await act(async () => {
        await result.current.processInput("test");
      });

      expect(result.current.response).toBe("ok");
      expect(result.current.error).toBe("Connection lost.");
      expect(result.current.isProcessing).toBe(false);
    });

    it("logs error to console.error", async () => {
      const consoleSpy = vi.spyOn(console, "error").mockImplementation(() => {});
      const thrownError = new Error("fail");

      vi.mocked(inputProcessorServices.getProcessingEst).mockRejectedValue(thrownError);
      vi.mocked(getErrorMessage).mockReturnValue("Error message");

      const { result } = renderHook(() => userInputProcessor());

      await act(async () => {
        await result.current.processInput("test");
      });

      expect(consoleSpy).toHaveBeenCalledWith("Processing error:", thrownError);
    });

    it("does not log to console when user cancels", async () => {
      const consoleSpy = vi.spyOn(console, "error").mockImplementation(() => {});

      vi.mocked(inputProcessorServices.getProcessingEst).mockRejectedValue(new DOMException("Aborted", "AbortError"));
      vi.mocked(getErrorMessage).mockReturnValue(null);

      const { result } = renderHook(() => userInputProcessor());

      await act(async () => {
        await result.current.processInput("test");
      });

      expect(consoleSpy).not.toHaveBeenCalled();
    });
  });

  // CANCEL

  describe("cancel", () => {
    it("aborts the active request", async () => {
      let abortSignal: AbortSignal | undefined;

      vi.mocked(inputProcessorServices.getProcessingEst).mockImplementation(async (_text, signal) => {
        abortSignal = signal;
        // simulate a long-running request
        return new Promise((resolve) => setTimeout(resolve, 10000));
      });
      vi.mocked(getErrorMessage).mockReturnValue(null);

      const { result } = renderHook(() => userInputProcessor());

      // Start processing (don't await — it's in-flight)
      act(() => {
        result.current.processInput("test");
      });

      // Cancel
      act(() => {
        result.current.cancel();
      });

      expect(abortSignal?.aborted).toBe(true);
    });

    it("sets isProcessing to false", async () => {
      vi.mocked(inputProcessorServices.getProcessingEst).mockImplementation(() => new Promise((resolve) => setTimeout(resolve, 10000)));
      vi.mocked(getErrorMessage).mockReturnValue(null);

      const { result } = renderHook(() => userInputProcessor());

      act(() => {
        result.current.processInput("test");
      });

      expect(result.current.isProcessing).toBe(true);

      act(() => {
        result.current.cancel();
      });

      expect(result.current.isProcessing).toBe(false);
    });

    it("does not throw when called with no active processing", () => {
      const { result } = renderHook(() => userInputProcessor());

      expect(() => {
        act(() => {
          result.current.cancel();
        });
      }).not.toThrow();
    });
  });

  // ABORT SIGNAL ISOLATION

  describe("abort controller lifecycle", () => {
    it("creates a new abort controller for each processInput call", async () => {
      const signals: AbortSignal[] = [];

      vi.mocked(inputProcessorServices.getProcessingEst).mockImplementation(async (_text, signal) => {
        signals.push(signal);
        return 0;
      });
      vi.mocked(inputProcessorServices.startProcessingStream).mockResolvedValue(createMockReader([]));

      const { result } = renderHook(() => userInputProcessor());

      await act(async () => {
        await result.current.processInput("first");
      });

      await act(async () => {
        await result.current.processInput("second");
      });

      expect(signals).toHaveLength(2);
      expect(signals[0]).not.toBe(signals[1]);
    });
  });
});
