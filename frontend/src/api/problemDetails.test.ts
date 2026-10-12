import { describe, expect, it } from "vitest";
import { AxiosError, AxiosHeaders } from "axios";
import { problemDetail } from "./problemDetails";

function failedRequest(status: number, data: unknown): AxiosError {
  const config = { headers: new AxiosHeaders() };
  return new AxiosError("Request failed", "ERR_BAD_REQUEST", config, null, {
    status,
    statusText: "",
    headers: {},
    config,
    data,
  });
}

describe("problemDetail", () => {
  it("returns the problem response's detail", () => {
    const error = failedRequest(400, { status: 400, title: "Bad Request", detail: "Value must be a number." });

    expect(problemDetail(error, "Save failed.")).toBe("Value must be a number.");
  });

  it("falls back when the response has no detail", () => {
    expect(problemDetail(failedRequest(500, { status: 500, title: "An error occurred" }), "Save failed.")).toBe("Save failed.");
  });

  it("falls back for a request that got no response", () => {
    const networkError = new AxiosError("Network Error", "ERR_NETWORK");

    expect(problemDetail(networkError, "Save failed.")).toBe("Save failed.");
  });

  it("falls back for anything that isn't a request error", () => {
    expect(problemDetail(new Error("boom"), "Save failed.")).toBe("Save failed.");
  });
});
