import argparse
import io
import json
import os
import sys
import zipfile

import requests


def tenant_exists(admin_bff_url, tenant_name, admin_bff_token=None):
    # 1. GET {admin-bff}/api/Facility/{tenant-name}
    check_url = f"{admin_bff_url}/api/Facility/{tenant_name}"
    print(f"Checking if tenant '{tenant_name}' exists at {check_url}...")

    headers = {}
    if admin_bff_token:
        headers['Authorization'] = f'Bearer {admin_bff_token}'

    try:
        response = requests.get(check_url, headers=headers)
        if response.status_code == 200:
            print(f"Tenant '{tenant_name}' already exists. Skipping creation.")
            return True
        else:
            return False
    except requests.exceptions.RequestException as e:
        print(f"Error checking tenant: {e}")
        # If the service is down, we probably can't proceed.
        sys.exit(1)


def create_tenant(tenant_name, admin_bff_url, admin_bff_token=None):
    print("Tenant does not exist. Creating new tenant...")

    # 2. POST {admin-bff}/api/Facility
    seed_data_path = os.path.join(os.path.dirname(__file__), 'seed_data', 'tenant.json')
    if not os.path.exists(seed_data_path):
        print(f"Error: Seed data file not found at {seed_data_path}")
        sys.exit(1)

    with open(seed_data_path, 'r') as f:
        tenant_json_content = f.read()

    # Replace {{tenant-name}} with the arg value
    payload_str = tenant_json_content.replace('{{tenant-name}}', tenant_name)
    payload = json.loads(payload_str)

    post_url = f"{admin_bff_url}/api/Facility"
    print(f"Creating tenant '{tenant_name}' at {post_url}...")

    headers = {}
    if admin_bff_token:
        headers['Authorization'] = f'Bearer {admin_bff_token}'

    try:
        response = requests.post(post_url, json=payload, headers=headers)
        if response.status_code in [200, 201]:
            print(f"Successfully created tenant '{tenant_name}'.")
        else:
            print(f"Failed to create tenant. Status code: {response.status_code}")
            print(response.text)
            sys.exit(1)
    except requests.exceptions.RequestException as e:
        print(f"Error creating tenant: {e}")
        sys.exit(1)
        
def init_artifacts(admin_bff_url, admin_bff_token=None):
    print("Initializing validation artifacts...")

    init_url = f"{admin_bff_url}/api/validation/artifact/$initialize"
    print(f"Calling POST {init_url}...")

    headers = {}
    if admin_bff_token:
        headers['Authorization'] = f'Bearer {admin_bff_token}'

    try:
        response = requests.post(init_url, headers=headers)
        if response.status_code in [200, 201, 204]:
            print("Successfully initialized validation artifacts.")
        else:
            print(f"Failed to initialize validation artifacts. Status code: {response.status_code}")
            print(response.text)
            sys.exit(1)
    except requests.exceptions.RequestException as e:
        print(f"Error initializing validation artifacts: {e}")
        sys.exit(1)

def init_categories(admin_bff_url, admin_bff_token=None):
    print("Initializing validation categories...")

    init_url = f"{admin_bff_url}/api/validation/category/$initialize"
    print(f"Calling POST {init_url}...")

    headers = {}
    if admin_bff_token:
        headers['Authorization'] = f'Bearer {admin_bff_token}'

    try:
        response = requests.post(init_url, headers=headers)
        if response.status_code in [200, 201, 204]:
            print("Successfully initialized validation categories.")
        else:
            print(f"Failed to initialize validation categories. Status code: {response.status_code}")
            print(response.text)
            sys.exit(1)
    except requests.exceptions.RequestException as e:
        print(f"Error initializing validation categories: {e}")
        sys.exit(1)


def clean_norm_ops(admin_bff_url, tenant_name, admin_bff_token=None):
    print("Cleaning normalization operations...")

    headers = {}
    if admin_bff_token:
        headers['Authorization'] = f'Bearer {admin_bff_token}'

    # Get all normalization operations for the tenant
    get_url = f"{admin_bff_url}/api/normalization/operations/facility/{tenant_name}"
    print(f"Fetching normalization operations from {get_url}...")

    try:
        response = requests.get(get_url, headers=headers)
        if response.status_code == 200:
            data = response.json()
            records = data.get('records', [])
            print(f"Found {len(records)} normalization operation(s) to delete.")

            # Delete each operation
            for idx, record in enumerate(records):
                operation_id = record.get('id')
                if not operation_id:
                    print(f"Warning: Record {idx + 1} does not have an 'id' field. Skipping.")
                    continue

                delete_url = f"{admin_bff_url}/api/normalization/operations/facility/{tenant_name}?operationId={operation_id}"
                print(f"Deleting normalization operation {idx + 1}/{len(records)} (ID: {operation_id})...")

                try:
                    delete_response = requests.delete(delete_url, headers=headers)
                    if delete_response.status_code in [200, 204]:
                        print(f"Successfully deleted normalization operation {idx + 1}.")
                    else:
                        print(
                            f"Failed to delete normalization operation {idx + 1}. Status code: {delete_response.status_code}")
                        print(delete_response.text)
                except requests.exceptions.RequestException as e:
                    print(f"Error deleting normalization operation {idx + 1}: {e}")

            print("Finished cleaning all normalization operations.")
        elif response.status_code == 404:
            print(f"No normalization operations found for tenant '{tenant_name}'.")
        else:
            print(f"Failed to fetch normalization operations. Status code: {response.status_code}")
            print(response.text)
    except requests.exceptions.RequestException as e:
        print(f"Error fetching normalization operations: {e}")


def create_norm_ops(admin_bff_url, tenant_name, admin_bff_token=None):
    print("Creating normalization operations...")

    # Load normalization operations from seed data
    seed_data_path = os.path.join(os.path.dirname(__file__), 'seed_data', 'normalization_ops.json')
    if not os.path.exists(seed_data_path):
        print(f"Error: Seed data file not found at {seed_data_path}")
        sys.exit(1)

    with open(seed_data_path, 'r') as f:
        ops_json_content = f.read()

    # Replace {{tenant-name}} with the arg value
    payload_str = ops_json_content.replace('{{tenant-name}}', tenant_name)

    try:
        operations = json.loads(payload_str)
    except json.JSONDecodeError as e:
        print(f"Error parsing normalization_ops.json: {e}")
        print(payload_str)
        sys.exit(1)

    if not isinstance(operations, list):
        print("Error: normalization_ops.json must contain an array at the top level")
        sys.exit(1)

    headers = {}
    if admin_bff_token:
        headers['Authorization'] = f'Bearer {admin_bff_token}'

    post_url = f"{admin_bff_url}/api/normalization/operations"
    print(f"Found {len(operations)} normalization operation(s) to create.")

    for idx, operation in enumerate(operations):
        print(f"Creating normalization operation {idx + 1}/{len(operations)}...")

        try:
            response = requests.post(post_url, json=operation, headers=headers)
            if response.status_code in [200, 201]:
                print(f"Successfully created normalization operation {idx + 1}.")
            else:
                print(f"Failed to create normalization operation {idx + 1}. Status code: {response.status_code}")
                print(response.text)
                sys.exit(1)
        except requests.exceptions.RequestException as e:
            print(f"Error creating normalization operation {idx + 1}: {e}")
            sys.exit(1)

    print("Finished creating all normalization operations.")


def create_query_plan(admin_bff_url, tenant_name, admin_bff_token=None):
    print("Creating query plan...")

    # Load query plan from seed data
    seed_data_path = os.path.join(os.path.dirname(__file__), 'seed_data', 'query_plan.json')
    if not os.path.exists(seed_data_path):
        print(f"Error: Seed data file not found at {seed_data_path}")
        sys.exit(1)

    with open(seed_data_path, 'r') as f:
        query_plan_content = f.read()

    # Replace {{tenant-name}} with the arg value
    payload_str = query_plan_content.replace('{{tenant-name}}', tenant_name)

    try:
        payload = json.loads(payload_str)
    except json.JSONDecodeError as e:
        print(f"Error parsing query_plan.json: {e}")
        print(payload_str)
        sys.exit(1)

    headers = {}
    if admin_bff_token:
        headers['Authorization'] = f'Bearer {admin_bff_token}'

    post_url = f"{admin_bff_url}/api/data/{tenant_name}/QueryPlan"
    print(f"Creating query plan for tenant '{tenant_name}' at {post_url}...")

    try:
        response = requests.post(post_url, json=payload, headers=headers)
        if response.status_code in [200, 201]:
            print(f"Successfully created query plan for tenant '{tenant_name}'.")
        elif response.status_code == 409:
            print(f"Query plan already exists for tenant '{tenant_name}'. Skipping creation.")
        else:
            print(f"Failed to create query plan. Status code: {response.status_code}")
            print(response.text)
            sys.exit(1)
    except requests.exceptions.RequestException as e:
        print(f"Error creating query plan: {e}")
        sys.exit(1)


def create_query_config(admin_bff_url, tenant_name, fhir_server_base, admin_bff_token=None):
    print("Creating query config...")

    # Load query config from seed data
    seed_data_path = os.path.join(os.path.dirname(__file__), 'seed_data', 'query_config.json')
    if not os.path.exists(seed_data_path):
        print(f"Error: Seed data file not found at {seed_data_path}")
        sys.exit(1)

    with open(seed_data_path, 'r') as f:
        query_config_content = f.read()

    # Replace {{tenant-name}} and {{fhir-server-base}} with the arg values
    payload_str = query_config_content.replace('{{tenant-name}}', tenant_name)
    payload_str = payload_str.replace('{{fhir-server-base}}', fhir_server_base)

    try:
        payload = json.loads(payload_str)
    except json.JSONDecodeError as e:
        print(f"Error parsing query_config.json: {e}")
        print(payload_str)
        sys.exit(1)

    headers = {}
    if admin_bff_token:
        headers['Authorization'] = f'Bearer {admin_bff_token}'

    post_url = f"{admin_bff_url}/api/data/fhirQueryConfiguration"
    print(f"Creating query config at {post_url}...")

    try:
        response = requests.post(post_url, json=payload, headers=headers)
        if response.status_code in [200, 201]:
            print(f"Successfully created query config.")
        elif response.status_code == 409:
            print(f"Query config already exists. Skipping creation.")
        else:
            print(f"Failed to create query config. Status code: {response.status_code}")
            print(response.text)
            sys.exit(1)
    except requests.exceptions.RequestException as e:
        print(f"Error creating query config: {e}")
        sys.exit(1)


def load_measure(admin_bff_url, github_repo, github_version, github_pat, admin_bff_token=None):
    print(f"Loading measure definitions from GitHub repository {github_repo} version {github_version}...")

    # Parse owner and repo from github_repo
    try:
        owner, repo = github_repo.split('/')
    except ValueError:
        print(f"Error: Invalid repository format '{github_repo}'. Expected format: owner/repo")
        sys.exit(1)

    # Set up headers for GitHub API
    github_headers = {
        'Accept': 'application/vnd.github+json',
        'X-GitHub-Api-Version': '2022-11-28'
    }
    if github_pat:
        print("Using personal access token for GitHub API authentication...")
        github_headers['Authorization'] = f'Bearer {github_pat}'

    try:
        # Get release information by tag
        release_url = f"https://api.github.com/repos/{owner}/{repo}/releases/tags/{github_version}"
        print(f"Fetching release information from {release_url}...")
        release_response = requests.get(release_url, headers=github_headers)

        if release_response.status_code != 200:
            print(f"Failed to fetch release information. Status code: {release_response.status_code}")
            print(release_response.text)
            sys.exit(1)

        release_data = release_response.json()

        # Find the measure-definitions.zip asset
        assets = release_data.get('assets', [])
        asset_id = None

        for asset in assets:
            if asset.get('name') == 'measure-definitions.zip':
                asset_id = asset.get('id')
                break

        if not asset_id:
            print("Error: measure-definitions.zip asset not found in the release.")
            sys.exit(1)

        print(f"Found measure-definitions.zip asset with ID {asset_id}")

        # Download the asset
        asset_url = f"https://api.github.com/repos/{owner}/{repo}/releases/assets/{asset_id}"
        download_headers = github_headers.copy()
        download_headers['Accept'] = 'application/octet-stream'

        print(f"Downloading measure definition ZIP from {asset_url}...")
        response = requests.get(asset_url, headers=download_headers)

        if response.status_code != 200:
            print(f"Failed to download measure definition. Status code: {response.status_code}")
            print(response.text)
            sys.exit(1)

        print("Successfully downloaded measure definition ZIP.")

        # Open the ZIP file from memory
        with zipfile.ZipFile(io.BytesIO(response.content)) as zip_file:
            # List all files in the ZIP
            all_files = zip_file.namelist()

            # Find all bundle JSON files in the bundles directory (only bundles/XXX/XXX-bundle.json, not nested deeper)
            bundle_files = [f for f in all_files if
                            (f.startswith('bundles/measure/') and f.endswith('-bundle.json') and f.count('/') == 3) or (f.endswith('-bundle.json') and f.count('/') == 0)]

            if not bundle_files:
                # Debug: Print all files in the ZIP
                print(f"All files in ZIP ({len(all_files)} total):")
                for file in all_files:
                    print(f"  {file}")
                    
                print("No bundle JSON files found in the bundles directory.")
                return

            print(f"Found {len(bundle_files)} bundle file(s) to load.")

            # POST each bundle JSON to the Admin BFF
            admin_bff_headers = {}
            if admin_bff_token:
                admin_bff_headers['Authorization'] = f'Bearer {admin_bff_token}'

            put_url = f"{admin_bff_url}/api/measureeval/measure-definition"

            for bundle_file in bundle_files:
                print(f"Loading bundle: {bundle_file}...")

                # Read the bundle JSON content
                with zip_file.open(bundle_file) as f:
                    bundle_content = f.read()
                    bundle_content_str = bundle_content.decode('utf-8', errors='ignore')
                    bundle_json = json.loads(bundle_content_str)

                # POST the bundle
                try:
                    bundle_response = requests.put(put_url, json=bundle_json, headers=admin_bff_headers)
                    if bundle_response.status_code in [200, 201, 204]:
                        print(f"Successfully loaded bundle: {bundle_file}")
                    else:
                        print(f"Failed to load bundle: {bundle_file}. Status code: {bundle_response.status_code}")
                        print(bundle_response.text)
                except requests.exceptions.RequestException as e:
                    print(f"Error loading bundle {bundle_file}: {e}")

            print("Finished loading all measure definitions.")

    except requests.exceptions.RequestException as e:
        print(f"Error downloading measure definition: {e}")
        sys.exit(1)
    except zipfile.BadZipFile:
        print("Downloaded file is not a valid ZIP file.")
        sys.exit(1)

def main():
    parser = argparse.ArgumentParser(description='Seed HAPI tenant into Admin BFF.')
    parser.add_argument('--admin-bff-base-url', default=os.environ.get('ADMIN_BFF_BASE_URL', 'http://localhost:8063'),
                        help='Base URL for Admin BFF (default: http://localhost:8063 or ADMIN_BFF_BASE_URL env var)')
    parser.add_argument('--fhir-server-base', default=os.environ.get('SEED_FHIR_SERVER_BASE'),
                        help='FHIR server base URL (default: SEED_FHIR_SERVER_BASE env var)')
    parser.add_argument('--tenant-name', default=os.environ.get('SEED_TENANT_NAME', 'ehr-test'),
                        help='Tenant name (default: ehr-test or SEED_TENANT_NAME env var)')
    parser.add_argument('--skip-init-artifacts', action='store_true',
                        help='Skip initializing validation artifacts (default: false')
    parser.add_argument('--skip-create-tenant', action='store_true',
                        help='Skip creating tenant (default: false)')
    parser.add_argument('--skip-init-categories', action='store_true',
                        help='Skip initializing validation categories (default: false')
    parser.add_argument('--clean-norm-ops', action='store_true', help='Clean normalization operations before seeding (default: false)', default=True)
    parser.add_argument('--skip-norm-ops', action='store_true', help='Skip creating normalization operations (default: false)')
    parser.add_argument('--skip-query-plan', action='store_true', help='Skip creating query plan (default: false)')
    parser.add_argument('--skip-query-config', action='store_true', help='Skip creating query config (default: false)')
    parser.add_argument('--skip-measure-load', action='store_true',
                        help='Skip loading measure definitions from GitHub (default: false)')
    parser.add_argument('--github-measure-repo', default=os.environ.get('GITHUB_MEASURE_REPO'),
                        help='GitHub repository in format owner/repo (e.g., account/repo) (default: GITHUB_MEASURE_REPO env var)')
    parser.add_argument('--github-measure-version', default=os.environ.get('GITHUB_MEASURE_VERSION'),
                        help='GitHub release version/tag (e.g., v1.0.0) (default: GITHUB_MEASURE_VERSION env var)')
    parser.add_argument('--admin-bff-token', help='Admin BFF authentication token (optional).')
    parser.add_argument('--github-pat', default=os.environ.get('GITHUB_PAT'),
                        help='GitHub personal access token for downloading measure definition (default: GITHUB_PAT env var)')

    args = parser.parse_args()

    if not args.fhir_server_base or len(args.fhir_server_base.strip()) == 0:
        raise ValueError("FHIR server base URL cannot be empty or whitespace only")

    if not args.tenant_name or len(args.tenant_name.strip()) == 0:
        raise ValueError("Tenant name cannot be empty or whitespace only")
    
    print("Parameters for seeding:\n----------------------")
    print(f"Admin BFF base URL: {args.admin_bff_base_url}")
    print(f"Admin BFF token: {'***' if args.admin_bff_token else 'Not provided'}")
    print(f"FHIR server base URL: {args.fhir_server_base}")
    print(f"Tenant name: {args.tenant_name}")
    print(f"GitHub measure repo: {args.github_measure_repo}")
    print(f"GitHub measure version: {args.github_measure_version}")
    print(f"GitHub PAT: {'***' if args.github_pat else 'Not provided'}")
    print(f"Skip measure load: {args.skip_measure_load}")
    print("----------------------\n")

    admin_bff_url = args.admin_bff_base_url.rstrip('/')
    tenant_name = args.tenant_name
    admin_bff_token = args.admin_bff_token

    if not args.skip_init_artifacts:
        init_artifacts(admin_bff_url, admin_bff_token)

    if not args.skip_init_categories:
        init_categories(admin_bff_url, admin_bff_token)

    if not args.skip_measure_load and args.github_measure_repo and args.github_measure_version:
        load_measure(admin_bff_url, args.github_measure_repo, args.github_measure_version, args.github_pat,
                     admin_bff_token)

    if not args.skip_create_tenant:
        if not tenant_exists(admin_bff_url, tenant_name, admin_bff_token):
            create_tenant(tenant_name, admin_bff_url, admin_bff_token)

    if args.clean_norm_ops:
        clean_norm_ops(admin_bff_url, tenant_name, admin_bff_token)

    if not args.skip_norm_ops:
        create_norm_ops(admin_bff_url, tenant_name, admin_bff_token)

    if not args.skip_query_plan:
        create_query_plan(admin_bff_url, tenant_name, admin_bff_token)

    if not args.skip_query_config:
        create_query_config(admin_bff_url, tenant_name, args.fhir_server_base, admin_bff_token)


if __name__ == '__main__':
    main()
