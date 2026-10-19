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
    create_mode = "ok"
    facilities = {}
    posts = []

    def do_GET(self):
        if self.path.startswith("/api/Facility/"):
            if self.create_mode == "preflight_error":
                self.send_response(503)
                self.end_headers()
                return
            facility_id = self.path.rsplit("/", 1)[-1]
            if facility_id in self.facilities:
                self.send_json(self.facilities[facility_id], 200)
            else:
                self.send_response(404)
                self.end_headers()
            return
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

    def send_json(self, value, status):
        body = json.dumps(value).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_POST(self):
        if self.path != "/api/Facility":
            self.send_response(404)
            self.end_headers()
            return
        request = self.read_json()
        self.posts.append(request)
        if self.headers.get("Authorization") != "Bearer test-create-token":
            self.send_json({"detail": "auth failure must-never-reach-model"}, 401)
            return
        if self.create_mode == "reject":
            self.send_json({"detail": "validation must-never-reach-model"}, 400)
            return
        if self.create_mode == "server_error":
            self.send_json({"detail": "server secret must-never-reach-model"}, 500)
            return
        self.facilities[request["facilityId"]] = request
        if self.create_mode == "disconnect_after_create":
            self.close_connection = True
            return
        if self.create_mode == "malformed_success":
            self.send_json({"unexpected": "shape"}, 201)
            return
        self.send_json({**request, "vendor": {"authentication": {"secret": "must-never-reach-model"}}}, 201)

    def read_json(self):
        if self.headers.get("Transfer-Encoding") == "chunked":
            chunks = []
            while True:
                size = int(self.rfile.readline().split(b";", 1)[0], 16)
                if size == 0:
                    self.rfile.readline()
                    break
                chunks.append(self.rfile.read(size))
                self.rfile.read(2)
            body = b"".join(chunks)
        else:
            body = self.rfile.read(int(self.headers["Content-Length"]))
        return json.loads(body)

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


class Acquisition(Tenant):
    configuration = None
    writes = []
    read_status = None
    write_status = None
    validation_mode = "connected"
    validations = []

    def do_GET(self):
        if self.path.startswith("/api/data/connectionValidation/$validate?"):
            from urllib.parse import urlsplit, parse_qs
            self.validations.append((len(self.writes), parse_qs(urlsplit(self.path).query)["fhirServerUrl"][0]))
            if self.headers.get("Authorization") != "Bearer test-acquisition-token":
                self.send_json({}, 401)
            elif self.validation_mode == "disconnected":
                self.close_connection = True
            elif self.validation_mode == "http_error":
                self.send_json({"detail": "must-never-reach-model"}, 502)
            elif self.validation_mode == "missing":
                self.send_json({}, 404)
            elif self.validation_mode == "malformed":
                self.send_json({"unexpected": "must-never-reach-model"}, 200)
            else:
                self.send_json({"isConnected": self.validation_mode == "connected", "errorMessage": "must-never-reach-model"}, 200)
            return
        if self.path != "/api/data/test-facility/fhirQueryConfiguration":
            self.send_json({}, 404)
        elif self.read_status:
            self.send_json({"detail": "must-never-reach-model"}, self.read_status)
        elif self.configuration is None:
            self.send_json({"status": 404, "detail": "No FhirQueryConfiguration found for facilityId: test-facility"}, 404)
        else:
            self.send_json(self.configuration, 200)

    def do_POST(self):
        self.save(201)

    def do_PUT(self):
        self.save(202)

    def save(self, status):
        request = self.read_json()
        self.writes.append((self.command, self.path, request))
        if self.headers.get("Authorization") != "Bearer test-acquisition-token":
            self.send_json({}, 401)
            return
        if self.write_status:
            self.send_json({"detail": "must-never-reach-model"}, self.write_status)
            return
        self.send_json({**request, "id": "config-id"}, status)


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
        cls.acquisition = ThreadingHTTPServer(("127.0.0.1", 0), Acquisition)
        threading.Thread(target=cls.acquisition.serve_forever, daemon=True).start()
        env = dict(os.environ)
        env["Logging__LogLevel__Default"] = "Critical"
        env["Logging__LogLevel__Microsoft.Hosting.Lifetime"] = "Critical"
        env["Onboarding__TenantApiBaseUrl"] = (
            f"http://127.0.0.1:{cls.tenant.server_port}/api/")
        env["Onboarding__MeasureEvalApiBaseUrl"] = (
            f"http://127.0.0.1:{cls.measureeval.server_port}/api/measureeval/")
        env["Onboarding__DataAcquisitionApiBaseUrl"] = f"http://127.0.0.1:{cls.acquisition.server_port}/api/data/"
        env["Onboarding__DataAcquisitionBearerToken"] = "test-acquisition-token"
        env["Onboarding__BearerToken"] = "test-create-token"
        # A legacy manual catalog must never replace the live service response.
        env["Onboarding__Measures__0__Id"] = "obsolete-config-definition"
        env["Onboarding__ConnectionProfiles__0__Id"] = "test-connection"
        env["Onboarding__ConnectionProfiles__0__DisplayName"] = "Test connection"
        env["Onboarding__QueryPlanProfiles__0__Id"] = "test-query-plan"
        env["Onboarding__QueryPlanProfiles__0__DisplayName"] = "Test query plan"
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
        cls.acquisition.shutdown()
        cls.acquisition.server_close()
        cls.measureeval.shutdown()
        cls.measureeval.server_close()

    def setUp(self):
        Tenant.mode = "ok"
        Tenant.create_mode = "ok"
        Tenant.facilities = {}
        Acquisition.configuration = None
        Acquisition.writes = []
        Acquisition.read_status = None
        Acquisition.write_status = None
        Acquisition.validation_mode = "connected"
        Acquisition.validations = []
        Tenant.posts = []
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
        tools = {tool["name"]: tool for tool in tools}
        self.assertEqual(set(tools), {"get_onboarding_options", "create_facility", "save_data_acquisition_configuration", "validate_fhir_server_connection", "get_data_acquisition_configuration", "lookup_epic_fhir_endpoints"})
        self.assertTrue(tools["lookup_epic_fhir_endpoints"]["annotations"]["readOnlyHint"])
        self.assertTrue(tools["get_data_acquisition_configuration"]["annotations"]["readOnlyHint"])
        connection_tool = tools["validate_fhir_server_connection"]
        self.assertTrue(connection_tool["annotations"]["readOnlyHint"])
        self.assertTrue(connection_tool["annotations"]["idempotentHint"])
        self.assertFalse(connection_tool["annotations"]["destructiveHint"])
        self.assertEqual(connection_tool["inputSchema"]["required"], ["fhirServerBaseUrl"])
        self.assertTrue(tools["get_onboarding_options"]["annotations"]["readOnlyHint"])
        self.assertEqual(tools["get_onboarding_options"]["inputSchema"].get("properties", {}), {})
        self.assertFalse(tools["create_facility"]["annotations"]["readOnlyHint"])
        self.assertFalse(tools["create_facility"]["annotations"]["idempotentHint"])
        self.assertEqual(tools["create_facility"]["inputSchema"]["properties"]["vendorVersionId"]["type"], "string")

    def create(self, **overrides):
        arguments = {"facilityId": "test-facility", "facilityName": "Test Hospital", "timeZone": "America/New_York"}
        arguments.update(overrides)
        response = self.rpc("tools/call", {"name": "create_facility", "arguments": arguments})
        self.assertNotIn("error", response)
        return response["result"]

    def save_acquisition(self, **overrides):
        arguments = {"facilityId": "test-facility"}
        arguments.update(overrides)
        return self.rpc("tools/call", {"name": "save_data_acquisition_configuration", "arguments": arguments})["result"]

    def existing_facility(self):
        Tenant.facilities["test-facility"] = {"facilityId": "test-facility", "timeZone": "America/New_York"}

    def get_acquisition(self, facility_id="test-facility"):
        return self.rpc("tools/call", {"name": "get_data_acquisition_configuration",
            "arguments": {"facilityId": facility_id}})["result"]

    def test_get_acquisition_configuration(self):
        Acquisition.configuration = {"facilityId": "test-facility", "id": "private-id",
            "fhirServerBaseUrl": "https://ehr.example/fhir", "maxConcurrentRequests": 4,
            "maxRetries": 3, "minAcquisitionPullTime": "08:00:00", "maxAcquisitionPullTime": None,
            "authentication": {"password": "must-never-reach-model"}, "extra": "must-never-reach-model"}
        result = self.get_acquisition()
        self.assertEqual(result["structuredContent"]["status"], "available")
        self.assertEqual(result["structuredContent"]["configuration"], {
            "facilityId": "test-facility", "fhirServerBaseUrl": "https://ehr.example/fhir",
            "maxConcurrentRequests": 4, "maxRetries": 3, "minAcquisitionPullTime": "08:00:00"})
        self.assertNotIn("must-never-reach-model", json.dumps(result))
        self.assertNotIn("private-id", json.dumps(result))
        self.assertEqual(Acquisition.writes, [])
        self.assertEqual(Acquisition.validations, [])

    def test_get_acquisition_missing_and_failures(self):
        self.assertEqual(self.get_acquisition()["structuredContent"]["status"], "not_found")
        for status in [401, 403, 404, 500]:
            Acquisition.read_status = status
            result = self.get_acquisition()
            self.assertEqual(result["structuredContent"]["status"], "unavailable")
            self.assertNotIn("must-never-reach-model", json.dumps(result))
        self.assertEqual(Acquisition.writes, [])

    def test_get_acquisition_rejects_invalid_or_unsafe_data(self):
        self.assertTrue(self.get_acquisition("../bad")["isError"])
        for body in [{}, {"facilityId": "other"},
                     {"facilityId": "test-facility", "fhirServerBaseUrl": "https://user:must-never-reach-model@ehr/fhir"},
                     {"facilityId": "test-facility", "fhirServerBaseUrl": "https://ehr/fhir?secret=must-never-reach-model"}]:
            Acquisition.configuration = body
            result = self.get_acquisition()
            self.assertEqual(result["structuredContent"]["status"], "unavailable")
            self.assertNotIn("must-never-reach-model", json.dumps(result))

    def test_standalone_connection_validation(self):
        for mode, expected in [("connected", "connected"), ("not_fhir", "failed"),
                               ("disconnected", "unavailable"),
                               ("http_error", "failed"), ("missing", "unavailable"),
                               ("malformed", "unavailable")]:
            with self.subTest(mode=mode):
                Acquisition.validation_mode = mode
                Acquisition.validations = []
                result = self.rpc("tools/call", {"name": "validate_fhir_server_connection",
                    "arguments": {"fhirServerBaseUrl": " https://ehr.example/fhir/path/ "}})["result"]
                self.assertEqual(result["structuredContent"]["status"], expected)
                self.assertTrue(Acquisition.validations)
                # HttpClient may retry GET after a transport disconnect.
                self.assertTrue(all(probe == (0, "https://ehr.example/fhir/path/")
                                    for probe in Acquisition.validations))
                self.assertEqual(Acquisition.writes, [])
                self.assertEqual(Tenant.posts, [])
                self.assertNotIn("must-never-reach-model", json.dumps(result))
                self.assertNotIn("test-acquisition-token", json.dumps(result))
                self.assertNotIn("saved", json.dumps(result))

    def test_standalone_connection_rejects_invalid_urls(self):
        for url in ["", "relative/fhir", "ftp://ehr/fhir", "https://user:secret@ehr/fhir",
                    "https://ehr/fhir?secret=value", "https://ehr/fhir#fragment"]:
            with self.subTest(url=url):
                result = self.rpc("tools/call", {"name": "validate_fhir_server_connection",
                    "arguments": {"fhirServerBaseUrl": url}})["result"]
                self.assertTrue(result["isError"])
                self.assertEqual(Acquisition.validations, [])
                self.assertEqual(Acquisition.writes, [])

    def test_add_acquisition(self):
        self.existing_facility()
        result = self.save_acquisition(fhirServerBaseUrl="http://ehr-test:8080/fhir", maxRetries=3,
                                       minAcquisitionPullTime="08:00:00", maxAcquisitionPullTime="18:00:00")
        self.assertEqual(result["structuredContent"]["status"], "created")
        method, path, body = Acquisition.writes[0]
        self.assertEqual((method, path), ("POST", "/api/data/fhirQueryConfiguration"))
        self.assertEqual(body["timeZone"], "America/New_York")
        self.assertEqual(body["maxConcurrentRequests"], 1)
        self.assertEqual(body["maxRetries"], 3)
        self.assertEqual(body["minAcquisitionPullTime"], "08:00:00")
        self.assertEqual(result["structuredContent"]["connectionValidation"]["status"], "connected")
        self.assertEqual(Acquisition.validations, [(1, "http://ehr-test:8080/fhir")])

    def test_update_acquisition_preserves_private_settings(self):
        self.existing_facility()
        Acquisition.configuration = {"id": "config-id", "facilityId": "test-facility",
            "fhirServerBaseUrl": "https://ehr.example/fhir", "maxConcurrentRequests": 2, "maxRetries": 4,
            "authentication": {"authType": "Basic", "userName": "private", "password": "must-never-reach-model"},
            "minAcquisitionPullTime": "07:00:00", "maxAcquisitionPullTime": "21:00:00"}
        result = self.save_acquisition(maxConcurrentRequests=5, maxAcquisitionPullTime="clear")
        self.assertEqual(result["structuredContent"]["status"], "updated")
        method, _, body = Acquisition.writes[0]
        self.assertEqual(method, "PUT")
        self.assertEqual(Acquisition.validations, [])
        self.assertEqual(result["structuredContent"]["connectionValidation"]["status"], "not_requested")
        self.assertEqual(body["id"], "config-id")
        self.assertEqual(body["authentication"], Acquisition.configuration["authentication"])
        self.assertEqual(body["minAcquisitionPullTime"], "07:00:00")
        self.assertIsNone(body["maxAcquisitionPullTime"])
        self.assertEqual(body["maxRetries"], 4)
        self.assertEqual(body["maxConcurrentRequests"], 5)
        self.assertNotIn("must-never-reach-model", json.dumps(result))
        self.assertNotIn("test-acquisition-token", json.dumps(result))

    def test_updated_url_validates_without_retrying_save(self):
        self.existing_facility()
        Acquisition.configuration = {"id": "config-id", "facilityId": "test-facility",
                                     "fhirServerBaseUrl": "https://old.example/fhir"}
        for mode, expected in [("connected", "connected"), ("failed", "failed"), ("http_error", "failed"),
                               ("missing", "unavailable"), ("malformed", "unavailable"), ("disconnected", "unavailable")]:
            with self.subTest(mode=mode):
                Acquisition.writes = []
                Acquisition.validations = []
                Acquisition.validation_mode = mode
                result = self.save_acquisition(fhirServerBaseUrl="https://new.example/fhir/path/")
                self.assertEqual(result["structuredContent"]["status"], "updated")
                self.assertEqual(result["structuredContent"]["connectionValidation"]["status"], expected)
                self.assertTrue(Acquisition.validations)
                # HttpClient may retry an idempotent GET after a transport disconnect.
                # Every probe must follow exactly one successful configuration write.
                self.assertTrue(all(probe == (1, "https://new.example/fhir/path/") for probe in Acquisition.validations))
                self.assertEqual(len(Acquisition.writes), 1)
                self.assertNotIn("must-never-reach-model", json.dumps(result))

    def test_acquisition_invalid_input_and_missing_url(self):
        self.existing_facility()
        for arguments in [{}, {"facilityId": "../bad"}, {"fhirServerBaseUrl": "https://user:secret@ehr/fhir"},
                          {"maxConcurrentRequests": 0}, {"maxRetries": 11}, {"minAcquisitionPullTime": "25:00:00"}]:
            with self.subTest(arguments=arguments):
                self.assertTrue(self.save_acquisition(**arguments)["isError"])
        self.assertEqual(Acquisition.writes, [])

    def test_acquisition_failed_reads_never_write(self):
        self.assertEqual(self.save_acquisition()["structuredContent"]["status"], "unavailable")
        self.existing_facility()
        for code in [401, 404, 503]:
            Acquisition.read_status = code
            self.assertEqual(self.save_acquisition()["structuredContent"]["status"], "unavailable")
        Acquisition.read_status = None
        Acquisition.configuration = {"facilityId": "other", "id": "config-id"}
        self.assertEqual(self.save_acquisition()["structuredContent"]["status"], "unavailable")
        self.assertEqual(Acquisition.writes, [])

    def test_acquisition_write_failures_no_retry(self):
        self.existing_facility()
        for code, status in [(400, "rejected"), (401, "rejected"), (404, "rejected"), (500, "unknown"), (200, "unknown")]:
            with self.subTest(code=code):
                Acquisition.writes = []
                Acquisition.write_status = code
                result = self.save_acquisition(fhirServerBaseUrl="https://ehr.example/fhir")
                self.assertEqual(result["structuredContent"]["status"], status)
                self.assertEqual(len(Acquisition.writes), 1)
                self.assertEqual(Acquisition.validations, [])
                self.assertNotIn("must-never-reach-model", json.dumps(result))

    def test_create_and_duplicate(self):
        vendor_version = "22222222-2222-2222-2222-222222222222"
        result = self.create(vendorVersionId=vendor_version)
        self.assertNotIn("must-never-reach-model", json.dumps(result))
        self.assertNotIn("test-create-token", json.dumps(result))
        created = result["structuredContent"]
        self.assertEqual(created["status"], "created")
        self.assertEqual(created["httpStatus"], 201)
        self.assertEqual(created["facility"]["vendorVersionId"], vendor_version)
        self.assertEqual(Tenant.posts[0]["scheduledReports"], {"daily": [], "weekly": [], "monthly": []})
        self.assertEqual(self.create()["structuredContent"]["status"], "already_exists")
        self.assertEqual(len(Tenant.posts), 1)

    def test_create_without_vendor(self):
        result = self.create()["structuredContent"]
        self.assertEqual(result["status"], "created")
        self.assertIsNone(Tenant.posts[0]["vendorVersionId"])

    def test_invalid_creation_never_posts(self):
        for overrides in [{"facilityId": "../bad"}, {"facilityName": " "}, {"facilityName": "<script>"},
                          {"timeZone": "not-a-zone"}, {"timeZone": "Eastern Standard Time"},
                          {"vendorVersionId": "not-a-uuid"}]:
            with self.subTest(overrides=overrides):
                self.assertTrue(self.create(**overrides)["isError"])
        self.assertEqual(Tenant.posts, [])

    def test_preflight_failure_does_not_create(self):
        Tenant.create_mode = "preflight_error"
        self.assertEqual(self.create()["structuredContent"]["status"], "unavailable")
        self.assertEqual(Tenant.posts, [])

    def test_creation_failure_does_not_retry_or_leak(self):
        for mode, status in [("reject", "rejected"), ("server_error", "unknown"),
                             ("malformed_success", "unknown"), ("disconnect_after_create", "unknown")]:
            with self.subTest(mode=mode):
                Tenant.create_mode = mode
                Tenant.facilities = {}
                Tenant.posts = []
                result = self.create()
                self.assertEqual(result["structuredContent"]["status"], status)
                self.assertNotIn("must-never-reach-model", json.dumps(result))
                self.assertEqual(len(Tenant.posts), 1)

    def test_catalog_and_redaction(self):
        Tenant.mode = "ok"
        result = self.call()
        self.assertNotIn("must-never-reach-model", json.dumps(result))
        self.assertNotIn("obsolete-config-definition", json.dumps(result))
        data = result["structuredContent"]
        self.assertEqual(data["status"], "complete")
        environment = data
        self.assertNotIn("environments", data)
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
        environment = self.call()["structuredContent"]
        self.assertEqual(environment["vendors"]["status"], "available")
        self.assertEqual(environment["vendors"]["options"], [])

    def test_failure_is_not_empty_success(self):
        for mode in ["unauthorized", "invalid", "disconnected"]:
            with self.subTest(mode=mode):
                Tenant.mode = mode
                data = self.call()["structuredContent"]
                self.assertEqual(data["status"], "partial")
                self.assertEqual(data["vendors"]["status"], "unavailable")
                self.assertEqual(data["measureDefinitions"]["status"], "available")

    def test_measureeval_empty_and_failures(self):
        MeasureEval.mode = "empty"
        data = self.call()["structuredContent"]
        self.assertEqual(data["status"], "complete")
        definitions = data["measureDefinitions"]
        self.assertEqual(definitions["status"], "available")
        self.assertEqual(definitions["options"], [])
        for mode in ["unauthorized", "invalid", "disconnected"]:
            with self.subTest(mode=mode):
                MeasureEval.mode = mode
                data = self.call()["structuredContent"]
                self.assertEqual(data["status"], "partial")
                environment = data
                self.assertEqual(environment["vendors"]["status"], "available")
                self.assertEqual(environment["measureDefinitions"]["status"], "unavailable")
                self.assertEqual(environment["measureDefinitions"]["source"], "measureeval_api")


    def test_json_encoded_profile_strings_fail_at_startup(self):
        for section in ["ConnectionProfiles", "QueryPlanProfiles"]:
            with self.subTest(section=section):
                env = dict(self.env)
                env[f"Onboarding__{section}__0"] = json.dumps({"Id": "incorrect", "DisplayName": "Incorrect"})
                result = subprocess.run(["dotnet", self.dll], env=env, capture_output=True, text=True, timeout=15)
                self.assertNotEqual(result.returncode, 0)
                self.assertIn("must be arrays of JSON objects", result.stdout + result.stderr)


if __name__ == "__main__":
    unittest.main(verbosity=2)
