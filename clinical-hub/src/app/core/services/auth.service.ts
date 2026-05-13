import { Injectable } from '@angular/core';

const TOKEN_KEY = 'access_token';

export type UserRole = 'patient' | 'staff' | 'admin';

interface JwtPayload {
  sub: string;
  role: UserRole;
  exp: number;
  [key: string]: unknown;
}

@Injectable({ providedIn: 'root' })
export class AuthService {

  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  setToken(token: string): void {
    localStorage.setItem(TOKEN_KEY, token);
  }

  clearToken(): void {
    localStorage.removeItem(TOKEN_KEY);
  }

  /**
   * Decodes the JWT payload. Returns null on any malformed input — never throws.
   */
  decodeToken(token: string): JwtPayload | null {
    try {
      const parts = token.split('.');
      if (parts.length !== 3) { return null; }
      // Normalise Base64URL → Base64 (RFC 7519 §3: `-` → `+`, `_` → `/`)
      const b64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
      const payload = JSON.parse(atob(b64)) as JwtPayload;
      if (typeof payload !== 'object' || payload === null) { return null; }
      return payload;
    } catch {
      return null;
    }
  }

  isTokenExpired(token: string): boolean {
    const payload = this.decodeToken(token);
    if (!payload || typeof payload['exp'] !== 'number') { return true; }
    return Date.now() >= payload['exp'] * 1000;
  }

  getCurrentRole(): UserRole | null {
    const token = this.getToken();
    if (!token) { return null; }
    const payload = this.decodeToken(token);
    const role = payload?.['role'];
    if (role === 'patient' || role === 'staff' || role === 'admin') {
      return role;
    }
    return null;
  }

  isAuthenticated(): boolean {
    const token = this.getToken();
    if (!token) { return false; }
    return !this.isTokenExpired(token);
  }
}
