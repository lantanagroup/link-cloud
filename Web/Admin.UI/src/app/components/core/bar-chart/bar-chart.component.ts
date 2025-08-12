import { Component, Input, ElementRef, ViewChild, OnChanges, SimpleChanges } from '@angular/core';
import * as d3 from 'd3';

@Component({
  selector: 'app-bar-chart',
  templateUrl: './bar-chart.component.html',
  styleUrl: './bar-chart.component.scss'
})
export class BarChartComponent implements OnChanges {
  @Input() data: Record<string, number> = {};
  @ViewChild('chart', { static: true }) chartContainer!: ElementRef;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['data']) {
      this.drawChart();
    }
  }

  private drawChart(): void {
    const element = this.chartContainer.nativeElement;
    d3.select(element).selectAll('*').remove();

    const dataEntries = Object.entries(this.data);
    const margin = { top: 20, right: 100, bottom: 30, left: 125 };
    const width = 500 - margin.left - margin.right;
    const height = dataEntries.length * 40;

    const svg = d3.select(element)
      .append('svg')
      .attr('width', width + margin.left + margin.right)
      .attr('height', height + margin.top + margin.bottom)
      .append('g')
      .attr('transform', `translate(${margin.left},${margin.top})`);

    const x = d3.scaleLinear()
      .domain([0, d3.max(dataEntries, d => d[1]) || 0])
      .range([0, width]);

    const y = d3.scaleBand()
      .domain(dataEntries.map(d => d[0]))
      .range([0, height])
      .padding(0.2);

    svg.append('g')
      .call(d3.axisLeft(y));

    svg.append('g')
      .attr('transform', `translate(0,${height})`)
      .call(d3.axisBottom(x).ticks(5).tickFormat(d => `${d} ms`));

    svg.selectAll('rect')
      .data(dataEntries)
      .enter()
      .append('rect')
      .attr('y', d => y(d[0])!)
      .attr('width', d => x(d[1]))
      .attr('height', y.bandwidth())
      .attr('fill', '#4285f4');

    svg.selectAll('text.value')
      .data(dataEntries)
      .enter()
      .append('text')
      .attr('class', 'value')
      .attr('x', d => x(d[1]) + 5)
      .attr('y', d => (y(d[0])! + y.bandwidth() / 2))
      .attr('dy', '.35em')
      .text(d => `${d[1]} ms`);
  }
}
