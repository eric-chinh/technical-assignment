import { describe, expect, it } from 'vitest';
import { toAppError } from './errors';

describe('toAppError', () => {
  it('maps a ProblemDetails body with field errors into fieldErrors', () => {
    const axiosError = {
      response: {
        status: 400,
        data: {
          title: 'Validation failed.',
          status: 400,
          errors: [{ propertyName: 'Name', errorMessage: 'Name is required.' }],
        },
      },
    };

    const result = toAppError(axiosError);

    expect(result.status).toBe(400);
    expect(result.message).toBe('Validation failed.');
    expect(result.fieldErrors).toEqual([{ propertyName: 'Name', errorMessage: 'Name is required.' }]);
  });

  it('maps a 500 ProblemDetails body, surfacing the traceId', () => {
    const axiosError = {
      response: {
        status: 500,
        data: { title: 'An unexpected error occurred.', status: 500, traceId: 'abc-123' },
      },
    };

    const result = toAppError(axiosError);

    expect(result.status).toBe(500);
    expect(result.traceId).toBe('abc-123');
  });

  it('falls back to a generic message when there is no response at all (network failure)', () => {
    const axiosError = { response: undefined, message: 'Network Error' };

    const result = toAppError(axiosError);

    expect(result.status).toBe(0);
    expect(result.message).toBe('Network Error');
  });
});
