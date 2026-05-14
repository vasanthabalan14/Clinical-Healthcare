import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface AdminUser {
  id: number;
  email: string;
  firstName: string;
  lastName: string;
  role: 'admin' | 'staff';
  isActive: boolean;
  lastLoginAt: string | null;
}

export interface CreateUserPayload {
  email: string;
  firstName: string;
  lastName: string;
  role: 'admin' | 'staff';
}

export interface UpdateUserPayload {
  firstName?: string;
  lastName?: string;
  isActive?: boolean;
}

export interface CreateUserResponse {
  message: string;
  userId: number;
}

@Injectable({ providedIn: 'root' })
export class AdminService {

  private base = `${environment.apiBaseUrl}/admin/users`;

  constructor(private http: HttpClient) {}

  getUsers(): Observable<AdminUser[]> {
    return this.http.get<AdminUser[]>(this.base);
  }

  createUser(payload: CreateUserPayload): Observable<CreateUserResponse> {
    return this.http.post<CreateUserResponse>(this.base, payload);
  }

  updateUser(id: number, payload: UpdateUserPayload): Observable<void> {
    return this.http.patch<void>(`${this.base}/${id}`, payload);
  }
}
