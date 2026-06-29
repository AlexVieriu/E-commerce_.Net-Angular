import { ChangeDetectionStrategy, Component } from '@angular/core';

// Placeholder — T28 builds the real dashboard (stat cards + recent orders table).
@Component({
  selector: 'app-admin-dashboard',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 class="text-2xl font-semibold">Dashboard</h2>
    <p class="text-gray-500 mt-2">Stats and recent orders land here in T28.</p>
  `,
})
export class AdminDashboardComponent {}
