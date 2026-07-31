import argparse
import json
import sys
from typing import Dict, List, Tuple, Any


def load_config(file_path: str) -> Dict:
    """Load Azure App Config JSON export from file."""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            return json.load(f)
    except FileNotFoundError:
        print(f"Error: File not found: {file_path}")
        sys.exit(1)
    except json.JSONDecodeError as e:
        print(f"Error: Invalid JSON in file {file_path}: {e}")
        sys.exit(1)


def extract_config_items(config: Dict) -> Dict[Tuple[str, str], Dict]:
    """Extract key-value pairs from Azure App Config format."""
    items = {}
    for item in config.get('items', []):
        key = item.get('key')
        if key:
            label = item.get('label') or ''
            composite_key = (key, label)
            items[composite_key] = {
                'value': item.get('value'),
                'content_type': item.get('content_type'),
                'label': item.get('label'),
                'tags': item.get('tags')
            }
    return items


def is_key_vault_reference(value: str) -> bool:
    """Check if value is a JSON object with only 'uri' field (Key Vault reference)."""
    try:
        obj = json.loads(value)
        return isinstance(obj, dict) and set(obj.keys()) == {'uri'}
    except (json.JSONDecodeError, TypeError):
        return False


def extract_key_vault_uri(value: str) -> str:
    """Extract URI from Key Vault reference JSON."""
    try:
        obj = json.loads(value)
        return obj.get('uri', '')
    except (json.JSONDecodeError, TypeError):
        return ''


def diff_tags(left: Any, right: Any) -> List[str]:
    """Describe how two tag dictionaries differ.

    Tags carry no runtime meaning - neither provider selects on them - so they are safe to
    write, but that also means a tagging pass is invisible unless something compares them.
    """
    left = left or {}
    right = right or {}
    if not isinstance(left, dict) or not isinstance(right, dict):
        return []
    changes = []
    for name in sorted(set(left) | set(right)):
        if name not in left:
            changes.append(f"+ {name}={right[name]!r}")
        elif name not in right:
            changes.append(f"- {name}={left[name]!r}")
        elif left[name] != right[name]:
            changes.append(f"~ {name}: {left[name]!r} --> {right[name]!r}")
    return changes


def normalize_value(value: Any) -> Any:
    """Normalize values for comparison - convert booleans to strings to match Azure App Config behavior."""
    if isinstance(value, bool):
        return "true" if value else "false"
    elif isinstance(value, dict):
        return {k: normalize_value(v) for k, v in value.items()}
    elif isinstance(value, list):
        return [normalize_value(item) for item in value]
    return value


def deep_compare_feature_flag(left_value: str, right_value: str) -> Tuple[bool, List[str]]:
    """Deep compare feature flag JSON objects and return differences."""
    try:
        left_obj = json.loads(left_value)
        right_obj = json.loads(right_value)

        # Normalize both objects to handle boolean vs string comparisons
        left_obj = normalize_value(left_obj)
        right_obj = normalize_value(right_obj)

        differences = []

        def compare_objects(obj1, obj2, path=""):
            """Recursively compare two objects."""
            if type(obj1) != type(obj2):
                differences.append(f"{path}: Type mismatch - {type(obj1).__name__} --> {type(obj2).__name__}")
                return

            if isinstance(obj1, dict):
                all_keys = set(obj1.keys()) | set(obj2.keys())
                for key in sorted(all_keys):
                    new_path = f"{path}.{key}" if path else key
                    if key not in obj1:
                        differences.append(f"{new_path}: Missing in left (right value: {json.dumps(obj2[key])})")
                    elif key not in obj2:
                        differences.append(f"{new_path}: Missing in right (left value: {json.dumps(obj1[key])})")
                    else:
                        compare_objects(obj1[key], obj2[key], new_path)
            elif isinstance(obj1, list):
                if len(obj1) != len(obj2):
                    differences.append(f"{path}: List length differs - {len(obj1)} --> {len(obj2)}")
                for i in range(min(len(obj1), len(obj2))):
                    compare_objects(obj1[i], obj2[i], f"{path}[{i}]")
            else:
                if obj1 != obj2:
                    differences.append(f"{path}: {json.dumps(obj1)} --> {json.dumps(obj2)}")

        compare_objects(left_obj, right_obj)
        return len(differences) == 0, differences
    except json.JSONDecodeError:
        return False, ["Error parsing feature flag JSON"]


def compare_configs(left_items: Dict, right_items: Dict) -> Dict[str, List]:
    """Compare two configurations and categorize differences."""
    results = {
        'newly_created': [],
        'deleted': [],
        'changed': [],
        'tags_changed': [],
        'unchanged': []
    }

    left_keys = set(left_items.keys())
    right_keys = set(right_items.keys())

    # Newly created keys (in left, not in right)
    results['newly_created'] = sorted(left_keys - right_keys)

    # Deleted keys (in right, not in left)
    results['deleted'] = sorted(right_keys - left_keys)

    # Compare common keys
    common_keys = sorted(left_keys & right_keys)
    for composite_key in common_keys:
        key, label = composite_key
        left_item = left_items[composite_key]
        right_item = right_items[composite_key]

        left_value = left_item['value']
        right_value = right_item['value']
        left_content_type = left_item.get('content_type', '')

        # Tags were captured but never compared, so a run that only changed tags looked
        # identical. Verifying a tagging pass needs this: the expected result is tag changes
        # and nothing else.
        tag_changes = diff_tags(left_item.get('tags'), right_item.get('tags'))

        # Check if it's a feature flag
        is_feature_flag = ('application/vnd.microsoft.appconfig.ff+json;charset=utf-8' in left_content_type or
                           'application/json' in left_content_type)

        if is_feature_flag:
            is_same, differences = deep_compare_feature_flag(left_value, right_value)
            if is_same and not tag_changes:
                results['unchanged'].append(composite_key)
            elif is_same:
                results['tags_changed'].append({
                    'key': key, 'label': label, 'tag_changes': tag_changes})
            else:
                results['changed'].append({
                    'key': key,
                    'label': label,
                    'differences': differences,
                    'left_value': left_value,
                    'right_value': right_value
                })
        else:
            # Normalize values for comparison (handles boolean vs string)
            normalized_left = left_value
            normalized_right = right_value

            # Try to parse as JSON and normalize if successful
            try:
                normalized_left = json.dumps(normalize_value(json.loads(left_value)), sort_keys=True)
                normalized_right = json.dumps(normalize_value(json.loads(right_value)), sort_keys=True)
            except (json.JSONDecodeError, TypeError):
                # Not JSON, compare as strings
                pass

            if normalized_left == normalized_right and not tag_changes:
                results['unchanged'].append(composite_key)
            elif normalized_left == normalized_right:
                results['tags_changed'].append({
                    'key': key, 'label': label, 'tag_changes': tag_changes})
            else:
                # Check if both values are Key Vault references
                if is_key_vault_reference(left_value) and is_key_vault_reference(right_value):
                    left_uri = extract_key_vault_uri(left_value)
                    right_uri = extract_key_vault_uri(right_value)
                    results['changed'].append({
                        'key': key,
                        'label': label,
                        'key_vault_comparison': f"{left_uri} --> {right_uri}"
                    })
                else:
                    results['changed'].append({
                        'key': key,
                        'label': label,
                        'left_value': left_value,
                        'right_value': right_value
                    })

    return results


def print_results(results: Dict[str, List]):
    """Print comparison results in a readable format."""
    print("\n" + "=" * 80)
    print("AZURE APP CONFIG COMPARISON RESULTS")
    print("=" * 80)

    # Newly created keys
    if results['newly_created']:
        print(f"\n🆕 NEWLY CREATED KEYS (in left, not in right): {len(results['newly_created'])}")
        print("-" * 80)
        for composite_key in results['newly_created']:
            key, label = composite_key
            label_str = f" [Label: {label}]" if label else " [No Label]"
            print(f"  + {key}{label_str}")
    else:
        print(f"\n🆕 NEWLY CREATED KEYS: 0")

    # Deleted keys
    if results['deleted']:
        print(f"\n🗑️  DELETED KEYS (in right, not in left): {len(results['deleted'])}")
        print("-" * 80)
        for composite_key in results['deleted']:
            key, label = composite_key
            label_str = f" [Label: {label}]" if label else " [No Label]"
            print(f"  - {key}{label_str}")
    else:
        print(f"\n🗑️  DELETED KEYS: 0")

    # Changed keys
    if results['changed']:
        print(f"\n🔄 CHANGED KEYS: {len(results['changed'])}")
        print("-" * 80)
        for item in results['changed']:
            label = item.get('label', '')
            label_str = f" [Label: {label}]" if label else " [No Label]"
            print(f"\n  Key: {item['key']}{label_str}")
            if 'differences' in item:
                # Feature flag differences
                print("  Feature Flag Differences:")
                for diff in item['differences']:
                    print(f"    • {diff}")
            elif 'key_vault_comparison' in item:
                # Key Vault reference comparison
                print(f"  {item['key_vault_comparison']}")
            else:
                # Regular value differences
                print(f"  {item['left_value']} --> {item['right_value']}")
    else:
        print(f"\n🔄 CHANGED KEYS: 0")

    # Tag-only changes
    if results['tags_changed']:
        print(f"\nTAG CHANGES ONLY: {len(results['tags_changed'])}")
        print("-" * 80)
        for item in results['tags_changed']:
            label_str = f" [Label: {item['label']}]" if item['label'] else " [No Label]"
            print(f"\n  Key: {item['key']}{label_str}")
            for change in item['tag_changes']:
                print(f"    {change}")
    else:
        print(f"\nTAG CHANGES ONLY: 0")

    # Unchanged keys
    if results['unchanged']:
        print(f"\n✅ UNCHANGED KEYS: {len(results['unchanged'])}")
        print("-" * 80)
        for composite_key in results['unchanged']:
            key, label = composite_key
            label_str = f" [Label: {label}]" if label else " [No Label]"
            print(f"  = {key}{label_str}")
    else:
        print(f"\n✅ UNCHANGED KEYS: 0")

    print("\n" + "=" * 80)
    print("SUMMARY")
    print("=" * 80)
    print(f"  Newly Created: {len(results['newly_created'])}")
    print(f"  Deleted:       {len(results['deleted'])}")
    print(f"  Changed:       {len(results['changed'])}")
    print(f"  Tags only:     {len(results['tags_changed'])}")
    print(f"  Unchanged:     {len(results['unchanged'])}")
    print("=" * 80 + "\n")


def main():
    """Main function to compare Azure App Config exports."""
    # The report uses emoji section markers, which a Windows console running cp1252 cannot
    # encode - the script died with UnicodeEncodeError before printing any results.
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    parser = argparse.ArgumentParser(
        description='Compare two Azure App Config JSON exports and identify differences.'
    )
    parser.add_argument(
        'compare_left',
        type=str,
        help='Path to the left (new/current) Azure App Config JSON export file'
    )
    parser.add_argument(
        'compare_right',
        type=str,
        help='Path to the right (old/baseline) Azure App Config JSON export file'
    )

    args = parser.parse_args()

    # Load configurations
    print(f"Loading left config from: {args.compare_left}")
    left_config = load_config(args.compare_left)

    print(f"Loading right config from: {args.compare_right}")
    right_config = load_config(args.compare_right)

    # Extract items
    left_items = extract_config_items(left_config)
    right_items = extract_config_items(right_config)

    print(f"\nLeft config contains {len(left_items)} items")
    print(f"Right config contains {len(right_items)} items")

    # Compare configurations
    results = compare_configs(left_items, right_items)

    # Print results
    print_results(results)


if __name__ == '__main__':
    main()
