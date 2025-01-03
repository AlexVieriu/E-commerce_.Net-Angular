import { Component } from '@angular/core';
import { MatCard } from '@angular/material/card';
import { Router } from '@angular/router';

@Component({
  selector: 'app-server-error',
  standalone: true,
  imports: [
    MatCard
  ],
  templateUrl: './server-error.component.html',
  styleUrl: './server-error.component.scss'
})
export class ServerErrorComponent {
  error?: any;

  constructor(private router: Router) {
    this.error = this.router.getCurrentNavigation()?.extras.state?.['error'];
  }
}