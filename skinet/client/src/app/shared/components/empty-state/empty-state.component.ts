import { Component, inject, input, output } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { BusyService } from '../../../core/services/busy.service';

@Component({
  selector: 'app-empty-state',
  imports: [MatIcon, MatButton],
  templateUrl: './empty-state.component.html',
  styleUrl: './empty-state.component.scss'
})

  // Inside Cart component
  // Displayed when the cart is empty
export class EmptyStateComponent {
  busyService = inject(BusyService); // to not display the empty cart before all products are loaded
  message = input.required<string>();
  icon = input.required<string>();
  actionText = input.required<string>();
  action = output<void>();


  onAction() {
    this.action.emit();
  }
}