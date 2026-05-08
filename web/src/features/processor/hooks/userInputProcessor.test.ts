import { renderHook, act, waitFor } from "@testing-library/react";
import { userInputProcessor } from "./useInputProcessor";
import { inputProcessorServices } from "../services/inputProcessorServices";
import { getErrorMessage } from "../../../shared/utils/getErrorMessage";
import { buildUrl } from "../../../shared/services/apiClient";
import type { ProcessedInputEvent } from "../types/inputProcessor.types";

// Mock dependencies
vi.mock("../services/inputProcessorServices");
vi.mock("../../../shared/utils/getErrorMessage");
vi.mock("../../../shared/services/apiClient");

// Mock EventSource
class MockEventSource {
  url: string;
  onmessage: ((event: MessageEvent) => void) | null = null;
  onerror: ((event: Event) => void) | null = null;
  close = vi.fn();

  constructor(url: string) {
    this.url = url;
    MockEventSource.instances.push(this);
  }

  static instances: MockEventSource[] = [];
  static reset() {
    MockEventSource.instances = [];
  }
  static get latest() {
    return MockEventSource.instances[MockEventSource.instances.length - 1];
  }

  simulateMessage(data: ProcessedInputEvent) {
    this.onmessage?.(new MessageEvent("message", { data: JSON.stringify(data) }));
  }

  simulateError() {
    this.onerror?.(new Event("error"));
  }
}

describe("userInputProcessor", () => {
  beforeEach(() => {
    vi.stubGlobal("EventSource", MockEventSource);
    MockEventSource.reset();
    vi.mocked(getErrorMessage).mockReturnValue("Something went wrong.");
    vi.mocked(buildUrl).mockReturnValue(new URL("http://localhost/api/InputProcessor/123/stream"));
    // Default: no pending job on mount
    vi.mocked(inputProcessorServices.getPendingJobId).mockResolvedValue(null);
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
      vi.mocked(inputProcessorServices.startProcessingJob).mockResolvedValue("job-1");

      const { result } = renderHook(() => userInputProcessor());

      // First call
      await act(async () => {
        await result.current.processInput("first");
      });

      act(() => {
        MockEventSource.latest.simulateMessage({ id: 0, data: "x", progress: 100, isCompleted: true, requestId: "job-1", isCancelled: false });
      });

      expect(result.current.response).toBe("x");

      // Second call - state should reset
      vi.mocked(inputProcessorServices.startProcessingJob).mockResolvedValue("job-2");

      await act(async () => {
        await result.current.processInput("second");
      });

      expect(result.current.response).toBe("");
      expect(result.current.error).toBeNull();
      expect(result.current.progress).toBe(0);
    });

    it("calls startProcessingJob with text and abort signal", async () => {
      vi.mocked(inputProcessorServices.startProcessingJob).mockResolvedValue("job-1");

      const { result } = renderHook(() => userInputProcessor());

      await act(async () => {
        await result.current.processInput("hello world");
      });

      expect(inputProcessorServices.startProcessingJob).toHaveBeenCalledWith("hello world", expect.any(AbortSignal));
    });

    it("creates EventSource with correct URL", async () => {
      vi.mocked(inputProcessorServices.startProcessingJob).mockResolvedValue("job-abc");
      vi.mocked(buildUrl).mockReturnValue(new URL("http://localhost/api/InputProcessor/job-abc/stream"));

      const { result } = renderHook(() => userInputProcessor());

      await act(async () => {
        await result.current.processInput("test");
      });

      expect(buildUrl).toHaveBeenCalledWith("InputProcessor/job-abc/stream");
      expect(MockEventSource.latest.url).toBe("http://localhost/api/InputProcessor/job-abc/stream");
    });

    it("accumulates response from SSE events", async () => {
      vi.mocked(inputProcessorServices.startProcessingJob).mockResolvedValue("job-1");

      const { result } = renderHook(() => userInputProcessor());

      await act(async () => {
        await result.current.processInput("test");
      });

      act(() => {
        MockEventSource.latest.simulateMessage({ id: 0, data: "H", progress: 33, isCompleted: false, requestId: "job-1", isCancelled: false });
        MockEventSource.latest.simulateMessage({ id: 1, data: "i", progress: 66, isCompleted: false, requestId: "job-1", isCancelled: false });
        MockEventSource.latest.simulateMessage({ id: 2, data: "!", progress: 100, isCompleted: true, requestId: "job-1", isCancelled: false });
      });

      expect(result.current.response).toBe("Hi!");
    });

    it("updates progress from SSE events", async () => {
      vi.mocked(inputProcessorServices.startProcessingJob).mockResolvedValue("job-1");

      const { result } = renderHook(() => userInputProcessor());

      await act(async () => {
        await result.current.processInput("test");
      });

      act(() => {
        MockEventSource.latest.simulateMessage({ id: 0, data: "a", progress: 50, isCompleted: false, requestId: "job-1", isCancelled: false });
      });

      expect(result.current.progress).toBe(50);

      act(() => {
        MockEventSource.latest.simulateMessage({ id: 1, data: "b", progress: 100, isCompleted: true, requestId: "job-1", isCancelled: false });
      });

      expect(result.current.progress).toBe(100);
    });

    it("sets isProcessing to false and closes stream when job completes", async () => {
      vi.mocked(inputProcessorServices.startProcessingJob).mockResolvedValue("job-1");

      const { result } = renderHook(() => userInputProcessor());

      await act(async () => {
        await result.current.processInput("test");
      });

      expect(result.current.isProcessing).toBe(true);

      act(() => {
        MockEventSource.latest.simulateMessage({ id: 0, data: "a", progress: 100, isCompleted: true, requestId: "job-1", isCancelled: false });
      });

      expect(result.current.isProcessing).toBe(false);
      expect(MockEventSource.latest.close).toHaveBeenCalled();
    });
  });

  // ERROR HANDLING

  describe("processInput - errors", () => {
    it("sets error message from getErrorMessage on failure", async () => {
      vi.mocked(inputProcessorServices.startProcessingJob).mockRejectedValue(new Error("Network failure"));
      vi.mocked(getErrorMessage).mockReturnValue("Something went wrong. Please try again.");

      const { result } = renderHook(() => userInputProcessor());

      await act(async () => {
        await result.current.processInput("test");
      });

      expect(result.current.error).toBe("Something went wrong. Please try again.");
    });

    it("does not set error when getErrorMessage returns null (user cancelled)", async () => {
      vi.mocked(inputProcessorServices.startProcessingJob).mockRejectedValue(new DOMException("Aborted", "AbortError"));
      vi.mocked(getErrorMessage).mockReturnValue(null);

      const { result } = renderHook(() => userInputProcessor());

      await act(async () => {
        await result.current.processInput("test");
      });

      expect(result.current.error).toBeNull();
    });

    it("logs error to console.error when not user-cancelled", async () => {
      const consoleSpy = vi.spyOn(console, "error").mockImplementation(() => {});
      const thrownError = new Error("fail");

      vi.mocked(inputProcessorServices.startProcessingJob).mockRejectedValue(thrownError);
      vi.mocked(getErrorMessage).mockReturnValue("Error message");

      const { result } = renderHook(() => userInputProcessor());

      await act(async () => {
        await result.current.processInput("test");
      });

      expect(consoleSpy).toHaveBeenCalledWith("Processing error:", thrownError);
    });

    it("does not log to console when user cancels", async () => {
      const consoleSpy = vi.spyOn(console, "error").mockImplementation(() => {});

      vi.mocked(inputProcessorServices.startProcessingJob).mockRejectedValue(new DOMException("Aborted", "AbortError"));
      vi.mocked(getErrorMessage).mockReturnValue(null);

      const { result } = renderHook(() => userInputProcessor());

      await act(async () => {
        await result.current.processInput("test");
      });

      expect(consoleSpy).not.toHaveBeenCalled();
    });

    it("closes stream and sets isProcessing to false on EventSource error", async () => {
      vi.mocked(inputProcessorServices.startProcessingJob).mockResolvedValue("job-1");
      const consoleSpy = vi.spyOn(console, "error").mockImplementation(() => {});

      const { result } = renderHook(() => userInputProcessor());

      await act(async () => {
        await result.current.processInput("test");
      });

      expect(result.current.isProcessing).toBe(true);

      act(() => {
        MockEventSource.latest.simulateError();
      });

      expect(result.current.isProcessing).toBe(false);
      expect(MockEventSource.latest.close).toHaveBeenCalled();
    });
  });

  // CANCEL

  describe("cancel", () => {
    it("calls cancel service with jobId and closes stream", async () => {
      vi.mocked(inputProcessorServices.startProcessingJob).mockResolvedValue("job-99");
      vi.mocked(inputProcessorServices.cancel).mockResolvedValue(undefined);

      const { result } = renderHook(() => userInputProcessor());

      await act(async () => {
        await result.current.processInput("test");
      });

      await act(async () => {
        await result.current.cancel();
      });

      expect(inputProcessorServices.cancel).toHaveBeenCalledWith("job-99");
      expect(MockEventSource.latest.close).toHaveBeenCalled();
    });

    it("sets isProcessing to false on cancel", async () => {
      vi.mocked(inputProcessorServices.startProcessingJob).mockResolvedValue("job-1");
      vi.mocked(inputProcessorServices.cancel).mockResolvedValue(undefined);

      const { result } = renderHook(() => userInputProcessor());

      await act(async () => {
        await result.current.processInput("test");
      });

      expect(result.current.isProcessing).toBe(true);

      await act(async () => {
        await result.current.cancel();
      });

      expect(result.current.isProcessing).toBe(false);
    });

    it("does not throw when called with no active processing", async () => {
      const { result } = renderHook(() => userInputProcessor());

      await expect(
        act(async () => {
          await result.current.cancel();
        })
      ).resolves.not.toThrow();
    });
  });

  // RECONNECT ON MOUNT

  describe("reconnect on mount", () => {
    it("resumes streaming when getPendingJobId returns an active job", async () => {
      vi.mocked(inputProcessorServices.getPendingJobId).mockResolvedValue({
        jobId: "restored-job",
        requestInput: "hello",
      });
      vi.mocked(buildUrl).mockReturnValue(new URL("http://localhost/api/InputProcessor/restored-job/stream"));

      const { result } = renderHook(() => userInputProcessor());

      await waitFor(() => {
        expect(result.current.isProcessing).toBe(true);
      });

      expect(MockEventSource.latest.url).toBe("http://localhost/api/InputProcessor/restored-job/stream");
      expect(buildUrl).toHaveBeenCalledWith("InputProcessor/restored-job/stream");
    });

    it("does not create EventSource when no pending job exists", async () => {
      vi.mocked(inputProcessorServices.getPendingJobId).mockResolvedValue(null);

      renderHook(() => userInputProcessor());

      await waitFor(() => {
        expect(inputProcessorServices.getPendingJobId).toHaveBeenCalled();
      });

      expect(MockEventSource.instances).toHaveLength(0);
    });
  });
});