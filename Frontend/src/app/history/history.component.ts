import { CommonModule } from '@angular/common';
import { AfterViewInit, Component, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import {
  MatPaginator,
  MatPaginatorIntl,
  MatPaginatorModule,
} from '@angular/material/paginator';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import {
  CardBodyComponent,
  CardComponent,
  ColComponent,
  FormControlDirective,
  InputGroupComponent,
  InputGroupTextDirective,
  RowComponent,
} from '@coreui/angular';
import { HistoryRecord } from '../@models';
import { TranslationPipe } from '../@services';
import { HistoryService } from './history.service';

import { MatDatepickerModule } from '@angular/material/datepicker';
import { IconDirective } from '@coreui/icons-angular';
import { ICON_SUBSET } from '../@icons';
import { PaginatorInt } from './paginator-int';

@Component({
  selector: 'app-history',
  imports: [
    CommonModule,
    FormsModule,
    RowComponent,
    ColComponent,
    CardComponent,
    CardBodyComponent,
    InputGroupComponent,
    InputGroupTextDirective,
    IconDirective,
    FormControlDirective,
    TranslationPipe,
    MatFormFieldModule,
    MatInputModule,
    MatTableModule,
    MatSortModule,
    MatPaginatorModule,
    MatDatepickerModule,
  ],
  templateUrl: './history.component.html',
  styleUrl: './history.component.scss',
  providers: [
    HistoryService,
    provideNativeDateAdapter(),
    {
      provide: MatPaginatorIntl,
      useClass: PaginatorInt,
    },
  ],
})
export class HistoryComponent implements AfterViewInit {
  readonly icons = ICON_SUBSET;
  readonly displayedColumns: string[] = ['eventTimeTitle', 'title', 'value'];
  dataSource!: MatTableDataSource<HistoryRecord>;

  @ViewChild(MatPaginator) paginator?: MatPaginator;
  @ViewChild(MatSort) sort?: MatSort;

  fromDate?: number;
  toDate?: number;

  constructor(private readonly service: HistoryService) {
    this.loadWithFilter();
  }

  ngAfterViewInit() {
    if (this.dataSource) {
      if (this.paginator) this.dataSource.paginator = this.paginator;
      if (this.sort) this.dataSource.sort = this.sort;
    }
  }

  loadWithFilter() {
    this.service.load(this.fromDate, this.toDate).subscribe((result) => {
      this.dataSource = new MatTableDataSource(result);
      if (this.paginator) this.dataSource.paginator = this.paginator;
      if (this.sort) this.dataSource.sort = this.sort;
    });
  }

  applyFilter(event: Event) {
    const filterValue = (event.target as HTMLInputElement).value;
    this.dataSource.filter = filterValue.trim().toLowerCase();

    if (this.dataSource.paginator) {
      this.dataSource.paginator.firstPage();
    }
  }
}
