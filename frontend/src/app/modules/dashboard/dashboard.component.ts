import { Component, AfterViewInit, ElementRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Chart } from 'chart.js/auto';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements AfterViewInit {
  @ViewChild('revenueChart') revenueChartRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('salesChart') salesChartRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('pipelineChart') pipelineChartRef!: ElementRef<HTMLCanvasElement>;

  private charts: Chart[] = [];

  ngAfterViewInit() {
    this.initRevenueChart();
    this.initSalesChart();
    this.initPipelineChart();
  }

  private initRevenueChart() {
    if (!this.revenueChartRef) return;
    
    const ctx = this.revenueChartRef.nativeElement.getContext('2d');
    if (!ctx) return;

    const chart = new Chart(ctx, {
      type: 'bar',
      data: {
        labels: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul'],
        datasets: [
          {
            label: 'Revenue',
            data: [62, 59, 85, 87, 58, 59, 56],
            backgroundColor: '#357197', // Custom blue
            borderRadius: 4,
            barThickness: 24
          },
          {
            label: 'Expenses',
            data: [26, 40, 37, 18, 86, 23, 100],
            backgroundColor: '#ea7f82', // Custom pinkish red
            borderRadius: 4,
            barThickness: 24
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: {
            display: false // We will render our own legend matching the screenshot
          }
        },
        scales: {
          y: {
            beginAtZero: true,
            max: 100,
            ticks: {
              stepSize: 20
            },
            grid: {
              color: '#f0f2f5'
            }
          },
          x: {
            grid: {
              display: false
            }
          }
        }
      }
    });

    this.charts.push(chart);
  }

  private initSalesChart() {
    if (!this.salesChartRef) return;

    const ctx = this.salesChartRef.nativeElement.getContext('2d');
    if (!ctx) return;

    const chart = new Chart(ctx, {
      type: 'bar',
      data: {
        labels: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul'],
        datasets: [
          {
            label: 'Current Year',
            data: [35, 45, 68, 55, 62, 48, 75],
            backgroundColor: '#0b4c79',
            borderRadius: 4,
            barThickness: 14
          },
          {
            label: 'Last Year',
            data: [42, 38, 50, 48, 55, 60, 52],
            backgroundColor: '#b0cbe3',
            borderRadius: 4,
            barThickness: 14
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: {
            display: false
          }
        },
        scales: {
          y: {
            beginAtZero: true,
            grid: {
              color: '#f0f2f5'
            }
          },
          x: {
            grid: {
              display: false
            }
          }
        }
      }
    });

    this.charts.push(chart);
  }

  private initPipelineChart() {
    if (!this.pipelineChartRef) return;

    const ctx = this.pipelineChartRef.nativeElement.getContext('2d');
    if (!ctx) return;

    const chart = new Chart(ctx, {
      type: 'bar',
      data: {
        labels: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul'],
        datasets: [
          {
            label: 'Enquiries',
            data: [72, 60, 85, 90, 65, 78, 88],
            backgroundColor: '#7b8ff7', // Purple
            borderRadius: 4,
            barThickness: 8
          },
          {
            label: 'Quotations',
            data: [50, 42, 60, 75, 55, 62, 70],
            backgroundColor: '#f7c343', // Yellow
            borderRadius: 4,
            barThickness: 8
          },
          {
            label: 'Sales Orders',
            data: [35, 28, 48, 55, 40, 50, 58],
            backgroundColor: '#4ade80', // Green
            borderRadius: 4,
            barThickness: 8
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: {
            display: false
          }
        },
        scales: {
          y: {
            beginAtZero: true,
            grid: {
              color: '#f0f2f5'
            }
          },
          x: {
            grid: {
              display: false
            }
          }
        }
      }
    });

    this.charts.push(chart);
  }
}
