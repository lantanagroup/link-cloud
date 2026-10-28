import { MatSnackBar } from '@angular/material/snack-bar';

export class SnackbarHelper {
  static showSuccessMessage(snackBar: MatSnackBar, message: string): void {
    snackBar.open(message, '', {
      duration: 6000,
      panelClass: 'success-snackbar',
      horizontalPosition: 'center',
      verticalPosition: 'top'
    });
  }

  static showErrorMessage(snackBar: MatSnackBar, message: string): void {
    snackBar.open(message, '', {
      duration: 6000,
      panelClass: 'error-snackbar',
      horizontalPosition: 'center',
      verticalPosition: 'top'
    });
  }
}
