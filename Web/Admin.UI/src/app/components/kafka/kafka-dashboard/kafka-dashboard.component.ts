import { Component, OnInit } from '@angular/core';
import { AppConfigService } from '../../../services/app-config.service';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-kafka-dashboard',
  imports: [CommonModule],
  templateUrl: './kafka-dashboard.component.html',
  styleUrl: './kafka-dashboard.component.scss'
})
export class KafkaDashboardComponent implements OnInit{
  kafkaUrl: string = '';
  constructor(private appConfig: AppConfigService) { }

  ngOnInit(): void {
    this.kafkaUrl = this.appConfig.config?.kafkaUrl || '';
  }
}
