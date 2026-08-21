import { describe, expect, it, beforeEach } from 'vitest';
import MockAdapter from 'axios-mock-adapter';
import { axiosClient } from './axiosClient';
import { setETag, getETag } from './etagStore';

describe('axiosClient interceptors', () => {
  let mock: MockAdapter;

  beforeEach(() => {
    mock = new MockAdapter(axiosClient);
  });

  it('captures the ETag response header for a GET and stores it by URL', async () => {
    mock.onGet('/products/1').reply(200, { id: 1 }, { etag: '"42"' });

    await axiosClient.get('/products/1');

    expect(getETag('/products/1')).toBe('"42"');
  });

  it('attaches If-Match from the stored ETag on a PUT to the same URL', async () => {
    setETag('/products/2', '"99"');
    mock.onPut('/products/2').reply((config) => {
      expect(config.headers?.['If-Match']).toBe('"99"');
      return [200, { id: 2 }];
    });

    await axiosClient.put('/products/2', { name: 'x' });
  });

  it('rejects with a normalized AppError on a 409 response', async () => {
    mock.onPut('/products/3').reply(409, { title: 'Conflict.', status: 409 });

    await expect(axiosClient.put('/products/3', {})).rejects.toMatchObject({ status: 409, message: 'Conflict.' });
  });
});
