import { Injectable } from "@angular/core";
import { IMeasureDefinitionConfigModel } from "../../interfaces/measure-definition/measure-definition-config-model.interface";

@Injectable()
export class BundleIdValidator {

  public invalidBundleId(formGroup: any, item:IMeasureDefinitionConfigModel): boolean {
    const bundle = formGroup['bundle'];
    let bundleId = formGroup['bundleId'];
    if (!bundleId) bundleId = item?.bundleId??"";
    if (!!bundle) {
      return bundleId !== bundle["id"];
    }
    return false;
  }
}



