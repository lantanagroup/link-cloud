import { PropertyChange } from "../../models/audit/property-change.model"

export interface IAuditModel {
  id: string
  facilityId: string
  correlationId: string
  serviceName: string
  eventDate: string
  user: string
  action: string
  resource: string
  propertyChanges: PropertyChange[]
  notes: string
}
