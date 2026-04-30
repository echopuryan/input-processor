import { apiClient, ApiError } from "./apiClient";

// Mock the constant
vi.mock("../constants/api", () => ({
  BASE_API_URL: "http://localhost:5000/api/",
}));

describe("apiClient", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  // GET

  describe("get", () => {
    it("makes a GET request to the correct URL", async () => {
      const mockData = { id: 1, name: "Test" };
      vi.spyOn(globalThis, "fetch").mockResolvedValue(Response.json(mockData, { status: 200 }));

      await apiClient.get("items");

      expect(fetch).toHaveBeenCalledWith(
        new URL("items", "http://localhost:5000/api/"),
        expect.objectContaining({
          method: "GET",
          headers: { "Content-Type": "application/json" },
        }),
      );
    });

    it("returns parsed JSON on success", async () => {
      const mockData = { id: 1, name: "Test" };
      vi.spyOn(globalThis, "fetch").mockResolvedValue(Response.json(mockData, { status: 200 }));

      const result = await apiClient.get<typeof mockData>("items");

      expect(result).toEqual(mockData);
    });

    it("appends query params to the URL", async () => {
      vi.spyOn(globalThis, "fetch").mockResolvedValue(Response.json({}, { status: 200 }));

      await apiClient.get("search", { q: "hello", page: "2" });

      const calledUrl = vi.mocked(fetch).mock.calls[0][0] as URL;
      expect(calledUrl.searchParams.get("q")).toBe("hello");
      expect(calledUrl.searchParams.get("page")).toBe("2");
    });

    it("passes custom headers", async () => {
      vi.spyOn(globalThis, "fetch").mockResolvedValue(Response.json({}, { status: 200 }));

      await apiClient.get("items", undefined, {
        headers: { Authorization: "Bearer token123" },
      });

      expect(fetch).toHaveBeenCalledWith(
        expect.anything(),
        expect.objectContaining({
          headers: {
            "Content-Type": "application/json",
            Authorization: "Bearer token123",
          },
        }),
      );
    });

    it("passes the abort signal", async () => {
      const controller = new AbortController();
      vi.spyOn(globalThis, "fetch").mockResolvedValue(Response.json({}, { status: 200 }));

      await apiClient.get("items", undefined, { signal: controller.signal });

      expect(fetch).toHaveBeenCalledWith(
        expect.anything(),
        expect.objectContaining({
          signal: controller.signal,
        }),
      );
    });

    it("throws ApiError with status on non-ok response", async () => {
      vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response(null, { status: 404, statusText: "Not Found" }));

      await expect(apiClient.get("missing")).rejects.toThrow(ApiError);
      await expect(apiClient.get("missing")).rejects.toMatchObject({
        status: 404,
        statusText: "Not Found",
      });
    });

    it("throws ApiError with parsed body when error response has JSON", async () => {
      const errorBody = { message: "Resource not found" };
      vi.spyOn(globalThis, "fetch").mockResolvedValue(Response.json(errorBody, { status: 404, statusText: "Not Found" }));

      try {
        await apiClient.get("missing");
      } catch (err) {
        expect(err).toBeInstanceOf(ApiError);
        expect((err as ApiError).body).toEqual(errorBody);
      }
    });

    it("throws ApiError without body when error response has no JSON", async () => {
      vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response("plain text", { status: 500, statusText: "Internal Server Error" }));

      try {
        await apiClient.get("fail");
      } catch (err) {
        expect(err).toBeInstanceOf(ApiError);
        expect((err as ApiError).body).toBeUndefined();
      }
    });

    it("throws ApiError on 429 rate limit", async () => {
      vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response(null, { status: 429, statusText: "Too Many Requests" }));

      await expect(apiClient.get("items")).rejects.toMatchObject({
        status: 429,
        statusText: "Too Many Requests",
      });
    });
  });

  // POST

  describe("post", () => {
    it("makes a POST request with JSON body", async () => {
      const payload = { name: "New Item" };
      vi.spyOn(globalThis, "fetch").mockResolvedValue(Response.json({ id: 1 }, { status: 201 }));

      await apiClient.post("items", { body: payload });

      expect(fetch).toHaveBeenCalledWith(
        expect.anything(),
        expect.objectContaining({
          method: "POST",
          body: JSON.stringify(payload),
        }),
      );
    });

    it("returns parsed JSON on success", async () => {
      const responseData = { id: 1, name: "Created" };
      vi.spyOn(globalThis, "fetch").mockResolvedValue(Response.json(responseData, { status: 201 }));

      const result = await apiClient.post<typeof responseData>("items", {
        body: { name: "Created" },
      });

      expect(result).toEqual(responseData);
    });

    it("sends no body when body option is undefined", async () => {
      vi.spyOn(globalThis, "fetch").mockResolvedValue(Response.json({}, { status: 200 }));

      await apiClient.post("trigger");

      expect(fetch).toHaveBeenCalledWith(
        expect.anything(),
        expect.objectContaining({
          body: undefined,
        }),
      );
    });

    it("passes custom headers and abort signal", async () => {
      const controller = new AbortController();
      vi.spyOn(globalThis, "fetch").mockResolvedValue(Response.json({}, { status: 200 }));

      await apiClient.post("items", {
        body: { name: "test" },
        headers: { "X-Custom": "value" },
        signal: controller.signal,
      });

      expect(fetch).toHaveBeenCalledWith(
        expect.anything(),
        expect.objectContaining({
          headers: {
            "Content-Type": "application/json",
            "X-Custom": "value",
          },
          signal: controller.signal,
        }),
      );
    });

    it("throws ApiError on non-ok response", async () => {
      vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response(null, { status: 400, statusText: "Bad Request" }));

      await expect(apiClient.post("items", { body: { invalid: true } })).rejects.toMatchObject({
        status: 400,
        statusText: "Bad Request",
      });
    });
  });

  // POST STREAM

  describe("postStream", () => {
    it("makes a POST request and returns a stream reader", async () => {
      const stream = new ReadableStream({
        start(controller) {
          controller.enqueue(new TextEncoder().encode("chunk1"));
          controller.close();
        },
      });

      vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response(stream, { status: 200 }));

      const reader = await apiClient.postStream("stream", {
        body: { input: "test" },
      });

      const { value } = await reader.read();
      expect(new TextDecoder().decode(value)).toBe("chunk1");
    });

    it("reads multiple chunks from the stream", async () => {
      const stream = new ReadableStream({
        start(controller) {
          controller.enqueue(new TextEncoder().encode("first"));
          controller.enqueue(new TextEncoder().encode("second"));
          controller.close();
        },
      });

      vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response(stream, { status: 200 }));

      const reader = await apiClient.postStream("stream", {
        body: { input: "test" },
      });

      const chunk1 = await reader.read();
      const chunk2 = await reader.read();
      const done = await reader.read();

      expect(new TextDecoder().decode(chunk1.value)).toBe("first");
      expect(new TextDecoder().decode(chunk2.value)).toBe("second");
      expect(done.done).toBe(true);
    });

    it("sends JSON body correctly", async () => {
      const stream = new ReadableStream({
        start(controller) {
          controller.close();
        },
      });

      vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response(stream, { status: 200 }));

      const payload = { userInput: "hello" };
      await apiClient.postStream("stream", { body: payload });

      expect(fetch).toHaveBeenCalledWith(
        expect.anything(),
        expect.objectContaining({
          method: "POST",
          body: JSON.stringify(payload),
        }),
      );
    });

    it("throws ApiError on non-ok response", async () => {
      vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response(null, { status: 429, statusText: "Too Many Requests" }));

      await expect(apiClient.postStream("stream", { body: { input: "test" } })).rejects.toMatchObject({
        status: 429,
        statusText: "Too Many Requests",
      });
    });

    it("throws Error when response has no readable stream", async () => {
      const response = new Response(null, { status: 200 });
      // Override body to simulate null stream
      Object.defineProperty(response, "body", { value: null });

      vi.spyOn(globalThis, "fetch").mockResolvedValue(response);

      await expect(apiClient.postStream("stream", { body: { input: "test" } })).rejects.toThrow("No readable stream available");
    });

    it("passes abort signal", async () => {
      const controller = new AbortController();
      const stream = new ReadableStream({
        start(c) {
          c.close();
        },
      });

      vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response(stream, { status: 200 }));

      await apiClient.postStream("stream", {
        body: { input: "test" },
        signal: controller.signal,
      });

      expect(fetch).toHaveBeenCalledWith(
        expect.anything(),
        expect.objectContaining({
          signal: controller.signal,
        }),
      );
    });
  });
});
