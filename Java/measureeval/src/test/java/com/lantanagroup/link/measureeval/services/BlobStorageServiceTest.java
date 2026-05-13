package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import com.azure.core.util.BinaryData;
import com.azure.storage.blob.BlobClient;
import com.azure.storage.blob.BlobContainerClient;
import com.lantanagroup.link.measureeval.entities.PatientReportingEvaluationStatus;
import com.lantanagroup.link.shared.entities.ReportScheduleModel;
import com.lantanagroup.link.shared.exceptions.ValidationException;
import com.lantanagroup.link.shared.services.ReportClient;
import org.hl7.fhir.r4.model.MeasureReport;
import org.hl7.fhir.r4.model.Patient;
import org.hl7.fhir.r4.model.Reference;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;

import java.util.List;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.Mockito.*;

class BlobStorageServiceTest {
    private BlobContainerClient containerClient;
    private BlobClient blobClient;
    private FhirContext fhirContext;
    private ReportClient reportClient;
    private MeasureReportGeneratedProducer measureReportGeneratedProducer;
    private BlobStorageService blobStorageService;
    private final String containerName = "test-container";

    @BeforeEach
    void setUp() {
        containerClient = mock(BlobContainerClient.class);
        blobClient = mock(BlobClient.class);
        fhirContext = FhirContext.forR4();
        reportClient = mock(ReportClient.class);
        measureReportGeneratedProducer = mock(MeasureReportGeneratedProducer.class);

        when(containerClient.getBlobClient(anyString())).thenReturn(blobClient);

        blobStorageService = new BlobStorageService(containerClient, containerName, fhirContext, reportClient, measureReportGeneratedProducer);
    }

    @Test
    void testUploadBasic() {
        String blobName = "test.txt";
        String content = "Hello World";

        String result = blobStorageService.upload(blobName, content);

        assertEquals(blobName, result);
        verify(containerClient).getBlobClient(blobName);
        verify(blobClient).upload(any(BinaryData.class), eq(true));
    }

    @Test
    void testUploadWithUri() {
        String blobUri = "https://account.blob.core.windows.net/test-container/path/to/blob.txt";
        String content = "Hello World";

        String result = blobStorageService.upload(blobUri, content);

        assertEquals("path/to/blob.txt", result);
        verify(containerClient).getBlobClient("path/to/blob.txt");
    }

    @Test
    void testUploadWithProtocolHostPort() {
        String blobUri = "http://localhost:10000/devstoreaccount1/test-container/my-blob.mr";
        String content = "Hello World";

        String result = blobStorageService.upload(blobUri, content);

        // URI.getPath() for http://localhost:10000/devstoreaccount1/test-container/my-blob.mr 
        // will be /devstoreaccount1/test-container/my-blob.mr
        // Then blobContainerNamePrefix = "/test-container/"
        // indexOf("/test-container/") will be found.
        // substring will return "my-blob.mr"
        assertEquals("my-blob.mr", result);
        verify(containerClient).getBlobClient("my-blob.mr");
    }

    @Test
    void testStorePatientInBlobStorageSuccess() {
        PatientReportingEvaluationStatus status = new PatientReportingEvaluationStatus();
        status.setPatientId("patient1");
        PatientReportingEvaluationStatus.Report report = new PatientReportingEvaluationStatus.Report();
        report.setReportTrackingId("track1");
        report.setReportType("type1");
        status.setReports(List.of(report));

        ReportScheduleModel schedule = new ReportScheduleModel();
        schedule.setPayloadRootUri("https://storage.com/test-container/root/");
        when(reportClient.getReportSchedule("track1")).thenReturn(schedule);

        MeasureReport measureReport = new MeasureReport();
        measureReport.setId("mr1");

        blobStorageService.storePatientInBlobStorage(status, report, measureReport);

        String expectedBlobName = "root/patient-patient1-type1.mr";
        verify(containerClient).getBlobClient(expectedBlobName);
        verify(blobClient).upload(any(BinaryData.class), eq(true));
        verify(measureReportGeneratedProducer).produceMeasureReportGeneratedRecord(eq(status), eq(report), eq(measureReport.getIdPart()), anyString(), eq(expectedBlobName));
    }

    @Test
    void testStorePatientInBlobStorageWithContainedResources() {
        PatientReportingEvaluationStatus status = new PatientReportingEvaluationStatus();
        status.setPatientId("patient1");
        PatientReportingEvaluationStatus.Report report = new PatientReportingEvaluationStatus.Report();
        report.setReportTrackingId("track1");
        report.setReportType("type1");
        status.setReports(List.of(report));

        ReportScheduleModel schedule = new ReportScheduleModel();
        schedule.setPayloadRootUri("https://storage.com/test-container/root/");
        when(reportClient.getReportSchedule("track1")).thenReturn(schedule);

        MeasureReport measureReport = new MeasureReport();
        measureReport.setId("mr1");
        
        Patient patient = new Patient();
        patient.setId("#LCR-patient-A");
        measureReport.addContained(patient);
        measureReport.setSubject(new Reference("#LCR-patient-A"));

        blobStorageService.storePatientInBlobStorage(status, report, measureReport);

        ArgumentCaptor<BinaryData> contentCaptor = ArgumentCaptor.forClass(BinaryData.class);
        verify(blobClient).upload(contentCaptor.capture(), eq(true));

        String content = contentCaptor.getValue().toString();
        // Check if patient ID was normalized
        assertTrue(content.contains("Patient/patient-A"));
        // Check if reference was updated
        assertTrue(content.contains("\"reference\":\"Patient/patient-A\""));
        // Check if MeasureReport ID line is present
        assertTrue(content.contains("MeasureReport/mr1"));
    }

    @Test
    void testStorePatientInBlobStoragePayloadUriNull() {
        PatientReportingEvaluationStatus status = new PatientReportingEvaluationStatus();
        status.setPatientId("patient1");
        PatientReportingEvaluationStatus.Report report = new PatientReportingEvaluationStatus.Report();
        report.setReportTrackingId("track1");
        status.setReports(List.of(report));

        ReportScheduleModel schedule = new ReportScheduleModel();
        schedule.setPayloadRootUri(null);
        when(reportClient.getReportSchedule("track1")).thenReturn(schedule);

        MeasureReport measureReport = new MeasureReport();

        assertThrows(ValidationException.class, () -> {
            blobStorageService.storePatientInBlobStorage(status, report, measureReport);
        });
    }

    @Test
    void testStorePatientInBlobStorageUploadFails() {
        PatientReportingEvaluationStatus status = new PatientReportingEvaluationStatus();
        status.setPatientId("patient1");
        PatientReportingEvaluationStatus.Report report = new PatientReportingEvaluationStatus.Report();
        report.setReportTrackingId("track1");
        status.setReports(List.of(report));

        ReportScheduleModel schedule = new ReportScheduleModel();
        schedule.setPayloadRootUri("https://storage.com/test-container/root/");
        when(reportClient.getReportSchedule("track1")).thenReturn(schedule);

        MeasureReport measureReport = new MeasureReport();

        doThrow(new RuntimeException("Upload failed")).when(blobClient).upload(any(BinaryData.class), anyBoolean());

        assertThrows(RuntimeException.class, () -> {
            blobStorageService.storePatientInBlobStorage(status, report, measureReport);
        });
    }

    @Test
    void testStorePatientInBlobStorageMeasureReportNoId() {
        PatientReportingEvaluationStatus status = new PatientReportingEvaluationStatus();
        status.setPatientId("patient1");
        PatientReportingEvaluationStatus.Report report = new PatientReportingEvaluationStatus.Report();
        report.setReportTrackingId("track1");
        status.setReports(List.of(report));

        ReportScheduleModel schedule = new ReportScheduleModel();
        schedule.setPayloadRootUri("https://storage.com/test-container/root/");
        when(reportClient.getReportSchedule("track1")).thenReturn(schedule);

        MeasureReport measureReport = new MeasureReport();
        // ID is not set

        blobStorageService.storePatientInBlobStorage(status, report, measureReport);

        assertNotNull(measureReport.getId());

        ArgumentCaptor<BinaryData> contentCaptor = ArgumentCaptor.forClass(BinaryData.class);
        verify(blobClient).upload(contentCaptor.capture(), eq(true));
        String content = contentCaptor.getValue().toString();
        assertTrue(content.contains("MeasureReport/" + measureReport.getIdPart()));
    }
}
