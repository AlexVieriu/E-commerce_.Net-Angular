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
  // history.state is the browser's built-in storage for data passed during navigation.
  // When the error interceptor calls router.navigateByUrl('/server-error', { state: { error: ... } }),
  // Angular stores that data in history.state. We can read it here — or anywhere
  readonly error: ApiErrorResponse | undefined = history.state['error'];
}
