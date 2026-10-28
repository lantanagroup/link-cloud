package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.parser.IParser;
import com.azure.core.util.BinaryData;
import com.azure.storage.blob.BlobClient;
import com.azure.storage.blob.BlobContainerClient;
import com.azure.storage.blob.BlobServiceClientBuilder;
import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import com.lantanagroup.link.shared.entities.ReportScheduleModel;
import com.lantanagroup.link.shared.exceptions.ValidationException;
import com.lantanagroup.link.shared.services.ReportClient;
import com.lantanagroup.link.shared.utils.LogUtils;
import org.hl7.fhir.instance.model.api.IIdType;
import org.hl7.fhir.r4.model.IdType;
import org.hl7.fhir.r4.model.MeasureReport;
import org.hl7.fhir.r4.model.Reference;
import org.hl7.fhir.r4.model.Resource;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.net.URI;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.stream.Collectors;

public class BlobStorageService {
    private static final Logger logger = LoggerFactory.getLogger(BlobStorageService.class);
    private final BlobContainerClient containerClient;
    private final FhirContext fhirContext;
    private final ReportClient reportClient;
    private final MeasureReportGeneratedProducer measureReportGeneratedProducer;
    private final String blobContainerName;

    public BlobStorageService(String connectionString, String blobContainerName, FhirContext fhirContext, ReportClient reportClient, MeasureReportGeneratedProducer measureReportGeneratedProducer) {
        this(new BlobServiceClientBuilder()
                .connectionString(connectionString)
                .buildClient()
                .getBlobContainerClient(blobContainerName), blobContainerName, fhirContext, reportClient, measureReportGeneratedProducer);
    }

    public BlobStorageService(BlobContainerClient containerClient, String blobContainerName, FhirContext fhirContext, ReportClient reportClient, MeasureReportGeneratedProducer measureReportGeneratedProducer) {
        this.containerClient = containerClient;
        this.blobContainerName = blobContainerName;
        this.fhirContext = fhirContext;
        this.reportClient = reportClient;
        this.measureReportGeneratedProducer = measureReportGeneratedProducer;
    }

    public String upload(String blobName, String content) {
        // If it specifies protocol/host/port, then strip it down to just the path
        if (blobName.contains("://")) {
            URI uri = URI.create(blobName);
            blobName = uri.getPath();
        }

        // Remove everything up to the blob container name prefix in the url
        String blobContainerNamePrefix = "/" + blobContainerName + "/";
        int blobContainerNamePrefixIndex = blobName.indexOf(blobContainerNamePrefix);
        if (blobContainerNamePrefixIndex > -1) {
            blobName = blobName.substring(blobContainerNamePrefixIndex + blobContainerNamePrefix.length());
        }

        BlobClient client = containerClient.getBlobClient(blobName);
        client.upload(BinaryData.fromString(content), true);

        return blobName;
    }

    public void storePatientInBlobStorage(PatientReportingEvaluationStatus status, PatientReportingEvaluationStatus.Report report, MeasureReport measureReport) {
        storePatientInBlobStorage(status, report, measureReport, null);
    }

    public void storePatientInBlobStorage(PatientReportingEvaluationStatus status, PatientReportingEvaluationStatus.Report report, MeasureReport measureReport, org.apache.kafka.common.header.Headers inboundHeaders) {
        IParser jsonParser = this.fhirContext.newJsonParser()
                //.setSuppressNarratives(true)    // consider this if narrative "text" fields aren't desired downstream
                .setPrettyPrint(false);     // Ensure no tab delim so that it serializes to a single line per each resource

        ReportScheduleModel summary = this.reportClient.getReportSchedule(report.getReportTrackingId());
        String payloadUri = summary.getPayloadRootUri();

        if (payloadUri == null) {
            throw new ValidationException("Payload URI for report " + report.getReportTrackingId() + " is null");
        }

        String patientIdPart = "patient-" + status.getPatientId() + "-" + report.getReportType();
        String fileName = patientIdPart + ".mr";
        String patientPayloadUri = payloadUri.endsWith("/") ? payloadUri + fileName : payloadUri + "/" + fileName;

        StringBuilder sb = new StringBuilder();
        List<Resource> resources = this.normalize(measureReport);

        for (Resource resource : resources) {
            String idLine = resource.getResourceType().toString() + "/" + resource.getIdPart();       // i.e. Condition/A
            sb.append(idLine).append("\n");
            sb.append(jsonParser.encodeResourceToString(resource)).append("\n");
        }

        String blobName;

        try {
            // Upload to ABS
            blobName = this.upload(patientPayloadUri, sb.toString());
            logger.info("Uploaded patient payload for report {} to blob storage: {}", report.getReportTrackingId(), patientPayloadUri);
        } catch (Exception ex) {
            logger.error("Failed to upload patient payload to blob storage: {}", ex.getMessage(), ex);
            throw ex;
        }

        // Produce MeasureReportGenerated event
        this.measureReportGeneratedProducer.produceMeasureReportGeneratedRecord(status, report, measureReport.getIdPart(), patientPayloadUri, blobName, inboundHeaders);
    }

    private List<Resource> normalize(MeasureReport measureReport) {
        if (!measureReport.hasId()) {
            measureReport.setId(UUID.randomUUID().toString());
        }

        List<Resource> contained = measureReport.getContained();

        // Contained resource IDs are only guaranteed unique within a resource type (e.g. Condition/A and
        // Observation/A can legally coexist), so the primary lookup is type-qualified. A secondary,
        // id-part-only index is kept to resolve untyped "#id" references, but only when unambiguous.
        Map<String, Resource> containedByTypedId = new HashMap<>();
        Map<String, List<Resource>> containedByIdPart = new HashMap<>();
        for (Resource resource : contained) {
            String idPart = stripHash(resource.getIdPart());
            containedByTypedId.put(resource.getResourceType().name() + "/" + idPart, resource);
            containedByIdPart.computeIfAbsent(idPart, key -> new ArrayList<>()).add(resource);
        }

        measureReport.setContained(null);
        measureReport.setEvaluatedResource(null);
        for (Resource resource : contained) {
            String idPart = resource.getIdPart().replaceAll("(?i)^#?LCR-", "");
            IdType id = new IdType(resource.getResourceType().name(), idPart);
            resource.setIdElement(id);
            measureReport.addEvaluatedResource(new Reference(id));
        }
        for (Reference reference :
                fhirContext.newTerser().getAllPopulatedChildElementsOfType(measureReport, Reference.class)) {
            IIdType referenceElement = reference.getReferenceElement();
            String idPart = stripHash(referenceElement.getIdPart());
            String resourceType = referenceElement.getResourceType();

            Resource resource;
            if (resourceType != null) {
                resource = containedByTypedId.get(resourceType + "/" + idPart);
            } else {
                List<Resource> candidates = containedByIdPart.getOrDefault(idPart, List.of());
                if (candidates.size() > 1) {
                    String candidateTypes = candidates.stream()
                            .map(candidate -> candidate.getResourceType().name())
                            .collect(Collectors.joining(", "));
                    logger.warn("Contained reference '#{}' is ambiguous between {} resources ({}); leaving reference unresolved",
                            LogUtils.sanitize(idPart), candidates.size(), LogUtils.sanitize(candidateTypes));
                    continue;
                }
                resource = candidates.isEmpty() ? null : candidates.get(0);
            }

            if (resource == null) {
                continue;
            }
            reference.setReferenceElement(resource.getIdElement());
        }
        List<Resource> normalized = new ArrayList<>();
        normalized.add(measureReport);
        normalized.addAll(contained);
        return normalized;
    }

    private String stripHash(String idPart) {
        if (idPart == null) {
            return null;
        }
        return idPart.replaceAll("^#", "");
    }
}
