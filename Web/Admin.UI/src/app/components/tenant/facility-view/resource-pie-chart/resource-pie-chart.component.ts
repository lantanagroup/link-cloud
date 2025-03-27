import { Component, ElementRef, Input, OnChanges, OnInit, SimpleChanges, ViewChild } from '@angular/core';
import { IResourceCountSummary } from '../report-view.interface';
import * as d3 from 'd3';


@Component({
  selector: 'app-resource-pie-chart',
  imports: [],
  templateUrl: './resource-pie-chart.component.html',
  styleUrl: './resource-pie-chart.component.scss'
})
export class ResourcePieChartComponent {
  @ViewChild('chart', { static: true }) chartElement!: ElementRef<SVGSVGElement>;
  @Input() data: IResourceCountSummary[] = [];

  private svg: any;
  private color: any;
  private arc: any;
  private pie: any;
  private width = 700;
  private height = 400;
  private radius = Math.min(this.width, this.height) / 2;

  constructor() { }
  
  ngOnInit(): void {
    this.createChart();
  } 

  private createChart(): void {
    const svgEl = this.chartElement.nativeElement;

    // Clear previous chart
    d3.select(svgEl).selectAll('*').remove();

    const svg = d3.select(svgEl)
      .attr('width', this.width)
      .attr('height', this.height)
      .append('g')
      .attr('transform', `translate(${this.width / 2}, ${this.height / 2})`);

    // Color scale
    const color = d3.scaleOrdinal<string>()
      .domain(this.data.map(d => d.resourceType))
      .range(d3.schemeCategory10);

    // Pie & Arc generators
    const pie = d3.pie<IResourceCountSummary>()
      .value(d => d.resourceCount);

    const arc = d3.arc<d3.PieArcDatum<IResourceCountSummary>>()
      .innerRadius(5) // set >0 for a donut chart
      .outerRadius(this.radius - 10);

    const pieData = pie(this.data);

    // Draw slices
    svg.selectAll('path')
      .data(pieData)
      .enter()
      .append('path')
      .attr('d', d => arc(d) ?? '')
      .attr('fill', d => color(d.data.resourceType))
      .attr('stroke', '#fff')
      .style('stroke-width', '2px')
      .on('mouseover', function (event, d) {
        const [x, y] = arc.centroid(d);
        tooltip
          .attr('x', x)
          .attr('y', y)
          .text(`${d.data.resourceType}: ${d.data.resourceCount}`)
          .transition()
          .duration(150)
          .style('opacity', 1);
      })
      .on('mouseout', () => {
        tooltip.transition().duration(150).style('opacity', 0);
      });

      // Create a group-level tooltip text element (initially hidden)
      const tooltip = svg.append('text')
      .attr('text-anchor', 'middle')
      .style('font-size', '14px')
      .style('font-weight', 'bold')
      .style('fill', '#000')
      .style('pointer-events', 'none')
      .style('opacity', 0);


      const legend = d3.select(svgEl)
        .append('g')
        .attr('transform', `translate(0, 20)`); // adjust X/Y as needed

      legend.selectAll('g')
        .data(this.data)
        .enter()
        .append('g')
        .attr('transform', (_, i) => `translate(0, ${i * 20})`)
        .each(function(d) {
          const g = d3.select(this);

          g.append('rect')
            .attr('width', 14)
            .attr('height', 14)
            .attr('fill', color(d.resourceType));

          g.append('text')
            .attr('x', 20)
            .attr('y', 11)
            .text(`${d.resourceType}: ${d.resourceCount}`)
            .style('font-size', '14px');
        });     

  } 

}
