import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatCard } from '@angular/material/card';
import { AccountService } from '../../../core/services/account.service';
import { Router } from '@angular/router';
import { SnackbarService } from '../../../core/services/snackbar.service';
import { ValidationError } from '../../../shared/models/validationError';
import { TextInputComponent } from "../../../shared/components/text-input/text-input.component";

@Component({
  selector: 'app-register',
  imports: [
    ReactiveFormsModule,
    MatCard,
    MatButton,
    TextInputComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss'
})

export class RegisterComponent {
  private fb = inject(FormBuilder);
  private accountService = inject(AccountService);
  private router = inject(Router);
  private snackBar = inject(SnackbarService);
  validationErrors = signal<string[] | undefined>(undefined);

  registerForm = this.fb.nonNullable.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: ['', Validators.required],
    password: ['', Validators.required]
  })

  onSubmit() {
    this.accountService.register(this.registerForm.getRawValue()).subscribe({
      next: () => {
        this.snackBar.success('Registration successful');
        this.router.navigateByUrl('/account/login');
      },
      error: (err: unknown) => {
        if (err instanceof ValidationError) this.validationErrors.set(err.errors);
      }
    })
  }
}