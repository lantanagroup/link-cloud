import { Injectable } from "@angular/core";
import { AbstractControl, AsyncValidatorFn, ValidationErrors } from "@angular/forms";
import { Observable, of } from "rxjs";
import { delay, map } from "rxjs/operators";
import { IReportConfigModel } from "../../interfaces/report/report-config-model.interface";
import { ReportService } from "../../services/gateway/report/report.service";

@Injectable()
export class ReportTypeValidator {

  constructor(private reportService: ReportService) { }

  checkIfReportTypeExists(reportType: string, item: IReportConfigModel): Observable<boolean> {
    return this.reportService
      .getReportConfigurations(item.facilityId)
      .pipe(
        map(reports => {
          const report = reports.find(report => report.reportType.toLowerCase() === reportType.toLowerCase() && report.id !== item.id);

          return report ? true : false;
        })
      ).pipe(delay(0));
  }
}

