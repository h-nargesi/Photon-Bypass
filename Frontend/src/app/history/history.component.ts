import { CommonModule } from '@angular/common';
import { AfterViewInit, Component, ViewChild } from '@angular/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginator, MatPaginatorModule } from '@angular/material/paginator';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import {
  CardBodyComponent,
  CardComponent,
  ColComponent,
  RowComponent,
} from '@coreui/angular';
import { HistoryRecord } from '../@models';
import { HistoryService } from './history.service';
import { TranslationPipe } from '../@services';

@Component({
  selector: 'app-history',
  imports: [
    CommonModule,
    RowComponent,
    ColComponent,
    CardComponent,
    CardBodyComponent,
    TranslationPipe,
    MatFormFieldModule,
    MatInputModule,
    MatTableModule,
    MatSortModule,
    MatPaginatorModule,
  ],
  templateUrl: './history.component.html',
  styleUrl: './history.component.scss',
  providers: [HistoryService],
})
export class HistoryComponent implements AfterViewInit {
  readonly displayedColumns: string[] = ['eventTimeTitle', 'title', 'value'];
  dataSource!: MatTableDataSource<HistoryRecord>;

  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  constructor(service: HistoryService) {
    service.load().subscribe((result) => {
      this.dataSource = new MatTableDataSource(result);
      if (this.paginator) this.dataSource.paginator = this.paginator;
      if (this.sort) this.dataSource.sort = this.sort;
    });
  }

  ngAfterViewInit() {
    if (this.dataSource) {
      this.dataSource.paginator = this.paginator;
      this.dataSource.sort = this.sort;
    }
  }

  applyFilter(event: Event) {
    const filterValue = (event.target as HTMLInputElement).value;
    this.dataSource.filter = filterValue.trim().toLowerCase();

    if (this.dataSource.paginator) {
      this.dataSource.paginator.firstPage();
    }
  }
}
