import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
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
    MatToolbarModule
  ],
  templateUrl: './account-dashboard.component.html',
  styleUrls: ['./account-dashboard.component.scss']
})
export class AccountDashboardComponent {
  private initPageSize: number = 10;
  private initPageNumber: number = 0;

  users: UserModel[] = [];
  paginationMetadata: PaginationMetadata = new PaginationMetadata;

  //search parameters
  searchText: string = '';
  filterFacilityBy: string = '';
  filterRoleBy: string = '';
  filterClaimBy: string = '';
  includeDeactivatedUsers: boolean = false;
  includeDeletedUsers: boolean = false;
  sortBy: string = '';


  displayedColumns: string[] = [ 'FirstName', 'LastName' , 'Role', 'Email', 'Actions'];
  dataSource = new MatTableDataSource<UserModel>(this.users);


  constructor(private userService: UserService) { }

  ngOnInit(): void {
    this.paginationMetadata.pageNumber = this.initPageNumber;
    this.paginationMetadata.pageSize = this.initPageSize;
    this.getUsers();
  }

  getUsers() {
    this.userService.list(this.searchText, this.filterFacilityBy, this.filterRoleBy, this.filterClaimBy, this.includeDeactivatedUsers, this.includeDeletedUsers, this.sortBy, this.paginationMetadata.pageSize, this.paginationMetadata.pageNumber).subscribe(data => {
      this.users = data.records;
      this.paginationMetadata = data.metadata;
    });
  }

  onEdit(row: UserModel) {
  }

  onDelete(row: UserModel) {
  }

  /*showCreateFacilityDialog(): void {
    this.dialog.open(FacilityConfigDialogComponent,
      {
        width: '75%',
        data: { dialogTitle: 'Create a facility configuration', viewOnly: false, facilityConfig: null }
      }).afterClosed().subscribe(res => {
      console.log(res)
      if (res) {
        this.getUsers();
        this.snackBar.open(`${res}`, '', {
          duration: 3500,
          panelClass: 'success-snackbar',
          horizontalPosition: 'end',
          verticalPosition: 'top'
        });
      }
    });
  }*/
}
