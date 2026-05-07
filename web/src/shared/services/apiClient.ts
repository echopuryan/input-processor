import { BASE_API_URL } from "../constants/api";

/**
 * Request options interface
 */
interface RequestOptions {
  signal?: AbortSignal;
  headers?: Record<string, string>;
}

/**
 * Request with body
 */
interface RequestWithBodyOptions extends RequestOptions {
  body?: unknown;
}

/**
 * Custom Error class
 */
export class ApiError extends Error {
  status: number;
  statusText: string;
  body?: unknown;

  constructor(status: number, statusText: string, body?: unknown) {
    super(`API Error ${status}: ${statusText}`);
    this.name = "ApiError";
    this.status = status;
    this.statusText = statusText;
    this.body = body;
  }
}

/**
 * Handles response received from the API
 *
 * @param response - Response from the API
 * @returns Json promise
 */
async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    let body: unknown;
    try {
      body = await response.json();
    } catch {
      // no JSON body in error response
    }
    throw new ApiError(response.status, response.statusText, body);
  }

  return response.json() as Promise<T>;
}

/**
 * Helper method to create URL object from relative paths
 *
 * @param path - URL path
 * @param params - URL params
 * @returns URL object to be used by fetch
 */
export function buildUrl(path: string, params?: Record<string, string>): URL {
  const url = new URL(path, BASE_API_URL);
  if (params) {
    Object.entries(params).forEach(([key, value]) => {
      url.searchParams.append(key, value);
    });
  }
  return url;
}

// Main object that's exported
export const apiClient = {
  /**
   * Performs GET API calls
   *
   * @param path - GET request path
   * @param params - URL params if any
   * @param options - Request options (header, abort signal)
   * @returns - JSON response from the API
   */
  get: async <T>(path: string, params?: Record<string, string>, options?: RequestOptions): Promise<T> => {
    // build URL
    const url = buildUrl(path, params);

    // make the fetch call
    const response = await fetch(url, {
      method: "GET",
      headers: {
        "Content-Type": "application/json",
        ...options?.headers,
      },
      signal: options?.signal,
    });

    // return handled response
    return handleResponse<T>(response);
  },

  /**
   * Make a POST request
   *
   * @param path - POST request path
   * @param options Request options (header, abort signal)
   * @returns - JSON response from the API
   */
  post: async <T>(path: string, options?: RequestWithBodyOptions): Promise<T> => {
    // build URL
    const url = buildUrl(path);

    // make the fetch call
    const response = await fetch(url, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        ...options?.headers,
      },
      body: options?.body ? JSON.stringify(options.body) : undefined,
      signal: options?.signal,
    });

    // return handled response
    return handleResponse<T>(response);
  },

  /**
   * POST request that returns a stream
   *
   * @param path - POST request path
   * @param options - Request options (header, abort signal)
   * @returns - Returns stream reader object
   */
  postStream: async (path: string, options?: RequestWithBodyOptions): Promise<ReadableStreamDefaultReader<Uint8Array>> => {
    // build URL
    const url = buildUrl(path);

    // make the fetch call
    const response = await fetch(url, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        ...options?.headers,
      },
      body: options?.body ? JSON.stringify(options.body) : undefined,
      signal: options?.signal,
    });

    if (!response.ok) {
      throw new ApiError(response.status, response.statusText);
    }

    const reader = response.body?.getReader();
    if (!reader) throw new Error("No readable stream available");

    return reader;
  },
};
