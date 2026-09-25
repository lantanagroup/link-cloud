import type {LocationIdentifierEntry, LocationTypeEntry} from '../../types';

/** A row counts as incomplete unless both its fields are filled - same rule HSLOC's mapping rows
 *  already use, so a row added but not yet finished (including a still-blank one) blocks Continue
 *  rather than silently saving half a pair Data Acquisition can't do anything with. */
export function findIncompleteLocationTypeIndexes(rows: LocationTypeEntry[]): number[] {
  const incomplete: number[] = [];
  rows.forEach((row, index) => {
    if (!(row.code.trim() && row.alias.trim())) {
      incomplete.push(index);
    }
  });
  return incomplete;
}

export function findIncompleteLocationIdentifierIndexes(rows: LocationIdentifierEntry[]): number[] {
  const incomplete: number[] = [];
  rows.forEach((row, index) => {
    if (!(row.system.trim() && row.code.trim())) {
      incomplete.push(index);
    }
  });
  return incomplete;
}

// Same character set the BFF's FieldValidationRules.FhirPathCharacterPattern allows for an
// imported custom FHIRPath, so an expression typed here and one uploaded in the sheet are held to
// the same rule.
const FHIR_PATH_CHARACTERS = /^[\w\s.()[\]'"=!<>,:%$*/+&|-]+$/;

/**
 * A structural check on a custom FHIRPath - not a full parse (Data Acquisition compiles it with
 * Firely on save and has the final word), but enough to catch what that compile rejects most often
 * before the facility gets a bare "DataAcquisition returned 400": an unclosed quote or bracket, a
 * dangling `.` ("Encounter.", "Encounter..id", ".id"), or an expression that ends on an operator.
 * A blank value is not invalid here - the tab can be left empty, same as a method with zero rows.
 */
export function isPlausibleFhirPath(value: string): boolean {
  const expression = value.trim();
  if (!expression) {
    return true;
  }
  if (!FHIR_PATH_CHARACTERS.test(expression)) {
    return false;
  }

  // Collapse each quoted literal to a single placeholder so its contents (which may legitimately
  // hold a '.', '(' or an operator) aren't mistaken for structure below.
  let structure = '';
  let quote: string | null = null;
  for (const char of expression) {
    if (quote) {
      if (char === quote) {
        quote = null;
      }
      continue;
    }
    if (char === '\'' || char === '"') {
      quote = char;
      structure += 'x';
      continue;
    }
    structure += char;
  }
  if (quote) {
    return false;
  }

  const stack: string[] = [];
  for (const char of structure) {
    if (char === '(' || char === '[') {
      stack.push(char === '(' ? ')' : ']');
    } else if (char === ')' || char === ']') {
      if (stack.pop() !== char) {
        return false;
      }
    }
  }
  if (stack.length > 0) {
    return false;
  }

  // Every '.' must join two path parts: something that can be navigated from on its left (a name,
  // a literal, a closing bracket) and a name on its right - or digits on both sides, for a decimal.
  const compact = structure.replace(/\s+/g, '');
  for (let index = 0; index < compact.length; index++) {
    if (compact[index] !== '.') {
      continue;
    }
    const before = compact[index - 1] ?? '';
    const after = compact[index + 1] ?? '';
    const isDecimal = /\d/.test(before) && /\d/.test(after);
    if (!isDecimal && !(/[\w)\]]/.test(before) && /[A-Za-z_]/.test(after))) {
      return false;
    }
  }

  // Can't end on an operator or separator - there's nothing for it to apply to.
  return !/[.=!<>,:+\-*/&|(]$/.test(compact);
}
