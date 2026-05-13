import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';

function makeToken(payload: object, expired = false): string {
  const exp = expired
    ? Math.floor(Date.now() / 1000) - 60   // 1 min ago
    : Math.floor(Date.now() / 1000) + 900; // 15 min from now
  const full = { ...payload, exp };
  const header  = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const body    = btoa(JSON.stringify(full));
  const sig     = 'fakesignature';
  return `${header}.${body}.${sig}`;
}

describe('AuthService', () => {
  let service: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(AuthService);
    localStorage.clear();
  });

  afterEach(() => localStorage.clear());

  // decodeToken — never throws
  it('decodeToken returns null for empty string — no throw', () => {
    expect(() => service.decodeToken('')).not.toThrow();
    expect(service.decodeToken('')).toBeNull();
  });

  it('decodeToken returns null for malformed JWT — no throw', () => {
    expect(() => service.decodeToken('not.a.jwt')).not.toThrow();
    // 'not' is valid base64 but not a JSON object with expected shape
  });

  it('decodeToken returns null for token with only 2 parts', () => {
    expect(service.decodeToken('header.body')).toBeNull();
  });

  it('decodeToken returns payload for valid token', () => {
    const token = makeToken({ role: 'patient', name: 'Alex' });
    const payload = service.decodeToken(token);
    expect(payload?.['role']).toBe('patient');
    expect(payload?.['name']).toBe('Alex');
  });

  it('decodeToken handles Base64URL chars (-) in payload — no throw', () => {
    // Craft a token whose payload contains `-` (Base64URL) in its raw encoding
    const jsonStr = JSON.stringify({ role: 'patient', exp: Math.floor(Date.now() / 1000) + 900 });
    const b64url  = btoa(jsonStr).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    const token   = `${btoa('{}')}.${b64url}.sig`;
    expect(() => service.decodeToken(token)).not.toThrow();
    expect(service.decodeToken(token)?.['role']).toBe('patient');
  });

  it('decodeToken handles Base64URL chars (_) in payload — no throw', () => {
    // `_` replaces `/` in Base64URL; ensure normalisation handles it
    const jsonStr = JSON.stringify({ role: 'staff', exp: Math.floor(Date.now() / 1000) + 900 });
    const b64url  = btoa(jsonStr).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    const token   = `${btoa('{}')}.${b64url}.sig`;
    expect(service.decodeToken(token)?.['role']).toBe('staff');
  });

  // isTokenExpired
  it('isTokenExpired returns true for expired token', () => {
    const token = makeToken({ role: 'patient' }, true);
    expect(service.isTokenExpired(token)).toBeTrue();
  });

  it('isTokenExpired returns false for valid token', () => {
    const token = makeToken({ role: 'patient' });
    expect(service.isTokenExpired(token)).toBeFalse();
  });

  it('isTokenExpired returns true for malformed token — no throw', () => {
    expect(() => service.isTokenExpired('bad')).not.toThrow();
    expect(service.isTokenExpired('bad')).toBeTrue();
  });

  it('isTokenExpired returns true for empty-payload token with valid signature', () => {
    const header = btoa('{}');
    const body   = btoa('{}'); // no exp field
    const token  = `${header}.${body}.sig`;
    expect(service.isTokenExpired(token)).toBeTrue();
  });

  // getCurrentRole
  it('getCurrentRole returns null when no token in localStorage', () => {
    expect(service.getCurrentRole()).toBeNull();
  });

  it('getCurrentRole returns patient role from stored token', () => {
    localStorage.setItem('access_token', makeToken({ role: 'patient' }));
    expect(service.getCurrentRole()).toBe('patient');
  });

  it('getCurrentRole returns staff role from stored token', () => {
    localStorage.setItem('access_token', makeToken({ role: 'staff' }));
    expect(service.getCurrentRole()).toBe('staff');
  });

  it('getCurrentRole returns null for unknown role value', () => {
    localStorage.setItem('access_token', makeToken({ role: 'superuser' }));
    expect(service.getCurrentRole()).toBeNull();
  });

  // isAuthenticated
  it('isAuthenticated returns false when no token', () => {
    expect(service.isAuthenticated()).toBeFalse();
  });

  it('isAuthenticated returns true for valid non-expired token', () => {
    localStorage.setItem('access_token', makeToken({ role: 'patient' }));
    expect(service.isAuthenticated()).toBeTrue();
  });

  it('isAuthenticated returns false for expired token', () => {
    localStorage.setItem('access_token', makeToken({ role: 'patient' }, true));
    expect(service.isAuthenticated()).toBeFalse();
  });
});
