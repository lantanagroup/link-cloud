import { Injectable } from "@angular/core";

@Injectable()
export class UrlOrBundleValidator {

  public bundleAndUrlMissing(formGroup: any): boolean {
      const url = formGroup["url"];
      const bundle = formGroup['bundle'];
      return !url && !bundle;
    };
  }



