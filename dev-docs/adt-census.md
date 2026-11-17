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
With `mrnIdentifierSystem`, the facility must already have a FHIR Query Configuration, or saving the SFTP configuration
returns 400: the search has no FHIR server to query without one.

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
one `ADTAcquired` event, keyed by facility, with its messages in file order. A message that carries several merges is
split into one entry per merge, each with its own patient: the PID/MRG groups of an A39, A40 or A42, and the MRG/PV1
groups of an A45. The MRG segment is read for patient merges and visit changes only. A file over
`SftpAcquisition:AdtMaxMessageBytes` (900,000 by default) is split across several events in the same order.

A message that cannot be tied to a patient is logged as an error and left out: no MRN, no Patient.Id at the location,
or a search that does not find exactly one patient. Failing to reach the FHIR server is different. It throws, so the
acquisition is retried with the file still in place rather than losing events. Logs identify a message by file,
position and control id (MSH-10), never by MRN.

Timestamps with an offset are converted to UTC. Without one, HL7 means the sender's local time, and the value is passed
on as sent.

## Census

`AdtEventService` applies the messages in order, in one transaction, and only then are `PatientEvent` messages produced.
Other message types, including A02, A05, A12 and A31, are ignored. A message already recorded, with the same patient, type, MSH-7 and encounter id, is
ignored too, so reading a file twice changes nothing.

| Message | Encounter | PatientEvent message |
| --- | --- | --- |
| A01 Admit/visit | Sets the admit date from PV1-44 | Admit, if it opened the stay |
| A03 Discharge | Sets the discharge date | Discharge, plus Admit first if it opened the stay |
| A04 Register a patient | As an A01 | Admit, if it opened the stay |
| A06 Outpatient to inpatient | Nothing specific to the message | Admit, if it opened the stay |
| A07 Inpatient to outpatient | Nothing specific to the message | Admit, if it opened the stay |
| A08 Update patient information | Corrects the admit date, and the discharge date of a discharged stay. Never opens a stay | None |
| A11 Cancel admit/visit, A23 Delete a patient record | Closes it at its admit date | None |
| A13 Cancel discharge | Clears the discharge date | Admit, if it opened the stay |
| A18, A40 Merge patient, and the older A30, A34, A36 and A39 | Moves matching encounters to the surviving patient | None |
| A42 Merge visit | Gives the visit the surviving visit number, keeping the prior one | None |
| A45 Move visit | Moves one visit to another patient | None |
| A50 Change visit number | Replaces the visit number | None |

A04 is sent for emergency, observation and outpatient visits, and Census does not filter by patient class: an A03 already
admitted such a visit when it was the first message seen for it, and an A04 now opens it at registration instead.

A feed can start mid-stay, so whichever tracked message comes first for a stay opens it and emits Admit. A later A01 for
that stay fills in its details without a second Admit. A patient who has only had ignored messages, such as transfers,
is not in the census until a tracked message arrives.

ADT messages do not set `EncounterStatus` or `EncounterClass`, so both stay empty on these encounters. PV1-2 is still kept
in the `PatientEvents` payload.

A message belongs to the encounter with the same encounter id. Without one, it belongs to the patient's open encounter,
and an A13 to their most recent one. An encounter with an A11 or A23 event is never matched, so the next message that can
open a stay, such as an A01 with the same encounter id, starts a new one and emits Admit.

A patient merge finds encounters by MRG-1, MRG-2 and MRG-4, against each encounter's MRN and patient identifiers, within the
facility. PID is the surviving patient, and its MRN replaces the prior one. Other prior identifiers stay on the encounter.
Only the patient's identifiers and MRN change: a merge can match several stays, including ended ones, so its PV1 is not
applied to any of them. A36 also merges account numbers, which Census does not use.

### Visit changes

A visit change finds the one encounter whose visit numbers include MRG-5 or MRG-6, and the message's encounter id is the
number it is known by now. Without one, the next message for the visit would carry a number Census has never seen, and an
A03 would open a second stay and emit Admit again.

- **A50** looks among the patient's own encounters, and the prior number is replaced.
- **A42** looks among the patient's own encounters too. The visit keeps the prior number beside the surviving one, so a late
  message with either reaches it. When the surviving visit is already tracked as an encounter of its own, the two were one
  stay counted twice: the surviving encounter takes the prior numbers, and the prior one is closed at its admit date and
  loses its numbers. Each gets an A42 event, the prior one's marked `MergedIntoAnotherEncounter` so a rebuild agrees.
- **A45** looks among the encounters of the patient in MRG-1, as a merge does, for the visit in MRG-5, or the message's
  encounter id when MRG-5 is empty. The encounter takes the PID patient's Patient.Id, MRN and identifiers and, unlike after
  a merge, drops the prior patient's, which belong to someone else.

A visit change that matches nothing is recorded and opens nothing. One already applied matches the new number and changes
nothing.

The MRN is stored only on `PatientEncounters.MedicalRecordNumber`. On `PatientIdentifiers`, `SourceType` `ADT_FHIR` is the
FHIR Patient.Id, with one row per encounter whichever way it was found, as `SFTP_FHIR` is for a Cerner extract. `ADT` is any
other identifier from PID-3, which is the facility's own and must never be read as a FHIR id. A PID-3 repetition counts as
an MRN if it has type `MR` or the same value as the message's MRN, and as the FHIR id if it has the same value as the
Patient.Id, alone or as a reference such as `Patient/123`. Neither is saved as `ADT`. The `PatientEvents` payload still keeps the
whole of PID-3.

## Known limits

- **Downstream cannot be told to undo anything.** After an A11, A23 or A13, or an A42 that closes a stay counted twice, the
  Admit or Discharge that was already sent still stands: Report keeps its entry, and QueryDispatch still runs the dispatch
  it scheduled for the discharge.
- **A merge or move does not reach downstream.** Report entries and acquired data keep the Patient.Id of the patient merged
  away or moved from, and that patient's earlier `PatientEvents` are not re-keyed.
- **Account numbers are not tracked.** A41, A44 and A49 are ignored, which matters only for a facility whose encounter id
  location is the account number (PID-18).
- **A lost Kafka produce loses the message.** Changes are committed before `PatientEvent` messages are produced. If
  producing fails, a retry finds the messages already recorded and emits nothing.
- **Times without an offset are facility-local** and stored as sent, while Census compares some dates to UTC.
