const etags = new Map<string, string>();

export function setETag(url: string, etag: string | undefined): void {
  if (etag) etags.set(url, etag);
}

export function getETag(url: string): string | undefined {
  return etags.get(url);
}
