import React, {useState} from 'react';
import {DataGrid2} from '@nhsn/nhsn-react-core';
import {GridColumn} from '@progress/kendo-react-grid';

export interface DataTableProps<T extends Record<string, unknown>> {
  rows: T[];
  /** Field name holding each row's stable identity. */
  dataItemKey: string;
  /** GridColumn elements. Use the re-exported `Column` below. */
  children: React.ReactNode;
  onRowClick?: (row: T) => void;
  pageable?: boolean;
}

/**
 * Local-data table.
 *
 * `DataGrid2` supports both server-driven paging via `fetchUrl` and local
 * paging over supplied rows. We always use the local mode: our data comes from
 * the BFF through `ApiClient`, so letting the grid issue its own requests would
 * put a second HTTP path in the component with no `apibaseurl` handling and no
 * shared error model.
 *
 * `orgId` is required by the package but unused in local mode — facility comes
 * from the token, and the component never asserts one.
 */
export function DataTable<T extends Record<string, unknown>>({
  rows,
  dataItemKey,
  children,
  onRowClick,
  pageable = true
}: DataTableProps<T>) {
  const [selectedState, setSelectedState] = useState({});

  return (
    <DataGrid2
      local
      rows={rows as never}
      orgId=""
      dataItemKey={dataItemKey}
      selectedState={selectedState}
      setSelectedState={setSelectedState}
      pagerSettings={pageable ? undefined : {buttonCount: 0, pageSizes: false, info: false}}
      onRowClick={
        onRowClick ? (event: {dataItem: T}) => onRowClick(event.dataItem) : undefined
      }>
      {children}
    </DataGrid2>
  );
}

export {GridColumn as Column};
