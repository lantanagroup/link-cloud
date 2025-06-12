import { MatTableDataSource } from "@angular/material/table";

export interface CatSummary {
  x: string;
  y: number;
}

export interface Issue {
  name: string;
  message: string;
  expression: string;
  location: string;
}

export interface Category {
  name: string;
  quantity: number;
  guidance: string;
  issues?: Issue[] | MatTableDataSource<Issue>;
}

export interface CategoryDataSource {
  name: string;
  quantity: number;
  guidance: string;
  issues?: MatTableDataSource<Issue>;
}