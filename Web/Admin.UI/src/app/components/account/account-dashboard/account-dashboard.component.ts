import {Component} from '@angular/core';
import {CommonModule} from '@angular/common';
import {PaginationMetadata} from "../../../models/pagination-metadata.model";
import {MatTableDataSource, MatTableModule} from "@angular/material/table";
import {UserService} from "../../../services/gateway/account/user.service";
import {UserModel} from "../../../models/user/user-model.model";
import {MatPaginatorModule} from "@angular/material/paginator";
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
  paginationMetadata: PaginationMetadata = new PaginationMetadata;

  dataSource!: MatTableDataSource<UserModel>;

  //search parameters
  searchText: string = '';
  filterFacilityBy: string = '';
  filterRoleBy: string = '';
  filterClaimBy: string = '';
  includeDeactivatedUsers: boolean = false;
  includeDeletedUsers: boolean = false;
  sortBy: string = '';


  displayedColumns: string[] = ['FirstName', 'LastName', 'Roles', 'Email', 'Actions'];

  loading = false;
  error: string | null = null;

  constructor(private userService: UserService) {
  }

  ngOnInit(): void {
    this.dataSource = new MatTableDataSource<UserModel>();
    this.paginationMetadata.pageNumber = this.initPageNumber;
    this.paginationMetadata.pageSize = this.initPageSize;
    this.getUsers();
  }

  getUsers() {
    this.loading = true;
    this.error = null;
    this.userService.list(
      this.searchText,
      this.filterFacilityBy,
      this.filterRoleBy,
      this.filterClaimBy,
      this.includeDeactivatedUsers,
      this.includeDeletedUsers,
      this.sortBy,
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

  onEdit(row: UserModel) {
  }

  onDelete(row: UserModel) {
  }

}
