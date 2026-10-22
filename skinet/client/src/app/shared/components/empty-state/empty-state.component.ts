import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { BusyService } from '../../../core/services/busy.service';

@Component({
  selector: 'app-empty-state',
  imports: [MatIcon, MatButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './empty-state.component.html',
  styleUrl: './empty-state.component.scss'
})
// Shown when the cart or the shop list is empty.
export class EmptyStateComponent {
  busyService = inject(BusyService); // hidden while a request is in flight
  message = input.required<string>();
  icon = input.required<string>();
  actionText = input.required<string>();
  action = output<void>();

  onAction() {
    this.action.emit();
  }
}
