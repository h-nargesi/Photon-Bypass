import { CommonModule, NgStyle } from '@angular/common';
import { Component } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import {
  ButtonDirective,
  CardBodyComponent,
  CardComponent,
  ColComponent,
  RowComponent,
  SpinnerComponent,
  TextColorDirective,
} from '@coreui/angular';
import { ChartjsComponent } from '@coreui/angular-chartjs';
import { IconDirective } from '@coreui/icons-angular';
import { getStyle } from '@coreui/utils';
import {
  ChartData,
  ChartDataset,
  ChartOptions,
  PluginOptionsByType,
  ScaleOptions,
  TooltipLabelStyle,
} from 'chart.js';
import { ICON_SUBSET } from '../../@icons';
import { TrafficDataModel } from '../../@models';
import { TranslationPipe } from '../../@services';
import { TrafficDataService } from './traffic-chart.service';

@Component({
  selector: 'app-traffic-chart',
  imports: [
    CommonModule,
    TextColorDirective,
    CardComponent,
    CardBodyComponent,
    RowComponent,
    ColComponent,
    ButtonDirective,
    IconDirective,
    ReactiveFormsModule,
    ChartjsComponent,
    CommonModule,
    NgStyle,
    TranslationPipe,
    SpinnerComponent,
  ],
  providers: [TrafficDataService],
  templateUrl: './traffic-chart.component.html',
  styleUrl: './traffic-chart.component.scss',
})
export class TrafficChartComponent {
  icons = ICON_SUBSET;
  title?: string;
  options?: ChartOptions;
  data?: ChartData;
  loading: boolean = false;

  constructor(private readonly service: TrafficDataService) {
    this.initOptions();
  }

  loadTrafficData() {
    this.loading = true;
    this.service.fetchTrafficData().subscribe((data) => {
      this.loading = false;
      this.initMainChart(data);
    });
  }

  private initMainChart(model: TrafficDataModel) {
    if (!model) return;

    const colors = this.getColors();
    const labels: string[] = model.labels;

    let index = 0;
    const datasets: ChartDataset[] = [];
    model.collections.forEach((collection) => {
      datasets.push({
        data: collection.data,
        label: collection.title,
        ...colors[index++],
      });
    });

    this.title = model.title;

    this.data = {
      datasets,
      labels,
    };
  }

  private getColors(): any[] {
    const brandSuccess = getStyle('--cui-success') ?? '#4dbd74';
    const brandInfo = getStyle('--cui-info') ?? '#20a8d8';
    const brandWarning = getStyle('--cui-warning') ?? '#20a8d8';
    const brandWarningBg = `rgba(${getStyle('--cui-warning-rgb')}, .1)`;
    const brandInfoBg = `rgba(${getStyle('--cui-info-rgb')}, .1)`;
    const brandDanger = getStyle('--cui-danger') ?? '#f86c6b';

    return [
      {
        // brandInfo
        backgroundColor: brandInfoBg,
        borderColor: brandInfo,
        pointHoverBackgroundColor: brandInfo,
        borderWidth: 1,
        fill: true,
      },
      {
        // brandInfo
        backgroundColor: brandWarningBg,
        borderColor: brandWarning,
        pointHoverBackgroundColor: brandWarning,
        borderWidth: 1,
        fill: true,
      },
      {
        // brandSuccess
        backgroundColor: 'transparent',
        borderColor: brandSuccess || '#4dbd74',
        pointHoverBackgroundColor: '#fff',
      },
      {
        // brandDanger
        backgroundColor: 'transparent',
        borderColor: brandDanger || '#f86c6b',
        pointHoverBackgroundColor: brandDanger,
        borderWidth: 1,
        borderDash: [8, 5],
      },
    ];
  }

  private initOptions() {
    const plugins: DeepPartial<PluginOptionsByType<any>> = {
      legend: {
        display: false,
      },
      tooltip: {
        callbacks: {
          labelColor: (context) =>
            ({
              backgroundColor: context.dataset.borderColor,
            } as TooltipLabelStyle),
        },
      },
    };

    const scales = this.getScales();

    this.options = {
      maintainAspectRatio: false,
      plugins,
      scales,
      elements: {
        line: {
          tension: 0.4,
        },
        point: {
          radius: 0,
          hitRadius: 10,
          hoverRadius: 4,
          hoverBorderWidth: 3,
        },
      },
    };
  }

  private getScales() {
    const colorBorderTranslucent = getStyle('--cui-border-color-translucent');
    const colorBody = getStyle('--cui-body-color');

    const scales: ScaleOptions<any> = {
      x: {
        grid: {
          color: colorBorderTranslucent,
          drawOnChartArea: false,
        },
        ticks: {
          color: colorBody,
        },
      },
      y: {
        border: {
          color: colorBorderTranslucent,
        },
        grid: {
          color: colorBorderTranslucent,
        },
        beginAtZero: true,
        ticks: {
          color: colorBody,
          maxTicksLimit: 5,
          stepSize: Math.ceil(250 / 5),
        },
      },
    };

    return scales;
  }
}

type DeepPartial<T> = T extends Function
  ? T
  : T extends Array<infer U>
  ? _DeepPartialArray<U>
  : T extends object
  ? _DeepPartialObject<T>
  : T | undefined;

type _DeepPartialArray<T> = Array<DeepPartial<T>>;
type _DeepPartialObject<T> = { [P in keyof T]?: DeepPartial<T[P]> };
