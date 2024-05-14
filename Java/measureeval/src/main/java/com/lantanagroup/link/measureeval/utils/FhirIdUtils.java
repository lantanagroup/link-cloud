package com.lantanagroup.link.measureeval.utils;

import org.hl7.fhir.instance.model.api.IIdType;

public class FhirIdUtils {
    public static IIdType setIdPart(IIdType id, String idPart) {
        return id.setParts(id.getBaseUrl(), id.getResourceType(), idPart, id.getVersionIdPart());
    }

    public static IIdType setLocal(IIdType id, boolean local) {
        if (!id.hasIdPart()) {
            return id;
        }
        String idPart;
        if (local) {
            if (id.isLocal()) {
                return id;
            }
            idPart = String.format("#%s", id.getIdPart());
        } else {
            if (!id.isLocal()) {
                return id;
            }
            idPart = id.getIdPart().replaceAll("^#", "");
        }
        return setIdPart(id, idPart);
    }
}
