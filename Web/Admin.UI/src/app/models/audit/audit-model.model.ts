import { IAuditModel } from "../../interfaces/audit/audit-model.interface";
import { PropertyChange } from "./property-change.model";

export class AuditModel implements IAuditModel {
    id!: string;
    facilityId!: string;
    correlationId!: string;
    serviceName!: string;
    eventDate!: string;
    user!: string;
    action!: string;
    resource!: string;
    propertyChanges!: PropertyChange[];
    notes!: string;
}
