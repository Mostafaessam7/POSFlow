import { describe, expect, it, beforeEach } from 'vitest';

import {
  clearAccessToken,
  getAccessToken,
  getCsrfToken,
  setAccessToken
} from './auth.session';

describe('auth session storage', () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    clearAccessToken();
  });

  it('never persists the access token to localStorage or sessionStorage', () => {
    // The single property this whole change exists to guarantee. If someone "helpfully" adds
    // persistence here later so the session survives a reload, the credential becomes readable by
    // any script on the page again and the HttpOnly refresh cookie stops mattering.
    setAccessToken('a-real-looking-access-token');

    expect(getAccessToken()).toBe('a-real-looking-access-token');
    expect(Object.keys(localStorage)).toHaveLength(0);
    expect(Object.keys(sessionStorage)).toHaveLength(0);
  });

  it('clears the token from memory on logout', () => {
    setAccessToken('a-real-looking-access-token');
    clearAccessToken();

    expect(getAccessToken()).toBeNull();
  });

  it('reads the CSRF token from the cookie the server sets', () => {
    // This one is *meant* to be script-readable: the client echoes it back in a header, which is
    // what an attacker's cross-origin page cannot do.
    document.cookie = 'XSRF-TOKEN=abc123';

    expect(getCsrfToken()).toBe('abc123');
  });

  it('returns null when no CSRF cookie is present', () => {
    document.cookie = 'XSRF-TOKEN=; expires=Thu, 01 Jan 1970 00:00:00 GMT';

    expect(getCsrfToken()).toBeNull();
  });
});
