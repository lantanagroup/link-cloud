"""Check a running published chat's startup assets, not just its HTTP health endpoint."""
import argparse
from html.parser import HTMLParser
from urllib.parse import urljoin
from urllib.request import urlopen


class StartupAssets(HTMLParser):
    def __init__(self):
        super().__init__()
        self.assets = []

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == "script" and attrs.get("src"):
            self.assets.append((attrs["src"], "javascript"))
        elif tag == "link" and attrs.get("rel") == "stylesheet":
            self.assets.append((attrs["href"], "css"))


def verify(base_url):
    with urlopen(base_url, timeout=15) as response:
        page = response.read().decode()
    parser = StartupAssets()
    parser.feed(page)
    assert any("_framework/blazor.web" in path for path, _ in parser.assets), "Missing Blazor startup script reference"
    for path, kind in parser.assets:
        with urlopen(urljoin(base_url, path), timeout=15) as response:
            content_type = response.headers.get("Content-Type", "")
            assert kind in content_type, f"Unexpected asset content type: {path}: {content_type}"
            assert response.read(), f"Empty asset: {path}"
        print(f"OK: {path}")
    print("Published chat startup assets passed.")


if __name__ == "__main__":
    arguments = argparse.ArgumentParser()
    arguments.add_argument("--url", default="http://127.0.0.1:8091/")
    verify(arguments.parse_args().url)
