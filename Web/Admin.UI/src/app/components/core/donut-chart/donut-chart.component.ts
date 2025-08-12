import { CommonModule } from '@angular/common';
import { AfterViewInit, Component, ElementRef, Input, OnChanges, OnDestroy, SimpleChanges, ViewChild } from '@angular/core';
import * as d3 from 'd3';

@Component({
  selector: 'app-donut-chart',
  imports: [
    CommonModule
  ],
  templateUrl: './donut-chart.component.html',
  styleUrl: './donut-chart.component.scss'
})
export class DonutChartComponent implements AfterViewInit, OnChanges, OnDestroy { 

  @Input() data: Record<string, number> = {};
  @ViewChild('container', { static: true }) container!: ElementRef;
  @ViewChild('chart', { static: true }) chart!: ElementRef<SVGSVGElement>;
  
  private resizeObserver!: ResizeObserver;

  ngAfterViewInit(): void {
    this.resizeObserver = new ResizeObserver(entries => {
      for (let entry of entries) {
        const { width, height } = entry.contentRect;
        this.renderChart(width, height);
      }
    });
    this.resizeObserver.observe(this.container.nativeElement);
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (this.container?.nativeElement) {
      const rect = this.container.nativeElement.getBoundingClientRect();
      this.renderChart(rect.width, rect.height);
    }
  }
  
  private renderChart(width: number, height: number): void {
    const svg = d3.select(this.chart.nativeElement);
    svg.selectAll('*').remove(); // clear chart

    const radius = Math.min(width, height) / 2.5;
    const innerRadius = radius * 0.5; // donut

    svg
      .attr('viewBox', `0 0 ${width} ${height}`)
      .attr('preserveAspectRatio', 'xMidYMid meet');

    const g = svg
      .append('g')
      .attr('transform', `translate(${width / 2}, ${height / 2})`);

    const dataEntries = Object.entries(this.data);
    const color = d3.scaleOrdinal(d3.schemeTableau10);

    const pie = d3.pie<any>().value(d => d[1]);
    const arc = d3.arc<d3.PieArcDatum<[string, number]>>()
      .innerRadius(innerRadius)
      .outerRadius(radius);

    g.selectAll('path')
      .data(pie(dataEntries))
      .enter()
      .append('path')
      .attr('d', arc)
      .attr('fill', d => color(d.data[0]))
      .attr('stroke', 'white')
      .attr('stroke-width', 2);

    const labelArc = d3.arc<d3.PieArcDatum<[string, number]>>()
      .innerRadius((radius + innerRadius) / 2)
      .outerRadius((radius + innerRadius) / 2);

    g.selectAll('text')
      .data(pie(dataEntries))
      .enter()
      .append('text')
      .attr('transform', d => `translate(${labelArc.centroid(d)})`)
      .attr('text-anchor', 'middle')
      .attr('alignment-baseline', 'middle')
      .style('font-size', '12px')
      .style('fill', '#fff')
      .text(d => d.data[1]);

    // Legend
    const legend = svg
      .append('g')
      .attr('class', 'legend')
      .attr('transform', `translate(0, 20)`);

    dataEntries.forEach((d, i) => {
      const legendRow = legend.append('g')
        .attr('transform', `translate(0, ${i * 20})`);

      legendRow.append('rect')
        .attr('width', 12)
        .attr('height', 12)
        .attr('fill', color(d[0]));

      legendRow.append('text')
        .attr('x', 18)
        .attr('y', 10)
        .attr('font-size', '12px')
        .text(`${d[0]} (${d[1]})`);
    });
  }

  ngOnDestroy(): void {
    if(this.resizeObserver) {
      this.resizeObserver.disconnect();
    }
  }

}
