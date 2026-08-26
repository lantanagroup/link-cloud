"""Focused tests for Azure App Configuration secret validation."""

import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

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
