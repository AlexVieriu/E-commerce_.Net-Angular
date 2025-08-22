import { inject, Injectable } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { ConfirmationDialogComponent } from '../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { firstValueFrom } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class DialogService {
  private dialog = inject(MatDialog);

  confirm(title: string, message: string) {
    
    // returns an observable
    const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
      data: { title, message },
      width: '400px'
    }).afterClosed();

    return firstValueFrom(dialogRef); // returns true or nothing
  }
}
