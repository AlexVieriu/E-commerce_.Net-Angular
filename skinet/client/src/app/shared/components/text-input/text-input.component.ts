import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { ControlValueAccessor, FormControl, NgControl, ReactiveFormsModule } from '@angular/forms';
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';

@Component({
  selector: 'app-text-input',
  imports: [ReactiveFormsModule, MatFormField, MatInput, MatError, MatLabel],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './text-input.component.html',
  styleUrl: './text-input.component.scss'
})
export class TextInputComponent implements ControlValueAccessor {
  controlDir = inject(NgControl, { self: true });
  label = input('');
  type = input('text');

  constructor() {
    this.controlDir.valueAccessor = this;
  }

  // ControlValueAccessor needs these, but the template binds `control` directly, so they do nothing.
  writeValue(): void {
    // no-op
  }
  registerOnChange(): void {
    // no-op
  }
  registerOnTouched(): void {
    // no-op
  }

  get control() {
    return this.controlDir.control as FormControl;
  }
}
