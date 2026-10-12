import axios from "axios";

// Every API error is a problem response (RFC 9457); `detail` is the
// human-readable explanation, safe to show the user.
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
}

// The `detail` of a failed request's problem response, or the fallback for
// anything else (a network error, a response without one).
export function problemDetail(error: unknown, fallback: string): string {
  if (!axios.isAxiosError<ProblemDetails>(error)) return fallback;
  return error.response?.data?.detail ?? fallback;
}
