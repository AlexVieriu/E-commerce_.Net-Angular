import { computed, inject, Injectable, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { HttpClient, HttpParams } from '@angular/common/http';
import { User } from '../../shared/models/user';
import { LoginCredentials } from '../../shared/models/loginCredentials';
import { RegisterCredentials } from '../../shared/models/registerCredentials';
import { map, tap } from 'rxjs';
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
    // build HTTP parameters (query string parameters) for the request
    let params = new HttpParams();

    // adding a parameter named 'useCookies' with the value 'true'
    params = params.append('useCookies', true);

    return this.http
      .post<User>(this.baseUrl + 'login', values, { params })
      .pipe(tap(() => this.signalrService.createHubConnection()));
  }

  register(values: RegisterCredentials) {
    return this.http.post(this.baseUrl + 'account/register', values);
  }

  getUserInfo() {
    return this.http.get<User>(this.baseUrl + 'account/user-info').pipe(
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

  // Drop client-side auth state without an API call. Used when a request 401s
  // (cookie expired or cleared out-of-band) so the UI reflects logged-out state.
  // The cart goes with it — it is the previous user's, whichever path got us here.
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
