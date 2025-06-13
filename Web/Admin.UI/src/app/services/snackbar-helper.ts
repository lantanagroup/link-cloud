import { MatSnackBar } from '@angular/material/snack-bar';

export class SnackbarHelper {
  static showSuccessMessage(snackBar: MatSnackBar, message: string): void {
    snackBar.open(message, '', {
      duration: 3500,
      panelClass: 'success-snackbar',
      horizontalPosition: 'end'
    });
  }

  static showErrorMessage(snackBar: MatSnackBar, message: string): void {
    snackBar.open(message, '', {
      duration: 3500,
      panelClass: 'error-snackbar',
      horizontalPosition: 'end',
      verticalPosition: 'top'
    });
  }
}
