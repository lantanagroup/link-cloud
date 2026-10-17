"""Protocol integration checks. Build the project first; uses only Python's standard library."""
import json
import os
import socket
from pathlib import Path
import subprocess
import threading
import time
import unittest
import urllib.error
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer


class Tenant(BaseHTTPRequestHandler):
    mode = "ok"

    def do_GET(self):
        if self.mode == "disconnected":
            self.close_connection = True
            return
        if self.mode == "unauthorized":
            self.send_response(401)
            self.end_headers()
            return
        if self.mode == "invalid":
            value = {"unexpected": "shape"}
        elif self.mode == "empty":
            value = []
        elif self.path == "/api/Vendor":
            value = [{"id": "11111111-1111-1111-1111-111111111111", "name": "Test vendor",
                      "authentication": {"secret": "must-never-reach-model"}}]
        elif self.path == "/api/VendorVersion":
            value = [{"id": "22222222-2222-2222-2222-222222222222",
                      "vendorId": "11111111-1111-1111-1111-111111111111", "version": "Test version",
                      "authentication": {"secret": "must-never-reach-model"}}]
        else:
            self.send_response(404)
            self.end_headers()
            return
        body = json.dumps(value).encode()
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, *_):
        pass


class MeasureEval(Tenant):
    mode = "ok"

    def do_GET(self):
        if self.mode != "ok":
            return super().do_GET()
        if self.path != "/api/measureeval/measure-definition":
            self.send_response(404)
            self.end_headers()
            return
        body = json.dumps([{"id": "test-definition", "version": 7,
                            "bundle": {"secret": "must-never-reach-model"}}]).encode()
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)


class ProtocolTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.port = 8080 if os.environ.get("DOTNET_RUNNING_IN_CONTAINER", "").lower() == "true" else 8090
        # Do not accidentally run tests against someone else's MCP server.
        with socket.socket() as probe:
            probe.bind(("127.0.0.1", cls.port))
        cls.tenant = ThreadingHTTPServer(("127.0.0.1", 0), Tenant)
        threading.Thread(target=cls.tenant.serve_forever, daemon=True).start()
        cls.measureeval = ThreadingHTTPServer(("127.0.0.1", 0), MeasureEval)
        threading.Thread(target=cls.measureeval.serve_forever, daemon=True).start()
        env = dict(os.environ)
        env["Logging__LogLevel__Default"] = "Critical"
        env["Logging__LogLevel__Microsoft.Hosting.Lifetime"] = "Critical"
        env["Onboarding__Environments__0__TenantApiBaseUrl"] = (
            f"http://127.0.0.1:{cls.tenant.server_port}/api/")
        env["Onboarding__Environments__0__MeasureEvalApiBaseUrl"] = (
            f"http://127.0.0.1:{cls.measureeval.server_port}/api/measureeval/")
        # A legacy manual catalog must never replace the live service response.
        env["Onboarding__Environments__0__Measures__0__Id"] = "obsolete-config-definition"
        env["Onboarding__Environments__0__ConnectionProfiles__0__Id"] = "test-connection"
        env["Onboarding__Environments__0__ConnectionProfiles__0__DisplayName"] = "Test connection"
        env["Onboarding__Environments__0__QueryPlanProfiles__0__Id"] = "test-query-plan"
        env["Onboarding__Environments__0__QueryPlanProfiles__0__DisplayName"] = "Test query plan"
        cls.dll = os.environ.get("ONBOARDING_MCP_DLL") or str(Path(__file__).resolve().parent / "bin/Debug/net8.0/Onboarding.Mcp.dll")
        cls.env = env
        cls.process = subprocess.Popen(["dotnet", cls.dll], env=env,
                                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        cls.session = None
        try:
            for _ in range(100):
                if cls.process.poll() is not None:
                    raise RuntimeError(f"MCP server exited; check build/runtime and port {cls.port}.")
                try:
                    cls.rpc("initialize", {"protocolVersion": "2025-06-18", "capabilities": {},
                            "clientInfo": {"name": "smoke-test", "version": "1.0"}}, 1)
                    break
                except urllib.error.URLError:
                    time.sleep(0.1)
            else:
                raise RuntimeError("MCP server did not start.")
            cls.rpc("notifications/initialized", {}, None)
        except BaseException:
            cls.tearDownClass()
            raise

    @classmethod
    def tearDownClass(cls):
        cls.process.terminate()
        cls.process.wait(timeout=10)
        cls.tenant.shutdown()
        cls.tenant.server_close()
        cls.measureeval.shutdown()
        cls.measureeval.server_close()

    def setUp(self):
        Tenant.mode = "ok"
        MeasureEval.mode = "ok"

    @classmethod
    def rpc(cls, method, params, request_id=2):
        payload = {"jsonrpc": "2.0", "method": method, "params": params}
        if request_id is not None:
            payload["id"] = request_id
        headers = {"Content-Type": "application/json", "Accept": "application/json, text/event-stream",
                   "MCP-Protocol-Version": "2025-06-18"}
        if cls.session:
            headers["Mcp-Session-Id"] = cls.session
        request = urllib.request.Request(f"http://127.0.0.1:{cls.port}/mcp", json.dumps(payload).encode(), headers)
        with urllib.request.urlopen(request, timeout=10) as response:
            cls.session = response.headers.get("Mcp-Session-Id", cls.session)
            body = response.read().decode()
            if not body:
                return None
            if "text/event-stream" in response.headers.get("Content-Type", ""):
                body = next(line[5:].strip() for line in body.splitlines() if line.startswith("data:"))
            return json.loads(body)

    def call(self, arguments=None):
        result = self.rpc("tools/call", {"name": "get_onboarding_options", "arguments": arguments or {}})
        self.assertNotIn("error", result)
        return result["result"]

    def test_discovery(self):
        tools = self.rpc("tools/list", {})["result"]["tools"]
        self.assertEqual([t["name"] for t in tools], ["get_onboarding_options"])
        self.assertTrue(tools[0]["annotations"]["readOnlyHint"])

    def test_catalog_and_redaction(self):
        Tenant.mode = "ok"
        result = self.call({"environmentId": "LOCAL"})
        self.assertNotIn("must-never-reach-model", json.dumps(result))
        self.assertNotIn("obsolete-config-definition", json.dumps(result))
        data = result["structuredContent"]
        self.assertEqual(data["status"], "complete")
        environment = data["environments"][0]
        self.assertEqual(environment["vendors"]["options"][0]["name"], "Test vendor")
        self.assertEqual(environment["vendorVersions"]["options"][0]["version"], "Test version")
        self.assertEqual(environment["connectionProfiles"]["status"], "configured")
        self.assertEqual(environment["connectionProfiles"]["options"][0], {"id": "test-connection", "displayName": "Test connection"})
        self.assertEqual(environment["queryPlanProfiles"]["status"], "configured")
        self.assertEqual(environment["queryPlanProfiles"]["options"][0], {"id": "test-query-plan", "displayName": "Test query plan"})
        self.assertNotIn("measures", environment)
        self.assertEqual(environment["measureDefinitions"], {
            "status": "available", "source": "measureeval_api",
            "options": [{"id": "test-definition", "version": 7}]})

    def test_empty_is_available(self):
        Tenant.mode = "empty"
        environment = self.call()["structuredContent"]["environments"][0]
        self.assertEqual(environment["vendors"]["status"], "available")
        self.assertEqual(environment["vendors"]["options"], [])

    def test_failure_is_not_empty_success(self):
        for mode in ["unauthorized", "invalid", "disconnected"]:
            with self.subTest(mode=mode):
                Tenant.mode = mode
                data = self.call()["structuredContent"]
                self.assertEqual(data["status"], "partial")
                self.assertEqual(data["environments"][0]["vendors"]["status"], "unavailable")
                self.assertEqual(data["environments"][0]["measureDefinitions"]["status"], "available")

    def test_measureeval_empty_and_failures(self):
        MeasureEval.mode = "empty"
        data = self.call()["structuredContent"]
        self.assertEqual(data["status"], "complete")
        definitions = data["environments"][0]["measureDefinitions"]
        self.assertEqual(definitions["status"], "available")
        self.assertEqual(definitions["options"], [])
        for mode in ["unauthorized", "invalid", "disconnected"]:
            with self.subTest(mode=mode):
                MeasureEval.mode = mode
                data = self.call()["structuredContent"]
                self.assertEqual(data["status"], "partial")
                environment = data["environments"][0]
                self.assertEqual(environment["vendors"]["status"], "available")
                self.assertEqual(environment["measureDefinitions"]["status"], "unavailable")
                self.assertEqual(environment["measureDefinitions"]["source"], "measureeval_api")

    def test_unknown_environment(self):
        self.assertTrue(self.call({"environmentId": "does-not-exist"})["isError"])

    def test_json_encoded_profile_strings_fail_at_startup(self):
        for section in ["ConnectionProfiles", "QueryPlanProfiles"]:
            with self.subTest(section=section):
                env = dict(self.env)
                env[f"Onboarding__Environments__0__{section}__0"] = json.dumps({"Id": "incorrect", "DisplayName": "Incorrect"})
                result = subprocess.run(["dotnet", self.dll], env=env, capture_output=True, text=True, timeout=15)
                self.assertNotEqual(result.returncode, 0)
                self.assertIn("must be arrays of JSON objects", result.stdout + result.stderr)


if __name__ == "__main__":
    unittest.main(verbosity=2)
