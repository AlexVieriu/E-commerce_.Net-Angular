import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatCard } from '@angular/material/card';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { AccountService } from '../../../core/services/account.service';
import { SnackbarService } from '../../../core/services/snackbar.service';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { switchMap } from 'rxjs';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, MatCard, MatFormField, MatInput, MatLabel, MatButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {
  private fb = inject(FormBuilder);
  private accountService = inject(AccountService);
  private router = inject(Router);
  private activatedRoute = inject(ActivatedRoute);
  private snackbar = inject(SnackbarService);
  returnUrl = '/shop';

  constructor() {
    const url = this.activatedRoute.snapshot.queryParams['returnUrl'];
    if (isLocalUrl(url)) this.returnUrl = url;
    else if (url) console.warn(`Rejected non-local returnUrl: ${url}`);
  }

  loginForm = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required]
  });

  // POST api/login comes from MapIdentityApi<AppUser>() in Program.cs.
  onSubmit() {
    this.accountService
      .login(this.loginForm.getRawValue())
      .pipe(switchMap(() => this.accountService.getUserInfo()))
      .subscribe({
        next: () => this.router.navigateByUrl(this.returnUrl),
        // The interceptor stays silent on a 401, so the wrong password needs its own message.
        error: (err: HttpErrorResponse) => {
          if (err.status === 401) this.snackbar.error('Invalid email or password.');
        }
      });
  }
}

// C0 controls, DEL and C1 — what .NET's char.IsControl covers.
function hasControlCharacter(value: string): boolean {
  for (const character of value) {
    const code = character.charCodeAt(0);
    // Examples: tab 0x09, newline 0x0a, carriage return 0x0d, DEL 0x7f.
    if (code <= 0x1f || (code >= 0x7f && code <= 0x9f)) return true;
  }
  return false;
}

// True only for a path on this origin, so a returnUrl from the query string cannot point off-site.
// Same rule as ASP.NET Core's SharedUrlHelper.IsLocalUrl: "/foo" passes, "//" and "/\" do not.
// Control characters are rejected because browsers strip tab, CR and LF while parsing a URL.
function isLocalUrl(url: unknown): url is string {
  if (typeof url !== 'string' || url.length === 0) return false;
  if (url[0] !== '/') return false;
  if (url.length === 1) return true;
  if (url[1] === '/' || url[1] === '\\') return false;
  return !hasControlCharacter(url);
}
