package com.lantanagroup.link.validation.controllers;

import com.lantanagroup.link.validation.configs.MethodSecurityConfig;
import com.lantanagroup.link.validation.models.RubricSettingsDto;
import com.lantanagroup.link.validation.services.RubricRegistryService;
import com.lantanagroup.link.validation.services.RubricSettingsService;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.autoconfigure.web.servlet.WebMvcTest;
import org.springframework.boot.test.mock.mockito.MockBean;
import org.springframework.context.annotation.Import;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.authority.SimpleGrantedAuthority;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.test.context.TestPropertySource;
import org.springframework.test.web.servlet.MockMvc;

import java.util.Arrays;
import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

/**
 * Who may call the rubric registry. It is for Link administrators: a caller with the {@code IsLinkAdmin}
 * permission. The other controller tests leave permissions out; these put them in.
 *
 * <p>The security filters are off, as in the other controller tests, so the caller is set where the
 * filters would have set it. What is under test is what the endpoints require of that caller.
 */
class RubricControllerSecurityTest {

    private static final String BASE = "/api/validation/rubrics";

    private static void signInWith(String... authorities) {
        SecurityContextHolder.getContext().setAuthentication(new UsernamePasswordAuthenticationToken(
                "someone", null, Arrays.stream(authorities).map(SimpleGrantedAuthority::new).toList()));
    }

    @Nested
    @WebMvcTest(RubricController.class)
    @AutoConfigureMockMvc(addFilters = false)
    @Import(MethodSecurityConfig.class)
    @TestPropertySource(properties = "authentication.anonymous=false")
    @DisplayName("when callers must sign in")
    class Enforced {

        @Autowired
        private MockMvc mockMvc;

        @MockBean
        private RubricRegistryService registry;

        @MockBean
        private RubricSettingsService settings;

        @AfterEach
        void signOut() {
            SecurityContextHolder.clearContext();
        }

        @Test
        @DisplayName("a Link administrator is let through")
        void administrator() throws Exception {
            signInWith("ROLE_LinkAdministrator", "IsLinkAdmin");
            when(settings.getSettings()).thenReturn(RubricSettingsDto.builder().build());

            mockMvc.perform(get(BASE + "/$settings")).andExpect(status().isOk());
            mockMvc.perform(get(BASE + "/$owners")).andExpect(status().isOk());
        }

        @Test
        @DisplayName("a signed-in caller without the permission is refused, to read as well as to change")
        void notAnAdministrator() throws Exception {
            signInWith("ROLE_LinkUser");

            mockMvc.perform(get(BASE))
                    .andExpect(status().isForbidden())
                    .andExpect(jsonPath("$.status").value(403))
                    .andExpect(jsonPath("$.message").value("You do not have permission to do this."))
                    .andExpect(jsonPath("$.data").doesNotExist());
            mockMvc.perform(get(BASE + "/$settings")).andExpect(status().isForbidden());
            mockMvc.perform(get(BASE + "/piqi.core/versions/1.0.0")).andExpect(status().isForbidden());
            mockMvc.perform(post(BASE + "/piqi.core/versions/1.0.0/$publish")).andExpect(status().isForbidden());
            mockMvc.perform(post(BASE + "/piqi.core/versions/1.0.0/$retire")).andExpect(status().isForbidden());
            mockMvc.perform(delete(BASE + "/piqi.core/versions/1.0.0")).andExpect(status().isForbidden());

            verify(registry, never()).publish(any(), any(), any());
            verify(registry, never()).retire(any(), any(), any());
            verify(registry, never()).deleteDraft(any(), any(), any());
        }

        @Test
        @DisplayName("the role without the permission is not enough")
        void roleAlone() throws Exception {
            signInWith("ROLE_LinkAdministrator");

            mockMvc.perform(get(BASE + "/$owners")).andExpect(status().isForbidden());
        }

        @Test
        @DisplayName("a caller that is not signed in is refused as such, not as a server error")
        void notSignedIn() throws Exception {
            mockMvc.perform(get(BASE + "/$owners"))
                    .andExpect(status().isUnauthorized())
                    .andExpect(jsonPath("$.status").value(401));
        }
    }

    @Nested
    @WebMvcTest(RubricController.class)
    @AutoConfigureMockMvc(addFilters = false)
    @Import(MethodSecurityConfig.class)
    @TestPropertySource(properties = "authentication.anonymous=true")
    @DisplayName("when the service runs with anonymous access")
    class Anonymous {

        @Autowired
        private MockMvc mockMvc;

        @MockBean
        private RubricRegistryService registry;

        @MockBean
        private RubricSettingsService settings;

        @Test
        @DisplayName("nobody is checked: local development and the compose stack have no caller to check")
        void open() throws Exception {
            when(registry.listOwners()).thenReturn(List.of("lcg"));

            mockMvc.perform(get(BASE + "/$owners")).andExpect(status().isOk());
        }
    }

    @Test
    @DisplayName("every rubric endpoint is covered: the requirement is on the controller, and on the dry run")
    void whereTheRequirementIs() throws Exception {
        assertThat(RubricController.class.getAnnotation(PreAuthorize.class).value())
                .isEqualTo("hasAuthority('IsLinkAdmin')");

        var dryRun = Arrays.stream(ValidationController.class.getDeclaredMethods())
                .filter(method -> method.getName().equals("dryRun"))
                .findFirst().orElseThrow();
        assertThat(dryRun.getAnnotation(PreAuthorize.class).value()).isEqualTo("hasAuthority('IsLinkAdmin')");
    }
}
