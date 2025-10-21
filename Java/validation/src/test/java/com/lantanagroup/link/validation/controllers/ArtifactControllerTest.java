package com.lantanagroup.link.validation.controllers;

import com.lantanagroup.link.validation.entities.Artifact;
import com.lantanagroup.link.validation.entities.ArtifactType;
import com.lantanagroup.link.validation.models.PackageDetailsModel;
import com.lantanagroup.link.validation.models.TerminologyDependency;
import com.lantanagroup.link.validation.repositories.ArtifactRepository;
import com.lantanagroup.link.validation.services.ArtifactService;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.autoconfigure.web.servlet.WebMvcTest;
import org.springframework.boot.test.mock.mockito.MockBean;
import org.springframework.test.web.servlet.MockMvc;

import java.util.List;
import java.util.Optional;

import static org.mockito.Mockito.when;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@WebMvcTest(ArtifactController.class)
@AutoConfigureMockMvc(addFilters = false)
class ArtifactControllerTest {

    @Autowired
    private MockMvc mockMvc;

    @MockBean
    private ArtifactRepository artifactRepository;

    @MockBean
    private ArtifactService artifactService;

    @Test
    void getTerminologyDependencies() throws Exception {
        TerminologyDependency dep = new TerminologyDependency("http://test.com/ValueSet", "1.0", true, true);
        when(artifactService.getTerminologyDependencies()).thenReturn(List.of(dep));

        mockMvc.perform(get("/api/validation/artifact/tx-dependencies"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$[0].url").value("http://test.com/ValueSet"))
                .andExpect(jsonPath("$[0].version").value("1.0"))
                .andExpect(jsonPath("$[0].resourceExists").value(true))
                .andExpect(jsonPath("$[0].versionExists").value(true));
    }

    @Test
    void getTerminologyDependencies_WithPackage() throws Exception {
        String packageId = "test-package";
        TerminologyDependency dep = new TerminologyDependency("http://test.com/ValueSet", "1.0", true, true);

        when(artifactRepository.findByTypeAndName(ArtifactType.PACKAGE, packageId)).thenReturn(Optional.of(new Artifact()));
        when(artifactService.getTerminologyDependencies(packageId)).thenReturn(List.of(dep));

        mockMvc.perform(get("/api/validation/artifact/PACKAGE/" + packageId + "/tx-dependencies"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$[0].url").value("http://test.com/ValueSet"))
                .andExpect(jsonPath("$[0].version").value("1.0"))
                .andExpect(jsonPath("$[0].resourceExists").value(true))
                .andExpect(jsonPath("$[0].versionExists").value(true));
    }

    @Test
    void getTerminologyDependencies_WithPackage_NotFound() throws Exception {
        String packageId = "non-existent";
        when(artifactRepository.findByTypeAndName(ArtifactType.PACKAGE, packageId)).thenReturn(Optional.empty());

        mockMvc.perform(get("/api/validation/artifact/PACKAGE/" + packageId + "/tx-dependencies"))
                .andExpect(status().isNotFound());
    }

    @Test
    void getPackageDetails() throws Exception {
        String packageId = "test-package";
        PackageDetailsModel model = new PackageDetailsModel();
        model.setVersion("1.0.0");
        PackageDetailsModel.Resource resource = new PackageDetailsModel.Resource();
        resource.setResourceType("StructureDefinition");
        resource.setId("test-sd");
        resource.setUrl("http://test.com/sd");
        resource.setName("TestSD");
        resource.setVersion("0.1");
        model.setResources(List.of(resource));

        when(artifactService.getPackageDetails(packageId)).thenReturn(model);

        mockMvc.perform(get("/api/validation/artifact/PACKAGE/" + packageId))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.version").value("1.0.0"))
                .andExpect(jsonPath("$.resources[0].resourceType").value("StructureDefinition"))
                .andExpect(jsonPath("$.resources[0].id").value("test-sd"));
    }

    @Test
    void getPackageDetails_NotFound() throws Exception {
        String packageId = "non-existent";
        when(artifactService.getPackageDetails(packageId)).thenReturn(null);

        mockMvc.perform(get("/api/validation/artifact/PACKAGE/" + packageId))
                .andExpect(status().isNotFound());
    }
}
