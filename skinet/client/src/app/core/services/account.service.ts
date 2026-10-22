import { computed, inject, Injectable, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { HttpClient, HttpParams } from '@angular/common/http';
import { User } from '../../shared/models/user';
import { LoginCredentials } from '../../shared/models/loginCredentials';
import { RegisterCredentials } from '../../shared/models/registerCredentials';
import { catchError, map, of, switchMap, tap } from 'rxjs';
import { Address } from '../../shared/models/address';
import { SignalrService } from './signalr.service';
import { CartService } from './cart.service';

@Injectable({
  providedIn: 'root'
})
export class AccountService {
  private readonly baseUrl = environment.apiUrl;
  private http = inject(HttpClient);
  private signalrService = inject(SignalrService);
  private cartService = inject(CartService);
  private _currentUser = signal<User | null>(null);
  readonly currentUser = this._currentUser.asReadonly();
  isAdmin = computed(() => this.currentUser()?.roles.includes('Admin') ?? false);

  login(values: LoginCredentials) {
    // useCookies=true: Identity answers with an auth cookie instead of a bearer token.
    let params = new HttpParams();
    params = params.append('useCookies', true);

    return this.http.post<User>(this.baseUrl + 'login', values, { params }).pipe(
      tap(() => this.signalrService.createHubConnection()),
      // The cart loads with the session — the mirror of clearCurrentUser() (BUG-18).
      // catchError: a cart that fails to load must not fail the login.
      switchMap((response) =>
        this.cartService.getCartAsync().pipe(
          catchError(() => of(null)),
          map(() => response)
        )
      )
    );
  }

  register(values: RegisterCredentials) {
    return this.http.post(this.baseUrl + 'account/register', values);
  }

  // Nullable: the endpoint answers an unauthenticated caller with 204, and an empty body is null.
  getUserInfo() {
    return this.http.get<User | null>(this.baseUrl + 'account/user-info').pipe(
      map((user) => {
        this._currentUser.set(user);
        return user;
      })
    );
  }

  logout() {
    return this.http
      .post(this.baseUrl + 'account/logout', {})
      .pipe(tap(() => this.clearCurrentUser()));
  }

  // Drops client auth state without an API call — used on logout and on a 401.
  // The cart goes too: it belongs to the previous user.
  clearCurrentUser() {
    this._currentUser.set(null);
    this.signalrService.stopHubConnection();
    this.cartService.clearLocalCart();
  }

  updateAddress(address: Address) {
    return this.http.post(this.baseUrl + 'account/address', address).pipe(
      tap(() => {
        this._currentUser.update((user) => {
          if (user) user.address = address;
          return user;
        });
      })
    );
  }

  getAuthState() {
    return this.http.get<{ isAuthenticated: boolean }>(this.baseUrl + 'account/auth-status');
  }
}
