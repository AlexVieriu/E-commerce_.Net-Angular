import { Component } from '@angular/core';
import { MatCard } from '@angular/material/card';
import { Router } from '@angular/router';

@Component({
  selector: 'app-server-error',
  imports: [MatCard],
  templateUrl: './server-error.component.html',
  styleUrl: './server-error.component.scss'
})
export class ServerErrorComponent {
  error?: any;

  // Shorthand declaration of the constructor
  constructor(private router: Router) {
    this.error = this.router.getCurrentNavigation()?.extras.state?.['error'];
  }
}