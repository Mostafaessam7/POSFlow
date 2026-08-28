// The access token lives ONLY here, in module memory — never localStorage, never sessionStorage.
//
// That is the whole point of the change this file is part of. Anything in Web Storage is readable
// by any script on the page, so a single XSS bug, or one compromised npm package, hands over the
// session. A module-scoped variable is not reachable that way; the refresh token is not reachable
// at all, because it now lives in an HttpOnly cookie the browser attaches and JavaScript cannot see.
//
// The cost is deliberate and worth naming: a full page reload loses the in-memory access token, so
// the app performs one silent /api/auth/refresh on startup to re-establish the session from the
// cookie. That is one extra request on load, in exchange for a credential that cannot be stolen by
// script.
let accessToken: string | null = null;

export function setAccessToken(token: string | null): void {
  accessToken = token;
}

export function getAccessToken(): string | null {
  return accessToken;
}

export function clearAccessToken(): void {
  accessToken = null;
}

// The CSRF token is the one value that is *meant* to be readable by script: the server sets it as a
// non-HttpOnly cookie and the client echoes it back in a header. An attacker's page can make the
// browser send cookies cross-origin, but the same-origin policy stops it reading them, so it cannot
// produce the matching header. That asymmetry is the entire double-submit defence.
export function getCsrfToken(): string | null {
  const match = document.cookie.match(/(?:^|;\s*)XSRF-TOKEN=([^;]*)/);

  return match ? decodeURIComponent(match[1]) : null;
}
