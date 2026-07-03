import { Component, Input, OnDestroy, ElementRef, ViewChild, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Chart, registerables } from 'chart.js';

// Register Chart.js elements
Chart.register(...registerables);

@Component({
  selector: 'app-chart-renderer',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="chart-canvas-wrapper">
      <canvas #chartCanvas></canvas>
    </div>
  `,
  styles: [`
    .chart-canvas-wrapper {
      position: relative;
      height: 220px;
      width: 100%;
      padding: 8px 4px;
      margin-top: 10px;
      background: #ffffff;
      border: 1px solid #e0e0e0;
      border-radius: 6px;
      box-shadow: 0 1px 4px rgba(0, 0, 0, 0.02);
    }
  `]
})
export class ChartRendererComponent implements OnDestroy, AfterViewInit {
  @ViewChild('chartCanvas') private chartCanvas!: ElementRef<HTMLCanvasElement>;
  
  @Input() chartConfig!: {
    type: string;
    labels: string[];
    datasets: any[];
  };

  private chartInstance?: Chart;

  ngAfterViewInit(): void {
    this.renderChart();
  }

  ngOnDestroy(): void {
    if (this.chartInstance) {
      this.chartInstance.destroy();
    }
  }

  private renderChart(): void {
    if (!this.chartConfig || !this.chartCanvas) {
      return;
    }

    const ctx = this.chartCanvas.nativeElement.getContext('2d');
    if (!ctx) return;

    const options: any = {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: {
          display: this.chartConfig.type !== 'pie' && this.chartConfig.type !== 'doughnut',
          position: 'top',
          labels: {
            font: {
              family: "'Inter', sans-serif",
              size: 11,
              weight: '500'
            },
            color: '#424242',
            boxWidth: 12
          }
        },
        tooltip: {
          backgroundColor: '#1a3a5c',
          titleFont: {
            family: "'Inter', sans-serif",
            size: 11
          },
          bodyFont: {
            family: "'Inter', sans-serif",
            size: 11
          },
          padding: 8,
          cornerRadius: 4
        }
      },
      scales: {
        x: {
          display: this.chartConfig.type !== 'pie' && this.chartConfig.type !== 'doughnut',
          grid: {
            display: false
          },
          ticks: {
            font: {
              family: "'Inter', sans-serif",
              size: 10
            },
            color: '#757575',
            maxRotation: 40,
            minRotation: 0
          }
        },
        y: {
          display: this.chartConfig.type !== 'pie' && this.chartConfig.type !== 'doughnut',
          grid: {
            color: '#eeeeee',
            drawBorder: false
          },
          ticks: {
            font: {
              family: "'Inter', sans-serif",
              size: 10
            },
            color: '#757575'
          }
        }
      }
    };

    this.chartInstance = new Chart(ctx, {
      type: this.chartConfig.type as any,
      data: {
        labels: this.chartConfig.labels,
        datasets: this.chartConfig.datasets
      },
      options: options
    });
  }
}
