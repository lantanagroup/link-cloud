import { IPropertyChange } from "../../interfaces/property-change.interface";

export class PropertyChange implements IPropertyChange {
    propertyName!: string;
    initialPropertyValue!: string;
    newPropertyValue!: string;
}
