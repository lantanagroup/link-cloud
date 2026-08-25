import { ToastrService } from 'ngx-toastr';

import { ErrorHandlingService } from './error-handling.service';

describe('ErrorHandlingService', () => {
  let service: ErrorHandlingService;
  let toastr: jasmine.SpyObj<ToastrService>;

  beforeEach(() => {
    toastr = jasmine.createSpyObj<ToastrService>('ToastrService', ['error']);
    service = new ErrorHandlingService(toastr);
  });

  function getRethrownError(error: unknown, genericToaster = false): unknown {
    let rethrownError: unknown;

    service.handleError(error, genericToaster).subscribe({
      error: (receivedError: unknown) => rethrownError = receivedError
    });

    return rethrownError;
  }

  it('formats sanitized structured problem details with a status and trace ID', () => {
    const error: any = {
      status: 503,
      error: {
        title: 'Upstream service at https://internal.example.test',
        status: 502,
        detail: 'Connection to 192.168.1.10 failed',
        traceId: 'trace https://traces.example.test'
      }
    };

    expect(getRethrownError(error)).toBe(error);
    expect(error.message).toBe('Connection to [IP] failed\nTrace ID: trace [URL]');
    expect(service.formatError(error, 'Fallback detail')).toBe(
      'Upstream service at [URL] (502): Connection to [IP] failed\nTrace ID: trace [URL]'
    );
  });

  it('uses a valid outer status when the problem details status is malformed', () => {
    const error = {
      status: 503,
      error: {
        title: 'Service unavailable',
        status: '503',
        detail: 'Retry later'
      }
    };

    getRethrownError(error);

    expect(service.formatError(error, 'Fallback detail')).toBe('Service unavailable (503): Retry later');
  });

  it('uses a sanitized fallback when structured fields are malformed', () => {
    const error = {
      message: { reason: 'invalid' },
      error: {
        title: { value: 'invalid' },
        status: '500',
        detail: ['invalid'],
        traceId: 42
      }
    };

    expect(service.formatError(error, 'Unable to reach 192.168.1.20')).toBe(
      'Error: Unable to reach [IP]'
    );
  });

  it('sanitizes unstructured error messages and shows the generic toaster', () => {
    const error = { message: 'Unexpected response from ftp://files.example.test' };

    expect(getRethrownError(error, true)).toBe(error);
    expect(error.message).toBe('Unexpected response from [URL]');
    expect(toastr.error).toHaveBeenCalledWith(
      'Unexpected response from [URL]',
      'Error',
      jasmine.objectContaining({ timeOut: 5000 })
    );
  });

  it('rethrows primitive errors as safe Error objects', () => {
    const rethrownError = getRethrownError('malformed error');

    expect(rethrownError).toEqual(jasmine.any(Error));
    expect((rethrownError as Error).message).toBe('An unknown error occurred');
  });
});