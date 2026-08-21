/**
 * The backend returns image URLs as paths relative to the API's own origin
 * (e.g. "/uploads/products/5000/photo.jpg"), served via app.UseStaticFiles().
 * That's fine when the SPA and API share an origin, but in this deployment
 * they're on different ports (the web/nginx container vs the api container) -
 * a plain <img src={imageUrl}> would resolve against the SPA's own origin
 * instead, where nginx's SPA fallback silently serves index.html rather than
 * a 404, and the "image" fails to render with no obvious error.
 */
export function resolveImageUrl(url: string | null): string | null {
  if (!url) return null;
  if (/^https?:\/\//i.test(url)) return url;

  const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? '';
  const apiOrigin = apiBaseUrl.replace(/\/api\/v\d+\/?$/, '');
  return `${apiOrigin}${url}`;
}
