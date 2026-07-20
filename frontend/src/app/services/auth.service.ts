import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { Router } from '@angular/router';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);
  private baseUrl = 'http://localhost:5022/api/auth';

  private currentUserSubject = new BehaviorSubject<any>(null);
  public currentUser$ = this.currentUserSubject.asObservable();

  constructor() {
    const savedUser = localStorage.getItem('current_agent');
    if (savedUser) {
      try {
        this.currentUserSubject.next(JSON.parse(savedUser));
      } catch (e) {
        localStorage.removeItem('current_agent');
        localStorage.removeItem('auth_token');
      }
    }
  }

  public get currentUserValue(): any {
    return this.currentUserSubject.value;
  }

  public get token(): string | null {
    return localStorage.getItem('auth_token');
  }

  login(credentials: { email: string; password: string }): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/login`, credentials).pipe(
      tap(res => {
        if (res && res.token) {
          localStorage.setItem('auth_token', res.token);
          localStorage.setItem('current_agent', JSON.stringify(res.agent));
          this.currentUserSubject.next(res.agent);
        }
      })
    );
  }

  logout(): Observable<any> {
    // Fire-and-forget backend logout, but clean up client-side state immediately
    return this.http.post<any>(`${this.baseUrl}/logout`, {}).pipe(
      tap({
        finalize: () => {
          localStorage.removeItem('auth_token');
          localStorage.removeItem('current_agent');
          this.currentUserSubject.next(null);
          this.router.navigate(['/login']);
        }
      })
    );
  }

  isAuthenticated(): boolean {
    return !!this.token;
  }
}
