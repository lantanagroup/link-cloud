"""Tests for the config key matching rules and the required-key check.

Every test asserts both directions. A checker that never fails is worse than no checker, so
for each notation class there is a case that must pass and a case that must fail - otherwise a
rule that silently matches everything would look healthy.

Run with:
    python -m unittest discover Scripts/AzureAppConfig/tests
"""

import json
import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import check_required_config as checker  # noqa: E402
import config_key_matching as matching  # noqa: E402


def item(key, value="v", label=None, content_type=""):
    return {"key": key, "value": value, "label": label,
            "content_type": content_type, "tags": {}}


def index(*items):
    return matching.build_store_index(list(items))


class JavaNotation(unittest.TestCase):
    """Java keys are catalogued dotted and stored slash-separated."""

    def test_slash_row_satisfies_dotted_key(self):
        store = index(item("/spring/datasource/url"))
        self.assertTrue(matching.is_satisfied("spring.datasource.url", "java", store, None))

    def test_dotnet_runtime_does_not_get_the_slash_form(self):
        # A .NET entry must not be satisfied by a Java row; the runtimes read different keys.
        store = index(item("/spring/datasource/url"))
        self.assertFalse(matching.is_satisfied("spring.datasource.url", "dotnet", store, None))

    def test_absent_key_is_not_satisfied(self):
        store = index(item("/spring/datasource/username"))
        self.assertFalse(matching.is_satisfied("spring.datasource.url", "java", store, None))


class ArrayElements(unittest.TestCase):
    """A singular catalog key is satisfied by the stored elements of its array."""

    def test_element_zero_satisfies_the_singular_key(self):
        store = index(item("KafkaConnection:BootstrapServers:0"))
        self.assertTrue(
            matching.is_satisfied("KafkaConnection:BootstrapServers", "dotnet", store, None))

    def test_higher_index_alone_still_satisfies(self):
        store = index(item("CORS:AllowedHeaders:4"))
        self.assertTrue(matching.is_satisfied("CORS:AllowedHeaders", "dotnet", store, None))

    def test_a_different_key_with_a_numeric_tail_does_not_satisfy(self):
        store = index(item("KafkaConnection:BootstrapServersExtra:0"))
        self.assertFalse(
            matching.is_satisfied("KafkaConnection:BootstrapServers", "dotnet", store, None))


class Placeholders(unittest.TestCase):
    """A {Placeholder} segment stands for any single segment."""

    TEMPLATE = "ReverseProxy:Clusters:{Service}:Destinations:destination1:Address"

    def test_concrete_row_satisfies_the_template(self):
        store = index(item("ReverseProxy:Clusters:ReportService:Destinations:destination1:Address"))
        self.assertTrue(matching.is_satisfied(self.TEMPLATE, "dotnet", store, None))

    def test_placeholder_does_not_span_multiple_segments(self):
        store = index(item("ReverseProxy:Clusters:A:B:Destinations:destination1:Address"))
        self.assertFalse(matching.is_satisfied(self.TEMPLATE, "dotnet", store, None))


class JsonBlobs(unittest.TestCase):
    """A JSON-valued row is flattened by the provider into child properties."""

    BLOB = item("/authentication",
                json.dumps({"anonymous": False, "authority": "https://x", "adminEmail": ""}),
                content_type="application/json")

    def test_child_of_a_blob_is_satisfied(self):
        self.assertTrue(
            matching.is_satisfied("authentication.authority", "java", index(self.BLOB), None))

    def test_child_absent_from_the_blob_is_not_satisfied(self):
        self.assertFalse(
            matching.is_satisfied("authentication.issuer", "java", index(self.BLOB), None))

    def test_key_vault_reference_is_not_treated_as_a_blob(self):
        # A keyvaultref value is JSON, but its {"uri": ...} is not config structure.
        ref = item("ConnectionStrings:DatabaseConnection",
                   json.dumps({"uri": "https://v.vault.azure.net/secrets/x"}),
                   content_type="application/vnd.microsoft.appconfig.keyvaultref+json")
        self.assertFalse(
            matching.is_satisfied("ConnectionStrings:DatabaseConnection:uri", "dotnet",
                                  index(ref), None))


class RelaxedBinding(unittest.TestCase):
    """Spring treats camelCase, kebab-case and snake_case as one property."""

    def test_camel_and_kebab_are_the_same_property(self):
        self.assertEqual(matching.relax("telemetry.exporterEndpoint"),
                         matching.relax("telemetry.exporter-endpoint"))

    def test_different_properties_stay_different(self):
        self.assertNotEqual(matching.relax("telemetry.exporterEndpoint"),
                            matching.relax("telemetry.exporterEndpoints"))


class LabelScoping(unittest.TestCase):
    """Mirrors the two Select() calls each service issues: no label, then its own."""

    def test_unlabeled_row_satisfies_a_service_entry(self):
        store = index(item("AutoMigrate", label=None))
        self.assertTrue(matching.is_satisfied("AutoMigrate", "dotnet", store, "Census"))

    def test_own_label_satisfies_a_service_entry(self):
        store = index(item("AutoMigrate", label="Census"))
        self.assertTrue(matching.is_satisfied("AutoMigrate", "dotnet", store, "Census"))

    def test_another_services_label_does_not_satisfy(self):
        store = index(item("AutoMigrate", label="Report"))
        self.assertFalse(matching.is_satisfied("AutoMigrate", "dotnet", store, "Census"))

    def test_global_entry_is_satisfied_by_any_label(self):
        store = index(item("AutoMigrate", label="Report"))
        self.assertTrue(matching.is_satisfied("AutoMigrate", "dotnet", store, None))


class Guards(unittest.TestCase):
    """The three cheap invariants the check enforces on store labels and Serilog indices."""

    CATALOG = {
        "serviceMeta": {"Report": {"label": "Report", "runtime": "dotnet"}},
        "global": [],
        "services": {"Report": []},
    }

    def test_colon_in_a_label_is_an_error(self):
        stores = {"dev": [item("X", label="Report:Production")]}
        findings = checker.check_labels(self.CATALOG, stores)
        self.assertEqual(1, len(findings))
        self.assertEqual("ERROR", findings[0].severity)

    def test_label_absent_from_serviceMeta_is_an_error(self):
        stores = {"dev": [item("X", label="Automation")]}
        findings = checker.check_labels(self.CATALOG, stores)
        self.assertEqual(1, len(findings))
        self.assertIn("serviceMeta", findings[0].message)

    def test_known_label_is_clean(self):
        stores = {"dev": [item("X", label="Report"), item("Y", label=None)]}
        self.assertEqual([], checker.check_labels(self.CATALOG, stores))

    def test_serilog_pin_present_is_clean(self):
        catalog = {**self.CATALOG,
                   "global": [{"key": "Serilog:WriteTo:1:Args:uri", "description": "d",
                               "required": True}]}
        stores = {"dev": [item("Serilog:WriteTo:1:Name", "GrafanaLoki")]}
        self.assertEqual([], checker.check_serilog_sink_order(catalog, stores))

    def test_serilog_pin_missing_warns(self):
        catalog = {**self.CATALOG,
                   "global": [{"key": "Serilog:WriteTo:1:Args:uri", "description": "d",
                               "required": True}]}
        findings = checker.check_serilog_sink_order(catalog, {"dev": []})
        self.assertEqual(1, len(findings))
        self.assertEqual("WARN", findings[0].severity)

    def test_serilog_pin_pointing_elsewhere_warns(self):
        catalog = {**self.CATALOG,
                   "global": [{"key": "Serilog:WriteTo:1:Args:uri", "description": "d",
                               "required": True}]}
        stores = {"dev": [item("Serilog:WriteTo:1:Name", "Console")]}
        findings = checker.check_serilog_sink_order(catalog, stores)
        self.assertEqual(1, len(findings))
        self.assertIn("Console", findings[0].message)


class RequiredKeyCheck(unittest.TestCase):
    """End-to-end over the required-key rule itself."""

    CATALOG = {
        "serviceMeta": {"Report": {"label": "Report", "runtime": "dotnet"},
                        "Validation": {"label": "Validation", "runtime": "java"}},
        "global": [{"key": "KafkaConnection:BootstrapServers", "description": "d",
                    "required": True}],
        "services": {
            "Report": [{"key": "Report:Thing", "description": "d", "required": True}],
            "Validation": [{"key": "link.terminology-service-url", "description": "d",
                            "required": True}],
        },
    }

    def test_all_present_is_clean(self):
        store = index(item("KafkaConnection:BootstrapServers:0"),
                      item("Report:Thing", label="Report"),
                      item("/link/terminology-service-url"))
        self.assertEqual([], checker.check_required_keys(self.CATALOG, {"dev": store}))

    def test_each_missing_key_is_reported_by_name(self):
        store = index(item("KafkaConnection:BootstrapServers:0"))
        findings = checker.check_required_keys(self.CATALOG, {"dev": store})
        reported = " ".join(f.where for f in findings)
        self.assertEqual(2, len(findings))
        self.assertIn("Report:Thing", reported)
        self.assertIn("link.terminology-service-url", reported)

    def test_not_required_entries_are_ignored(self):
        catalog = {"serviceMeta": {}, "services": {},
                   "global": [{"key": "Absent:Key", "description": "d", "required": False}]}
        self.assertEqual([], checker.check_required_keys(catalog, {"dev": index()}))

    def test_missing_in_one_environment_only(self):
        present = index(item("KafkaConnection:BootstrapServers:0"),
                        item("Report:Thing", label="Report"),
                        item("/link/terminology-service-url"))
        absent = index(item("KafkaConnection:BootstrapServers:0"),
                       item("Report:Thing", label="Report"))
        findings = checker.check_required_keys(self.CATALOG, {"dev": present, "qa": absent})
        self.assertEqual(1, len(findings))
        self.assertIn("qa", findings[0].message)
        self.assertNotIn("dev,", findings[0].message)


KV = matching.KEY_VAULT_REF_CONTENT_TYPE + ";charset=utf-8"


def secret(key, label=None):
    """A row served as a Key Vault reference, as the exports record one."""
    return item(key, value='{"uri":"https://v.vault.azure.net/secrets/s"}',
                label=label, content_type=KV)


class SensitiveFlagTests(unittest.TestCase):
    """Both directions again: the catalog must not claim more, or less, than the stores do."""

    @staticmethod
    def catalog(entry):
        return {"serviceMeta": {}, "services": {}, "global": [entry]}

    def test_key_vault_backed_key_must_be_marked_sensitive(self):
        cat = self.catalog({"key": "A:Secret", "description": "d", "required": True})
        findings = checker.check_sensitive_flags(cat, {"dev": [secret("A:Secret")]})
        self.assertEqual(1, len(findings))
        self.assertEqual("ERROR", findings[0].severity)

    def test_marked_sensitive_and_key_vault_backed_is_clean(self):
        cat = self.catalog({"key": "A:Secret", "description": "d", "required": True,
                            "sensitive": True})
        self.assertEqual([], checker.check_sensitive_flags(cat, {"dev": [secret("A:Secret")]}))

    def test_sensitive_entry_stored_as_a_literal_is_an_error(self):
        cat = self.catalog({"key": "A:Secret", "description": "d", "required": True,
                            "sensitive": True})
        findings = checker.check_sensitive_flags(cat, {"dev": [item("A:Secret", "hunter2")]})
        self.assertEqual(1, len(findings))
        self.assertEqual("ERROR", findings[0].severity)
        self.assertIn("public repository", findings[0].message)

    def test_one_key_vault_environment_is_enough(self):
        """Mixed backing is not flagged: the key is a secret somewhere, so the flag is right."""
        cat = self.catalog({"key": "A:Secret", "description": "d", "required": True,
                            "sensitive": True})
        stores = {"dev": [secret("A:Secret")], "qa": [item("A:Secret", "local-dev-value")]}
        self.assertEqual([], checker.check_sensitive_flags(cat, stores))

    def test_uncatalogued_key_vault_row_warns_rather_than_fails(self):
        cat = self.catalog({"key": "Other:Key", "description": "d", "required": False})
        findings = checker.check_sensitive_flags(cat, {"dev": [secret("Rogue:Secret")]})
        self.assertEqual(1, len(findings))
        self.assertEqual("WARN", findings[0].severity)
        self.assertIn("Rogue:Secret", findings[0].where)

    def test_java_slash_notation_matches_the_dotted_catalog_entry(self):
        cat = self.catalog({"key": "spring.datasource.password", "description": "d",
                            "required": True, "sensitive": True, "runtime": "java"})
        stores = {"dev": [secret("/spring/datasource/password")]}
        self.assertEqual([], checker.check_sensitive_flags(cat, stores))

    def test_java_slash_notation_is_still_held_to_the_flag(self):
        cat = self.catalog({"key": "spring.datasource.password", "description": "d",
                            "required": True, "runtime": "java"})
        findings = checker.check_sensitive_flags(
            cat, {"dev": [secret("/spring/datasource/password")]})
        self.assertEqual(1, len(findings))
        self.assertEqual("ERROR", findings[0].severity)

    def test_plain_rows_alone_produce_nothing(self):
        cat = self.catalog({"key": "A:Plain", "description": "d", "required": True})
        self.assertEqual([], checker.check_sensitive_flags(cat, {"dev": [item("A:Plain")]}))


class ExportLocation(unittest.TestCase):
    """Where the tooling looks for link-cac's exports.

    Every script defaults its --config-dir through this one function, so a wrong answer here
    silently repoints all of them - and the failure mode is a check that reads no exports at
    all rather than one that reads the wrong ones.
    """

    def setUp(self):
        self.saved = os.environ.pop("LINK_CAC_CONFIG_DIR", None)

    def tearDown(self):
        os.environ.pop("LINK_CAC_CONFIG_DIR", None)
        if self.saved is not None:
            os.environ["LINK_CAC_CONFIG_DIR"] = self.saved

    def test_defaults_to_a_sibling_clone(self):
        self.assertEqual(os.path.join("..", "link-cac", "Config"),
                         matching.default_config_dir())

    def test_environment_variable_wins(self):
        os.environ["LINK_CAC_CONFIG_DIR"] = "D:/elsewhere/Config"
        self.assertEqual("D:/elsewhere/Config", matching.default_config_dir())

    def test_empty_environment_variable_falls_back(self):
        # An exported-but-empty variable is the shape a CI step that forgot to set it takes.
        # Honouring it would resolve every export path to a bare filename in the CWD.
        os.environ["LINK_CAC_CONFIG_DIR"] = ""
        self.assertEqual(os.path.join("..", "link-cac", "Config"),
                         matching.default_config_dir())

    def test_missing_export_hint_names_the_directory_it_looked_in(self):
        catalog = {"environments": {"dev": {"store": "nhsnlink-ac-dev"}}}
        hint = matching.missing_export_hint(catalog, "dev", config_dir="D:/elsewhere/Config")
        self.assertIn("D:/elsewhere/Config", hint)
        self.assertIn("LINK_CAC_CONFIG_DIR", hint)
        self.assertIn("nhsnlink-ac-dev", hint)


if __name__ == "__main__":
    unittest.main()
