#!/usr/bin/env python3
import glob
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET


def get_changed_lines(base_ref):
    print(f"Finding changed lines compared to origin/{base_ref}...")
    # Fetch the base ref to ensure it's available for diff
    subprocess.run(["git", "fetch", "origin", base_ref], capture_output=True)
    
    cmd = ["git", "diff", "-U0", f"origin/{base_ref}...HEAD"]
    result = subprocess.run(cmd, capture_output=True, text=True)
    if result.returncode != 0:
        print(f"Error running git diff: {result.stderr}")
        return {}

    changed_lines = {}
    current_file = None
    for line in result.stdout.splitlines():
        if line.startswith("+++ b/"):
            current_file = line[6:]
            changed_lines[current_file] = set()
        elif line.startswith("@@"):
            match = re.search(r"\+(\d+)(?:,(\d+))?", line)
            if match and current_file:
                start = int(match.group(1))
                count = int(match.group(2)) if match.group(2) else 1
                if count == 0:
                    continue
                for i in range(start, start + count):
                    changed_lines[current_file].add(i)
    return changed_lines

def parse_cobertura(report_path, changed_files):
    covered = 0
    total = 0
    
    if not os.path.exists(report_path):
        return 0, 0

    print(f"Parsing Cobertura report: {report_path}")
    try:
        tree = ET.parse(report_path)
        root = tree.getroot()
    except Exception as e:
        print(f"  Error parsing {report_path}: {e}")
        return 0, 0

    for cls in root.findall(".//class"):
        filename = cls.get("filename")
        if not filename: continue
        
        # Normalize filename
        filename = filename.replace("\\", "/")
        
        # Cobertura filename might be relative to some base or absolute
        # Try to find which changed file it matches
        matched_cf = None
        if filename in changed_files:
            matched_cf = filename
        else:
            # Try suffix match
            for cf in changed_files:
                if cf.endswith(filename):
                    matched_cf = cf
                    break
        
        if matched_cf:
            file_changed_lines = changed_files[matched_cf]
            print(f"  Tracing coverage for: {matched_cf}")
            
            # Lines in Cobertura can be under <lines> or directly under <class> or <method>
            lines_nodes = cls.findall(".//line")
            found_lines = set()
            for line_node in lines_nodes:
                line_num = int(line_node.get("number"))
                if line_num in file_changed_lines and line_num not in found_lines:
                    found_lines.add(line_num)
                    total += 1
                    hits = int(line_node.get("hits"))
                    is_covered = hits > 0
                    if is_covered:
                        covered += 1
                    
                    status = "COVERED" if is_covered else "NOT COVERED"
                    # Try to find method name
                    method_name = "unknown"
                    for method in cls.findall(".//method"):
                        if any(l.get("number") == str(line_num) for l in method.findall(".//line")):
                            method_name = method.get("name")
                            # Handle .NET async state machine methods
                            if method_name == "MoveNext":
                                class_name = cls.get("name", "")
                                match = re.search(r"<([^>]+)>", class_name)
                                if match:
                                    method_name = match.group(1)
                            break
                    
                    print(f"    [DOTNET] Line {line_num} in {method_name}() - {status}")

    return covered, total

def parse_jacoco(report_path, changed_files):
    covered = 0
    total = 0

    if not os.path.exists(report_path):
        return 0, 0

    print(f"Parsing Jacoco report: {report_path}")
    try:
        tree = ET.parse(report_path)
        root = tree.getroot()
    except Exception as e:
        print(f"  Error parsing {report_path}: {e}")
        return 0, 0

    for package in root.findall(".//package"):
        package_path = package.get("name")
        for sourcefile in package.findall("sourcefile"):
            source_name = sourcefile.get("name")
            
            matching_changed_file = None
            # Java paths: Java/<module>/src/main/java/<package>/<sourcename>
            # Search for a changed file that ends with package_path/source_name
            suffix = f"{package_path}/{source_name}"
            for cf in changed_files:
                if cf.replace("\\", "/").endswith(suffix):
                    matching_changed_file = cf
                    break
            
            if matching_changed_file:
                file_changed_lines = changed_files[matching_changed_file]
                print(f"  Tracing coverage for: {matching_changed_file}")
                
                for line_node in sourcefile.findall("line"):
                    line_num = int(line_node.get("nr"))
                    if line_num in file_changed_lines:
                        total += 1
                        ci = int(line_node.get("ci"))
                        is_covered = ci > 0
                        if is_covered:
                            covered += 1
                        
                        status = "COVERED" if is_covered else "NOT COVERED"
                        
                        # Try to find method name
                        method_name = "unknown"
                        for cls in package.findall(f"class[@sourcefilename='{source_name}']"):
                            for method in cls.findall("method"):
                                # Jacoco methods have a 'line' attribute which is the first line of the method
                                if method.get("line") == str(line_num):
                                    method_name = method.get("name")
                                    break
                            if method_name != "unknown": break

                        print(f"    [JAVA] Line {line_num} in {method_name}() - {status}")

    return covered, total

def main():
    base_ref = os.environ.get("GITHUB_BASE_REF", "dev")
    pr_number = os.environ.get("PR_NUMBER")
    
    changed_lines = get_changed_lines(base_ref)
    if not changed_lines:
        print("No changed lines detected in this PR.")
        sys.exit(0)

    dotnet_covered = 0
    dotnet_total = 0
    cobertura_reports = glob.glob("**/coverage.cobertura.xml", recursive=True)
    for report in cobertura_reports:
        c, t = parse_cobertura(report, changed_lines)
        dotnet_covered += c
        dotnet_total += t

    java_covered = 0
    java_total = 0
    jacoco_reports = glob.glob("**/jacoco.xml", recursive=True)
    for report in jacoco_reports:
        c, t = parse_jacoco(report, changed_lines)
        java_covered += c
        java_total += t

    dotnet_pct = (dotnet_covered / dotnet_total * 100) if dotnet_total > 0 else None
    java_pct = (java_covered / java_total * 100) if java_total > 0 else None

    percentages = []
    if dotnet_pct is not None:
        print(f"Overall DotNet Coverage on Changed Code: {dotnet_pct:.2f}% ({dotnet_covered}/{dotnet_total})")
        percentages.append(dotnet_pct)
    if java_pct is not None:
        print(f"Overall Java Coverage on Changed Code: {java_pct:.2f}% ({java_covered}/{java_total})")
        percentages.append(java_pct)

    if not percentages:
        print("No coverage data found for changed lines.")
        final_pct = 0
    else:
        # Summation (average) of language percentages as requested
        final_pct = sum(percentages) / len(percentages)
    
    print(f"Final Combined Coverage: {final_pct:.2f}%")

    if pr_number:
        update_pr_description(pr_number, final_pct)
    else:
        print("PR_NUMBER not set, skipping description update.")

def update_pr_description(pr_number, coverage_pct):
    coverage_str = f"{coverage_pct:.1f}%"
    print(f"Updating PR #{pr_number} description with coverage: {coverage_str}")
    
    try:
        # Get current body
        cmd = ["gh", "pr", "view", pr_number, "--json", "body", "-q", ".body"]
        result = subprocess.run(cmd, capture_output=True, text=True)
        if result.returncode != 0:
            print(f"Error getting PR description: {result.stderr}")
            return

        body = result.stdout
        # Match "- Coverage: " followed by anything until end of line
        pattern = r"(- Coverage:).*$"
        replacement = f"\\1 {coverage_str}"
        
        if re.search(pattern, body, re.MULTILINE):
            new_body = re.sub(pattern, replacement, body, flags=re.MULTILINE)
        else:
            print("'- Coverage:' not found in PR description. Adding it under Unit Testing section.")
            if "### 🧑‍🔬 Unit Testing" in body:
                new_body = body.replace("### 🧑‍🔬 Unit Testing", f"### 🧑‍🔬 Unit Testing\n\n- Coverage: {coverage_str}")
            else:
                new_body = body + f"\n\n### 🧑‍🔬 Unit Testing\n- Coverage: {coverage_str}"

        if new_body.strip() == body.strip():
            print("No changes needed to PR description.")
            return

        with open("new_body.md", "w", encoding="utf-8") as f:
            f.write(new_body)
        
        cmd = ["gh", "pr", "edit", pr_number, "--body-file", "new_body.md"]
        result = subprocess.run(cmd, capture_output=True, text=True)
        if result.returncode != 0:
            print(f"Error updating PR description: {result.stderr}")
        else:
            print("PR description updated successfully.")
    except Exception as e:
        print(f"Failed to update PR description: {e}")

if __name__ == "__main__":
    main()
