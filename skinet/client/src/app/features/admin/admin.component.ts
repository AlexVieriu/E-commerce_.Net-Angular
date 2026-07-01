import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatIcon } from '@angular/material/icon';

// Admin shell: persistent sidebar + a <router-outlet> the child pages render into.
@Component({
  selector: 'app-admin',
  imports: [RouterLink, RouterLinkActive, RouterOutlet, MatSidenavModule, MatListModule, MatIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin.component.html',
  styleUrl: './admin.component.scss'
})
export class AdminComponent {
  // Sidebar items as data so the template stays declarative; paths are relative to /admin.
  protected readonly navLinks = [
    { path: 'dashboard', label: 'Dashboard', icon: 'dashboard' },
    { path: 'products', label: 'Products', icon: 'inventory_2' },
    { path: 'orders', label: 'Orders', icon: 'receipt_long' },
  ];
}
