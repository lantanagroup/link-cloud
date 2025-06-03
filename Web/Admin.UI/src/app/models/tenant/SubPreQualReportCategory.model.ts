import { SubPreQualReportIssueModel } from "./SubPreQualReportIssue.model";

export class SubPreQualReportCategoryModel {
  name: string = '[Category Name]';
  quantity: number = 12;
  guidance: string = '[Category Guidance]';
  issues: SubPreQualReportIssueModel[] = [];
}
