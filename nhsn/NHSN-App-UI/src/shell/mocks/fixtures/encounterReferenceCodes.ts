import type {EncounterCode} from '../../../core/api/contracts';

/**
 * A representative sample for offline mock mode — deliberately NOT the real catalog.
 */
export const ENCOUNTER_REFERENCE_CODES: EncounterCode[] = [
  {
    system: 'CPT',
    code: '34830',
    display:
      'Open repair of infrarenal aortic aneurysm or dissection, plus repair of associated arterial trauma, following unsuccessful endovascular repair; tube prosthesis',
    category: 'AAA',
    categoryName: 'Abdominal aortic aneurysm repair'
  },
  {
    system: 'CPT',
    code: '35082',
    display:
      'Direct repair of aneurysm, pseudoaneurysm, or excision (partial or total) and graft insertion, with or without patch graft; for ruptured aneurysm, abdominal aorta',
    category: 'AAA',
    categoryName: 'Abdominal aortic aneurysm repair'
  },
  {
    system: 'CPT',
    code: '23900',
    display: 'Interthoracoscapular amputation (forequarter)',
    category: 'AMP',
    categoryName: 'Limb amputation'
  },
  {
    system: 'CPT',
    code: '23920',
    display: 'Disarticulation of shoulder',
    category: 'AMP',
    categoryName: 'Limb amputation'
  },
  {
    system: 'CPT',
    code: '49402',
    display: 'Removal of peritoneal foreign body from peritoneal cavity',
    category: 'XLAP',
    categoryName: 'Exploratory laparotomy'
  },
  {
    system: 'SNOMED',
    code: '4525004',
    display: 'Emergency department patient visit',
    category: 'ENC',
    categoryName: 'Encounter Type / Admission Status'
  },
  {
    system: 'SNOMED',
    code: '183452005',
    display: 'Emergency hospital admission',
    category: 'ENC',
    categoryName: 'Encounter Type / Admission Status'
  },
  {
    system: 'SNOMED',
    code: '32485007',
    display: 'Hospital admission',
    category: 'ENC',
    categoryName: 'Encounter Type / Admission Status'
  },
  {
    system: 'SNOMED',
    code: '8715000',
    display: 'Hospital admission, elective',
    category: 'ENC',
    categoryName: 'Encounter Type / Admission Status'
  },
  {
    system: 'SNOMED',
    code: '448951000000000',
    display: 'Admission to observation unit',
    category: 'ENC',
    categoryName: 'Encounter Type / Admission Status'
  }
];
