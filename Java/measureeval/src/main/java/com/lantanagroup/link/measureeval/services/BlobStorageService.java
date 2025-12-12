package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.parser.IParser;
import com.azure.core.util.BinaryData;
import com.azure.storage.blob.BlobClient;
import com.azure.storage.blob.BlobContainerClient;
import com.azure.storage.blob.BlobServiceClient;
import com.azure.storage.blob.BlobServiceClientBuilder;
import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import com.lantanagroup.link.shared.entities.ReportScheduleSummaryModel;
import com.lantanagroup.link.shared.exceptions.ValidationException;
import com.lantanagroup.link.shared.services.ReportClient;
import org.hl7.fhir.r4.model.IdType;
import org.hl7.fhir.r4.model.MeasureReport;
import org.hl7.fhir.r4.model.Reference;
import org.hl7.fhir.r4.model.Resource;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.function.Function;
import java.util.stream.Collectors;

public class BlobStorageService {
    private static final Logger logger = LoggerFactory.getLogger(BlobStorageService.class);
    private final BlobContainerClient containerClient;
    private final FhirContext fhirContext;
    private final ReportClient reportClient;
    private final MeasureReportGeneratedProducer measureReportGeneratedProducer;

    public BlobStorageService(String connectionString, String blobContainerName, FhirContext fhirContext, ReportClient reportClient, MeasureReportGeneratedProducer measureReportGeneratedProducer) {
        this.fhirContext = fhirContext;
        this.reportClient = reportClient;
        this.measureReportGeneratedProducer = measureReportGeneratedProducer;
        BlobServiceClient serviceClient = new BlobServiceClientBuilder()
                .connectionString(connectionString)
                .buildClient();
        containerClient = serviceClient.getBlobContainerClient(blobContainerName);
    }

    public void upload(String blobName, String content) {
        BlobClient client = containerClient.getBlobClient(blobName);
        client.upload(BinaryData.fromString(content));
    }

    public void storePatientInBlobStorage(PatientReportingEvaluationStatus status, MeasureReport measureReport) {
        IParser jsonParser = this.fhirContext.newJsonParser()
                //.setSuppressNarratives(true)    // consider this if narrative "text" fields aren't desired downstream
                .setPrettyPrint(false);     // Ensure no tab delim so that it serializes to a single line per each resource

        for (PatientReportingEvaluationStatus.Report report : status.getReports()) {
            ReportScheduleSummaryModel summary = this.reportClient.getReportScheduleSummaryModel(status.getFacilityId(), report.getReportTrackingId());
            String payloadUri = summary.getPayloadRootUri();

            if (payloadUri == null) {
                throw new ValidationException("Payload URI for report " + report.getReportTrackingId() + " is null");
            }

            String patientIdPart = "patient-" + status.getPatientId();
            String patientPayloadUri = payloadUri.endsWith("/") ? payloadUri + patientIdPart : payloadUri + "/" + patientIdPart;
            StringBuilder sb = new StringBuilder();
            List<Resource> resources = this.normalize(measureReport);
            resources.add(0, measureReport);

            for (Resource resource : resources) {
                String idLine = resource.getResourceType() + "/" + resource.getId();
                sb.append(idLine).append("\n");
                sb.append(jsonParser.encodeResourceToString(resource)).append("\n");
            }
            
            try {
                // Upload to ABS
                this.upload(patientPayloadUri, sb.toString());
            } catch (Exception ex) {
                logger.error("Failed to upload patient payload to blob storage: {}", ex.getMessage(), ex);
                throw ex;
            }

            // Produce MeasureReportGenerated event
            this.measureReportGeneratedProducer.produceMeasureReportGeneratedRecord(
                    status,
                    report,
                    measureReport,
                    patientPayloadUri
            );
        }
    }

    private List<Resource> normalize(MeasureReport measureReport) {
        if (!measureReport.hasId()) {
            measureReport.setId(UUID.randomUUID().toString());
        }

        List<Resource> contained = measureReport.getContained();
        Map<String, Resource> containedByIdPart = contained.stream().collect(Collectors.toMap(
                resource -> stripHash(resource.getIdPart()),
                Function.identity()));
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
            String idPart = stripHash(reference.getReferenceElement().getIdPart());
            Resource resource = containedByIdPart.get(idPart);
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
