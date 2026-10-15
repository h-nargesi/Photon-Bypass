import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  Component,
  DestroyRef,
  ViewChild,
  inject,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
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
import { EventType, HistoryRecord } from '../@models';
import { TranslationPipe, UserService } from '../@services';
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
  readonly displayedColumns: string[] = [
    'eventTimeTitle',
    'target',
    'category',
    'title',
    'value',
  ];
  dataSource!: MatTableDataSource<HistoryRecord>;
  
  @ViewChild(MatPaginator) paginator?: MatPaginator;
  @ViewChild(MatSort) sort?: MatSort;

  fromDate?: Date;
  toDate?: Date;

  constructor(
    private readonly service: HistoryService,
    private readonly user_service: UserService
  ) {}

  private readonly destroy_ref = inject(DestroyRef);

  ngOnInit() {
    this.loadWithFilter();
    this.user_service.onTargetChanged
      .pipe(takeUntilDestroyed(this.destroy_ref))
      .subscribe(() => {
        this.loadWithFilter();
      });
  }

  ngAfterViewInit() {
    if (this.dataSource) {
      if (this.paginator) this.dataSource.paginator = this.paginator;
      if (this.sort) this.dataSource.sort = this.sort;
    }
  }

  loadWithFilter() {
    const from = this.fromDate ? this.fromDate.getTime() : undefined;
    const to = this.toDate
      ? new Date(this.toDate).setHours(23, 59, 59, 999)
      : undefined;

    this.service
      .load(this.user_service.targetName, from, to)
      .subscribe((result) => {
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

  getColor(type: EventType): string {
    return COLORS[type];
  }
}

const COLORS: string[] = ['info', 'info', 'success', 'warning', 'danger', 'danger'];
