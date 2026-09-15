"""Focused tests for Azure App Configuration secret validation."""

import contextlib
import io
import json
import os
import shutil
import sys
import tempfile
import unittest
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import config_findings as findings_mod  # noqa: E402
import validate_aac_secrets as validator  # noqa: E402


def item(key, value):
    return {"key": key, "value": value, "label": None, "content_type": ""}


class PasswordlessRedisConnectionStringTests(unittest.TestCase):
    """Redis options are allowed, but the password belongs in Key Vault."""

    KEY = "ConnectionStrings:Redis"

    def test_connection_parameters_are_allowed(self):
        findings = validator.check_item(
            "config.json", 0, item(
                self.KEY,
                "redis.example:10000,abortConnect=false,resolveDns=true,allowAdmin=true"))

        self.assertEqual([], findings)

    def test_password_parameter_is_an_error(self):
        findings = validator.check_item(
            "config.json", 0, item(self.KEY, "redis.example:10000,password=abc"))

        self.assertEqual(1, len(findings))
        self.assertEqual("ERROR", findings[0].severity)
        self.assertIn("inline password", findings[0].message)

    def test_pwd_parameter_is_an_error(self):
        findings = validator.check_item(
            "config.json", 0, item(self.KEY, "redis.example:10000,pwd=abc"))

        self.assertEqual(1, len(findings))
        self.assertEqual("ERROR", findings[0].severity)
        self.assertIn("inline password", findings[0].message)

class NonProductionFixtureSecretTests(unittest.TestCase):
    """The mock DMRP fixture credentials are exempt from the key-name warning only."""

    CLIENT_SECRET = "MockDmrpApi:AuthClientSecret"
    SIGNING_KEY = "MockDmrpApi:SigningKey"

    def test_fixture_client_secret_is_allowed(self):
        findings = validator.check_item(
            "config.json", 0,
            item(self.CLIENT_SECRET, "hcMQx4r02OkwoGvAAvUqOFkh3X-cOH2l4N9knjjJgsI"))

        self.assertEqual([], findings)

    def test_fixture_signing_key_is_allowed(self):
        findings = validator.check_item(
            "config.json", 0, item(self.SIGNING_KEY, "L5MEH2KTYjZevwKb5FIpY" + "x" * 65))

        self.assertEqual([], findings)

    def test_exemption_is_case_insensitive(self):
        findings = validator.check_item(
            "config.json", 0, item("mockdmrpapi:signingkey", "a" * 80))

        self.assertEqual([], findings)

    def test_exempt_key_still_errors_on_a_real_credential(self):
        """The exemption must not become a place to park an actual secret."""
        findings = validator.check_item(
            "config.json", 0, item(
                self.CLIENT_SECRET,
                "mongodb://admin:hunter2@cluster.example.net:27017/link"))

        self.assertEqual(1, len(findings))
        self.assertEqual("ERROR", findings[0].severity)
        self.assertIn("MongoDB URI", findings[0].message)

    def test_exempt_key_still_errors_on_a_malformed_vault_reference(self):
        findings = validator.check_item(
            "config.json", 0, item(
                self.SIGNING_KEY,
                "https://nhsnlink-kv-qa.vault.azure.net/secrets/mock-dmrp-signing-key"))

        self.assertEqual(1, len(findings))
        self.assertEqual("ERROR", findings[0].severity)

    def test_sibling_mock_key_is_not_exempted(self):
        """Scoped to exact keys, so a future MockDmrpApi secret is still reported."""
        findings = validator.check_item(
            "config.json", 0, item("MockDmrpApi:UpstreamApiKey", "literal-value-here"))

        self.assertEqual(1, len(findings))
        self.assertEqual("WARN", findings[0].severity)


class DefaultExportDiscoveryTests(unittest.TestCase):
    """main() resolves its own default glob, so a missing link-cac must be reported.

    resolve_paths() echoes a pattern back when it matches nothing, so `paths` is never
    empty and cannot be used to detect a missing checkout -- the guard has to test the
    default separately. It was unreachable dead code before, hence these tests.
    """

    def setUp(self):
        self.tmp = tempfile.mkdtemp()
        self.addCleanup(shutil.rmtree, self.tmp, True)

    def run_main(self, argv, config_dir):
        """Invoke main() with the export directory swapped for a temporary one."""
        with mock.patch.object(validator.matching, "default_config_dir",
                               return_value=config_dir),              mock.patch.object(validator.sys, "argv", ["validate_aac_secrets.py"] + argv),              contextlib.redirect_stdout(io.StringIO()),              contextlib.redirect_stderr(io.StringIO()):
            return validator.main()

    def write_export(self, name, items):
        path = os.path.join(self.tmp, name)
        with open(path, "w", encoding="utf-8") as handle:
            json.dump({"items": items}, handle)
        return path

    def test_missing_default_exports_are_unusable_not_clean(self):
        """An absent link-cac must not read as 'nothing wrong here'."""
        self.assertEqual(findings_mod.EXIT_UNUSABLE,
                         self.run_main([], os.path.join(self.tmp, "nonexistent")))

    def test_matching_default_exports_are_scanned(self):
        self.write_export("app-config.dev.json", [item("Logging:LogLevel", "Information")])

        self.assertEqual(0, self.run_main([], self.tmp))

    def test_default_glob_skips_the_derived_inventory(self):
        """config-key-inventory.json sits beside the exports and is not one."""
        self.write_export("app-config.dev.json", [item("Logging:LogLevel", "Information")])
        with open(os.path.join(self.tmp, "config-key-inventory.json"), "w",
                  encoding="utf-8") as handle:
            handle.write("not an export")

        self.assertEqual(0, self.run_main([], self.tmp))

    def test_a_credential_in_a_default_export_still_fails(self):
        """The guard short-circuits only when nothing matched, never a real scan."""
        self.write_export("app-config.qa.json",
                          [item("Some:Key", "AccountKey=" + "A" * 32)])

        self.assertEqual(findings_mod.EXIT_FINDINGS, self.run_main([], self.tmp))

    def test_explicit_missing_path_keeps_the_file_load_error(self):
        """An explicit path is the caller's mistake; the checkout guard must not eat it."""
        with self.assertRaises(SystemExit) as caught:
            self.run_main([os.path.join(self.tmp, "absent.json")], self.tmp)

        self.assertEqual(findings_mod.EXIT_UNUSABLE, caught.exception.code)
