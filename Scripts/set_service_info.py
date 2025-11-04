import os
import sys
import json
import yaml

def update_java_config(yaml_path, commit, build=None, product_version=None, test_mode=False):
    with open(yaml_path, "r", encoding="utf-8") as f:
        try:
            data = yaml.safe_load(f) or {}
        except yaml.YAMLError as e:
            print(f"ERROR: Could not parse {yaml_path} as YAML: {str(e)}")
            sys.exit(1)

    if "service-information" not in data:
        data["service-information"] = {}

    changed = False
    if commit is not None:
        data["service-information"]["commit"] = commit
        changed = True
    if build is not None:
        data["service-information"]["build"] = build
        changed = True
    if product_version is not None:
        data["service-information"]["product-version"] = product_version
        changed = True

    if changed:
        if not test_mode:
            with open(yaml_path, "w", encoding="utf-8") as f:
                yaml.dump(data, f, sort_keys=False)
        print("\nUpdated YAML contents:")
        print(yaml.dump(data, sort_keys=False))
        if test_mode:
            print("(Test mode: File not actually updated)")
    else:
        print("No changes necessary")

    print(f"Updated {yaml_path} with service-information.commit = {commit}")

def update_dotnet_config(json_path, commit, build=None, product_version=None, test_mode=False):
    with open(json_path, "r", encoding="utf-8") as f:
        try:
            data = json.load(f)
        except json.JSONDecodeError as e:
            print(f"ERROR: Could not parse {json_path} as JSON: {str(e)}")
            sys.exit(1)

    if "ServiceInformation" not in data or not isinstance(data["ServiceInformation"], dict):
        data["ServiceInformation"] = {}

    changed = False
    if commit is not None:
        data["ServiceInformation"]["Commit"] = commit
        changed = True
    if build is not None:
        data["ServiceInformation"]["Build"] = build
        changed = True
    if product_version is not None:
        data["ServiceInformation"]["ProductVersion"] = product_version
        changed = True

    if changed:
        if not test_mode:
            with open(json_path, "w", encoding="utf-8") as f:
                json.dump(data, f, indent=2, ensure_ascii=False)
        print("\nUpdated JSON contents:")
        print(json.dumps(data, indent=2, ensure_ascii=False))
        if test_mode:
            print("(Test mode: File not actually updated)")
    else:
        print("No changes necessary")

    print(f"Updated {json_path} with ServiceInformation.Commit = {commit}")

def update_commit(directory, service_name, commit, product_version=None, build=None, test_mode=False):
    json_path = os.path.join(directory, service_name, "appsettings.json")
    yaml_path = os.path.join(directory, service_name, "src", "main", "resources", "application.yml")

    if os.path.exists(json_path):
        update_dotnet_config(json_path, commit, product_version, build, test_mode)
    elif os.path.exists(yaml_path):
        update_java_config(yaml_path, commit, product_version, build, test_mode)
    else:
        print(f"ERROR: Neither appsettings.json at {json_path} nor application.yml at {yaml_path} found")
        sys.exit(1)


def get_param(args, index, env_var):
    if len(args) > index:
        return None if args[index] == "None" else args[index]
    return os.environ.get(env_var)

if __name__ == "__main__":
    directory = get_param(sys.argv[1:], 0, 'BUILD_SOURCESDIRECTORY')
    service_name = get_param(sys.argv[1:], 1, 'SERVICENAME')
    commit = get_param(sys.argv[1:], 2, 'GIT_COMMIT')
    build = get_param(sys.argv[1:], 3, 'BUILD_NUMBER')
    product_version = get_param(sys.argv[1:], 4, 'PRODUCT_VERSION')
    test_mode = get_param(sys.argv[1:], 5, 'TEST_MODE') == 'true'

    if not all([directory, service_name, commit]):
        print("Error: Missing required parameters.")
        print("Provide either command line arguments:")
        print("  python update_commit.py <Directory> <ServiceName> <Commit> <Build> <ProductVersion> <TestMode?>")
        print("Or environment variables:")
        print("  BUILD_SOURCESDIRECTORY, SERVICENAME, GIT_COMMIT, BUILD_NUMBER, PRODUCT_VERSION, TEST_MODE")
        sys.exit(1)

    update_commit(directory, service_name, commit, build, product_version, test_mode)