export interface FieldError {
  propertyName: string;
  errorMessage: string;
}

export interface AppError {
  status: number;
  message: string;
  fieldErrors?: FieldError[];
  traceId?: string;
  /**
   * The raw response body, always preserved. Most endpoints return a
   * ProblemDetails-shaped error (title/errors/traceId, extracted into the
   * fields above) - but the stock-adjustment endpoint deliberately does
   * NOT (backend spec section 7: insufficient stock isn't an exception,
   * so its 409 body is the plain AdjustStockResult shape, not
   * ProblemDetails). Callers that know they're calling an endpoint with a
   * non-standard error body read it from here instead of the fields above.
   */
  raw?: unknown;
}

interface AxiosLikeError {
  response?: { status: number; data?: Record<string, unknown> };
  message?: string;
}

export function toAppError(error: AxiosLikeError): AppError {
  if (!error.response) {
    return { status: 0, message: error.message ?? 'Network error' };
  }

  const { status, data } = error.response;
  const title = (data?.title as string | undefined) ?? 'An error occurred.';
  const fieldErrors = data?.errors as FieldError[] | undefined;
  const traceId = data?.traceId as string | undefined;

  return { status, message: title, fieldErrors, traceId, raw: data };
}
