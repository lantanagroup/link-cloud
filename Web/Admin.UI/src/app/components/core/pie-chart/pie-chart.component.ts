import { Component, ElementRef, Input, ViewChild } from '@angular/core';
import * as d3 from 'd3';


@Component({
  selector: 'app-pie-chart',
  imports: [],
  templateUrl: './pie-chart.component.html',
  styleUrl: './pie-chart.component.scss'
})
export class PieChartComponent {
  @ViewChild('chart', { static: true }) chartElement!: ElementRef<SVGSVGElement>;
  @Input() data: Record<string, number> = {};
  @Input() width = 700;
  @Input() height = 400;

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

      const radius = Math.min(this.width, this.height) / 2;
  
      // Color scale
      const color = d3.scaleOrdinal<string>()
        .domain(Object.keys(this.data))
        .range(d3.schemeCategory10);
  
      // Pie & Arc generators
      const pie = d3.pie<{ key: string; value: number }>()
        .value(d => d.value);
  
      const arc = d3.arc<d3.PieArcDatum<{ key: string; value: number }>>()
        .innerRadius(5) // set >0 for a donut chart
        .outerRadius(radius - 10);
  
      const pieData = pie(
        Object.entries(this.data).map(([key, value]) => ({ key, value }))          
      );
  
      // Draw slices
      svg.selectAll('path')
        .data(pieData)
        .enter()
        .append('path')
        .attr('d', d => arc(d) ?? '')
        .attr('fill', d => color(d.data.key))
        .attr('stroke', '#fff')
        .style('stroke-width', '2px')
        .on('mouseover', function (event, d) {
          const [x, y] = arc.centroid(d);
          tooltip
            .attr('x', x)
            .attr('y', y)
            .text(`${d.data.key}: ${d.data.value}`)
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
  

        const margin = { top: 10, left: 10 };
        const legendSpacing = 20;
  
        const legend = d3.select(svgEl)
          .append('g')
          .attr('class', 'legend')
          .attr('transform', `translate(${margin.left}, ${margin.top})`);     
  
        legend.selectAll('g')
          .data(
            Object.entries(this.data).map(([key, value]) => ({ key, value }))
          )
          .enter()
          .append('g')
          .attr('transform', (_, i) => `translate(0, ${i * 20})`)
          .each(function(d) {
            const g = d3.select(this);  
            g.append('rect')
              .attr('width', 14)
              .attr('height', 14)
              .attr('fill', color(d.key));
  
            g.append('text')
              .attr('x', 20)
              .attr('y', 11)
              .text(`${d.key}: ${d.value}`)
              .style('font-size', '14px');
          });     
  
    }
}
