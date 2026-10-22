import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatCard } from '@angular/material/card';
import { ApiErrorResponse } from '../../models/apiErrorResponse';

@Component({
  selector: 'app-server-error',
  imports: [MatCard],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './server-error.component.html',
  styleUrl: './server-error.component.scss'
})
export class ServerErrorComponent {
  // errorInterceptor navigates here with { state: { error } }; the router keeps it in history.state.
  readonly error: ApiErrorResponse | undefined = history.state['error'];
}
