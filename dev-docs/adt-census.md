# HL7 v2 ADT census over SFTP

A facility can supply its census as files of HL7 v2 ADT messages. It follows the same path as the Cerner extract, with
the sub type `HL7ADT`: `PatientCensusScheduled` creates an `SftpAcquisitionLog`, the Acquisition Worker's
`Hl7AdtProcessor` reads the files, and Census consumes the resulting `ADTAcquired` event.

Unlike a Cerner extract, which is a snapshot of who is admitted, an ADT feed is a stream of changes. Most of what is
different below follows from that.

## Configuration

An acquisition configuration with the sub type `HL7ADT` carries an `adtConfiguration`, stored as JSON with the rest of
the SFTP configuration. Exactly one of the first two is required:

| Setting | Meaning |
| --- | --- |
| `patientIdLocation` | Where the message carries the FHIR Patient.Id. A value such as `Patient/123` is reduced to `123`. |
| `mrnIdentifierSystem` | Find the Patient.Id with `GET /Patient?identifier=<system>\|<mrn>`, using the facility's FHIR Query configuration. |
| `mrnLocation` | Optional. Where to read the MRN. Defaults to `PID-3[5=MR].1`. |
| `encounterIdLocation` | Optional. Where to read the encounter id. Defaults to `PV1-19.1`. |

The acquisition must also have a processed directory or `removeAfterProcessing`, so a file is not read again on every run.

### Locations

`SEG[selector]-Field[selector].Component.Subcomponent`. Everything after the field is optional, and numbers are 1-based.

| Location | Reads |
| --- | --- |
| `PID-2` | The whole field. |
| `PID-3.1` | First component of the first repetition. |
| `PID-3[2].1` | First component of the second repetition. |
| `PID-3[5=MR].1` | First component of the repetition whose fifth component is `MR`. |
| `OBX[2]-5` | OBX-5 of the second OBX segment. |
| `OBX[3.1=FHIRID]-5` | OBX-5 of the OBX segment whose OBX-3.1 is `FHIRID`. |

## Data Acquisition

Files are read oldest first, with ties broken by name, because SFTP timestamps are coarse. Each file normally produces
one `ADTAcquired` event, keyed by facility, with its messages in file order. A file over
`SftpAcquisition:AdtMaxMessageBytes` (900,000 by default) is split across several events in the same order.

A message that cannot be tied to a patient is logged as an error and left out: no MRN, no Patient.Id at the location,
or a search that does not find exactly one patient. Failing to reach the FHIR server is different. It throws, so the
acquisition is retried with the file still in place rather than losing events. Logs identify a message by file,
position and control id (MSH-10), never by MRN.

Timestamps with an offset are converted to UTC. Without one, HL7 means the sender's local time, and the value is passed
on as sent.

## Census

`AdtEventService` applies the messages in order, in one transaction, and only then are `PatientEvent` messages produced.
Other message types are ignored. A message already recorded, with the same patient, type, MSH-7 and encounter id, is
ignored too, so reading a file twice changes nothing.

| Message | Encounter | PatientEvent message |
| --- | --- | --- |
| A01 Admit/visit | Sets the status, and the admit date from PV1-44 | Admit, if it opened the stay |
| A02 Transfer | Sets the location | Admit, if it opened the stay |
| A03 Discharge | Sets the discharge date and status | Discharge, plus Admit first if it opened the stay |
| A06 Outpatient to inpatient | Sets the class | Admit, if it opened the stay |
| A07 Inpatient to outpatient | Sets the class | Admit, if it opened the stay |
| A11 Cancel admit/visit | Marks `cancelled`, closed at its admit date | None |
| A12 Cancel transfer | Sets the location back | Admit, if it opened the stay |
| A13 Cancel discharge | Clears the discharge date | Admit, if it opened the stay |
| A18 Merge | Moves matching encounters to the surviving patient | None |

A feed can start mid-stay, so whichever message comes first for a stay opens it and emits Admit. A later A01 for that
stay fills in its details without a second Admit.

A message belongs to the encounter with the same encounter id. Without one, it belongs to the patient's open encounter,
and an A13 to their most recent one. A cancelled encounter is never matched, so an A01 after an A11 starts a new one.

An A18 finds encounters by MRG-1, MRG-2 and MRG-4, against each encounter's MRN and patient identifiers, within the
facility. PID is the surviving patient. The prior identifiers stay on the encounter.

On `PatientIdentifiers`, `SourceType` `FHIR` is the FHIR Patient.Id and `ADT` is an identifier from PID-3, which is the
facility's own and must never be read as a FHIR id.

## Known limits

- **Downstream cannot be told to undo anything.** After an A11 or A13, the Admit or Discharge that was already sent still
  stands: Report keeps its entry, and QueryDispatch still runs the dispatch it scheduled for the discharge.
- **A merge does not reach downstream.** Report entries and acquired data keep the Patient.Id of the patient merged away,
  and that patient's earlier `PatientEvents` are not re-keyed.
- **A lost Kafka produce loses the message.** Changes are committed before `PatientEvent` messages are produced. If
  producing fails, a retry finds the messages already recorded and emits nothing.
- **Times without an offset are facility-local** and stored as sent, while Census compares some dates to UTC.
