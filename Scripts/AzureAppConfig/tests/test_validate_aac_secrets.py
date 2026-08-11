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