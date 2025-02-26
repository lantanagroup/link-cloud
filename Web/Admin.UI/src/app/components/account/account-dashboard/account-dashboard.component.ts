import {Component} from '@angular/core';
import {CommonModule} from '@angular/common';
import {PaginationMetadata} from "../../../models/pagination-metadata.model";
import {MatTableDataSource, MatTableModule} from "@angular/material/table";
import {UserModel} from "../../../models/user/user-model.model";
import {MatPaginatorModule, PageEvent} from "@angular/material/paginator";
import {MatExpansionModule} from "@angular/material/expansion";
import {FormsModule} from "@angular/forms";
import {MatFormFieldModule} from "@angular/material/form-field";
import {MatSelectModule} from "@angular/material/select";
import {MatInputModule} from "@angular/material/input";
import {MatIconModule} from "@angular/material/icon";
import {MatButtonModule} from "@angular/material/button";
import {MatTooltipModule} from "@angular/material/tooltip";
import {MatToolbarModule} from "@angular/material/toolbar";
import {MatChipsModule} from "@angular/material/chips";
import {MatDialog} from "@angular/material/dialog";
import {AccountConfigDialogComponent} from "../account-config-dialog/account-config-dialog.component";
import {FormMode} from "../../../models/FormMode.enum";
import {IAccountConfigModel} from "../../../interfaces/account/account-config-model.interface";
import {RoleModel} from "../../../models/role/role-model.model";
import {AccountService} from "../../../services/gateway/account/account.service";
import {MatSnackBar} from "@angular/material/snack-bar";
import {DeleteConfirmationDialogComponent} from "../../delete-confirmation-dialog/delete-confirmation-dialog.component";


@Component({
  selector: 'app-account-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    MatTableModule,
    MatPaginatorModule,
    MatExpansionModule,
    FormsModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
    MatToolbarModule,
    MatChipsModule
  ],
  templateUrl: './account-dashboard.component.html',
  styleUrls: ['./account-dashboard.component.scss']
})
export class AccountDashboardComponent {
  private initPageSize: number = 10;
  private initPageNumber: number = 0;

  users: UserModel[] = [];
  allRoles: RoleModel[] = [];
  paginationMetadata: PaginationMetadata = new PaginationMetadata;

  dataSource!: MatTableDataSource<UserModel>;

  //search parameters
  searchText: string = '';
  filterFacilityBy: string = '';
  filterRoleBy: string = '';
  filterClaimBy: string = '';
  includeDeactivatedUsers: boolean = false;
  includeDeletedUsers: boolean = true;
  sortBy: string = 'username';
  sortOrder: number = 0;

  displayedColumns: string[] = ['UserName', 'FirstName', 'LastName', 'Roles', 'Email', 'Actions'];

  loading = false;
  error: string | null = null;

  constructor(private accountService: AccountService, private dialog: MatDialog, private snackBar: MatSnackBar) {
  }

  ngOnInit(): void {
    this.dataSource = new MatTableDataSource<UserModel>();
    this.paginationMetadata.pageNumber = this.initPageNumber;
    this.paginationMetadata.pageSize = this.initPageSize;
    this.getAccounts();
    this.getAllRoles();
  }

  getAccounts() {
    this.loading = true;
    this.error = null;
    this.accountService.getUsers(
      this.searchText,
      this.filterFacilityBy,
      this.filterRoleBy,
      this.filterClaimBy,
      this.includeDeactivatedUsers,
      this.includeDeletedUsers,
      this.sortBy,
      this.sortOrder,
      this.paginationMetadata.pageSize,
      this.paginationMetadata.pageNumber
    ).subscribe({
      next: (data) => {
        this.users = data.records;
        this.dataSource.data = this.users;
        this.paginationMetadata = data.metadata;
        this.loading = false;
      },
      error: (error) => {
        this.error = 'Failed to load users. Please try again.';
        this.loading = false;
        console.error('Error loading users:', error);
      }
    });
  }

  getAllRoles() {
    this.loading = true;
    this.error = null;
    this.accountService.getAllRoles().subscribe({
      next: (data) => {
        this.allRoles = data;
      },
      error: (error) => {
        this.error = 'Failed to load roles. Please try again.';
        this.loading = false;
        console.error('Error loading roles:', error);
      }
    });
  }

  pagedEvent(event: PageEvent) {
    this.paginationMetadata.pageSize = event.pageSize;
    this.paginationMetadata.pageNumber = event.pageIndex;
    this.getAccounts();
  }

  onEdit(row: IAccountConfigModel): void {
    this.dialog.open(AccountConfigDialogComponent, {
      width: '75%',
      data: {
        dialogTitle: 'Edit Account Configuration',
        formMode: FormMode.Edit,
        viewOnly: false,
        accountConfig: {...row},
        allRoles: this.allRoles // Pass all the roles
      }
    }).afterClosed().subscribe(res => {
      if (res) {
        this.getAccounts();
        this.snackBar.open(`Account Updated`, '', {
          duration: 3500,
          panelClass: 'success-snackbar',
          horizontalPosition: 'end',
          verticalPosition: 'top'
        });
      }
    });
  }

  onDelete(row: IAccountConfigModel): void {
    const dialogRef = this.dialog.open(DeleteConfirmationDialogComponent, {
      width: '400px',
      data: {
        message: `Are you sure you want to delete this account configuration?`
      }
    });
    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.deleteAccountConfig(row);
      }
    });
  }


  onAdd(): void {
    this.dialog.open(AccountConfigDialogComponent,
      {
        width: '75%',
        data: {
          dialogTitle: 'Account Configuration',
          formMode: FormMode.Create,
          viewOnly: false,
          accountConfig: {},
          allRoles: this.allRoles
        }
      }).afterClosed().subscribe(res => {
      if (res) {
        this.getAccounts();
        this.snackBar.open(`Account Created`, '', {
          duration: 3500,
          panelClass: 'success-snackbar',
          horizontalPosition: 'end',
          verticalPosition: 'top'
        });
      }
    });
  }

  onRestore(row: IAccountConfigModel): void {
    this.accountService.recoverUser(row.id).subscribe({
      next: () => {
        this.getAccounts();
        this.snackBar.open('Account restored successfully!', '', {
          duration: 3500,
          panelClass: 'success-snackbar',
          horizontalPosition: 'end',
          verticalPosition: 'top'
        });
      },
      error: (error) => {
        console.error('Error restoring account:', error);
        this.snackBar.open('Failed to restore account. Please try again.', '', {
          duration: 3500,
          panelClass: 'error-snackbar',
          horizontalPosition: 'end',
          verticalPosition: 'top'
        });
      }
    });
  }


  private deleteAccountConfig(row: IAccountConfigModel): void {
    console.log(`Deleted Account Config: ${JSON.stringify(row)}`);
    this.accountService.deleteUser(row.id).subscribe(() => {
      this.snackBar.open('Account configuration deleted successfully!', '', {
        duration: 3500,
        panelClass: 'success-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
      this.getAccounts();
    });
  }


}
