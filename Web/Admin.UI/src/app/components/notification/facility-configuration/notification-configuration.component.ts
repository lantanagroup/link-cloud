import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NotificationService } from '../../../services/gateway/notification.service';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { OverlayContainer } from '@angular/cdk/overlay';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatExpansionModule } from '@angular/material/expansion';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectChange, MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatListModule } from '@angular/material/list';
import { MatChipsModule } from '@angular/material/chips';
import { INotificationConfiguration } from '../../../interfaces/notification/notification-configuration-model.interface';
import { PaginationMetadata } from '../../../models/pagination-metadata.model';
import { animate, state, style, transition, trigger } from '@angular/animations';
import { NotificationConfigDialogComponent } from '../notification-config-dialog/notification-config-dialog.component';
import { DeleteItemDialogComponent } from '../../core/delete-item-dialog/delete-item-dialog.component';

@Component({
  selector: 'app-facility-configuration',
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
    MatListModule,
    MatChipsModule,
    MatDialogModule,
    MatSnackBarModule
  ],
  templateUrl: './notification-configuration.component.html',
  styleUrls: ['./notification-configuration.component.scss'],
  animations: [
    trigger('detailExpand', [
      state('collapsed', style({ height: '0px', minHeight: '0' })),
      state('expanded', style({ height: '*' })),
      transition('expanded <=> collapsed', animate('225ms cubic-bezier(0.4, 0.0, 0.2, 1)')),
    ]),
  ],
})
export class NotificationConfigurationComponent implements OnInit {
  private initPageSize: number = 10;
  private initPageNumber: number = 0;

  configurations: INotificationConfiguration[] = [];
  paginationMetadata: PaginationMetadata = new PaginationMetadata;

  displayedColumns: string[] = ["facilityId", 'channels'];
  columnsToDisplayWithExpand = ['expand',...this.displayedColumns.map(x => x), 'actions'];
  expandedRecord: INotificationConfiguration | null | undefined;
  dataSource = new MatTableDataSource<INotificationConfiguration>(this.configurations);

  //search parameters
  searchText: string = '';
  filterFacilityBy: string = '';
  sortBy: string = '';

  constructor(private notificationSerice: NotificationService, private dialog: MatDialog, private overlay: OverlayContainer, private snackBar: MatSnackBar) { }

  ngOnInit(): void {
    this.paginationMetadata.pageNumber = this.initPageNumber;
    this.paginationMetadata.pageSize = this.initPageSize;
    this.getNotificationConfigurations();
  }

  pagedEvent(event: PageEvent) {
    this.paginationMetadata.pageSize = event.pageSize;
    this.paginationMetadata.pageNumber = event.pageIndex;
    this.getNotificationConfigurations();
  }

  getNotificationConfigurations() {
    this.notificationSerice.listFacilityConfiguration(this.searchText, this.filterFacilityBy, this.sortBy, this.paginationMetadata.pageSize, this.paginationMetadata.pageNumber).subscribe(data => {
        this.configurations = data.records;
        this.paginationMetadata = data.metadata;
      });
  }

  onFilterSelection(filter: MatSelectChange) {

    switch (filter.source.id) {
      case ("facilityFilterSelector"):
        {
          if (filter.value === 'All') {
            this.filterFacilityBy = '';
          }
          else {
            this.filterFacilityBy = filter.value;
          }
        }
        break;
    }

    this.getNotificationConfigurations();
  }

  clearFilters() {
    this.searchText = '';
    this.filterFacilityBy = '';
    this.sortBy = '';

    this.getNotificationConfigurations();
  }

  showCreateNotificationConfigurationDialog(): void {
    this.dialog.open(NotificationConfigDialogComponent,
      {
        width: '50%',
        data: { dialogTitle: 'Create a notification configuration', viewOnly: false, notificationconfig: null }
      }).afterClosed().subscribe(res => {
        if (res) {
          this.getNotificationConfigurations();
          this.snackBar.open(`${res}`, '', {
            duration: 3500,
            panelClass: 'success-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
        }
      });
  }

  showEditNotificationConfigurationDialog($event: Event, row: INotificationConfiguration): void {

    //prevent the row from expanding
    $event.stopPropagation();

    this.dialog.open(NotificationConfigDialogComponent,
      {
        width: '50%',
        data: { dialogTitle: `Edit ${row.facilityId} notification configuration`, viewOnly: false, notificationconfig: row }
      }).afterClosed().subscribe(res => {
        if (res) {
          this.getNotificationConfigurations();
          this.snackBar.open(`${res}`, '', {
            duration: 3500,
            panelClass: 'success-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
        }
      });
  }

  showViewNotificationConfigurationDialog($event: Event, row: INotificationConfiguration): void {

    //prevent the row from expanding
    $event.stopPropagation();

    this.dialog.open(NotificationConfigDialogComponent,
      {
        width: '50%',
        data: { dialogTitle: 'Notification configuration', viewOnly: true, notificationconfig: row }
      });
  }

  showDeleteItemDialog($event: Event, row: INotificationConfiguration) {

    //prevent the row from expanding
    $event.stopPropagation();

    this.dialog.open(DeleteItemDialogComponent,
      {
        width: '50%',
        data: {
          dialogTitle: 'Delete notification configuration',
          dialogMessage: `Are you sure you want to delete the notification configuration for '${row.facilityId}'?`
        }
      }).afterClosed().subscribe(res => {
        if (res) {
          this.notificationSerice.deleteFacilityConfiguration(row.id).subscribe(outcome => {
            this.getNotificationConfigurations();
            this.snackBar.open(`Successfully deleted the notification configuration for ${row.facilityId}`, '', {
              duration: 3500,
              panelClass: 'success-snackbar',
              horizontalPosition: 'end',
              verticalPosition: 'top'
            });
          });
        }
      });
  }

}
