export interface IValidationCategory {
  id: string;
  title: string;
  severity: 'ERROR'| 'WARNING'| 'INFORMATION';
  acceptable: boolean;
  guidance: string;
  requireMatch?: boolean;
}

export interface IValidationRule {
  id: number;
  matcher: any; // TODO: Define matcher interface based on backend model
  timestamp: string;
}

export interface IValidationRuleSet {
  ruleSetNumber: number;
  rules: IValidationRule[];
}