import sys
import re
import os
import xml.etree.ElementTree as ET

def validate_version(version):
    pattern = r'^\d+\.\d+\.\d+$'
    if not re.match(pattern, version):
        print(f"Error: Version '{version}' is not in the correct format (major.minor.patch)")
        sys.exit(1)

def update_csproj(file_path, version):
    if not os.path.exists(file_path):
        print(f"Warning: File not found: {file_path}")
        return

    # Use ET to parse and update, but ET can be tricky with formatting and comments.
    # For .csproj, simple regex might be safer to preserve formatting if we only need to change <Version>
    
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # Regex to find <Version>...</Version>
    # Also handle <VersionPrefix> if needed, but prompt says "version assembly and nuget version" 
    # which usually means <Version> in SDK-style projects.
    # We should also update <AssemblyVersion> and <FileVersion> if they exist, 
    # although <Version> usually covers them if not specified.
    # The prompt explicitly says "version assembly and nuget version".
    
    new_content = content
    version_pattern = r'(<Version>).*?(</Version>)'
    assembly_version_pattern = r'(<AssemblyVersion>).*?(</AssemblyVersion>)'
    file_version_pattern = r'(<FileVersion>).*?(</FileVersion>)'
    
    changed = False
    if re.search(version_pattern, new_content):
        new_content = re.sub(version_pattern, rf'\g<1>{version}\g<2>', new_content)
        changed = True
    else:
        # If <Version> doesn't exist, we add it to the first <PropertyGroup>
        property_group_pattern = r'(<PropertyGroup>)'
        if re.search(property_group_pattern, new_content):
             new_content = re.sub(property_group_pattern, rf'\g<1>\n    <Version>{version}</Version>', new_content, count=1)
             changed = True

    if re.search(assembly_version_pattern, new_content):
        new_content = re.sub(assembly_version_pattern, rf'\g<1>{version}\g<2>', new_content)
        changed = True
    
    if re.search(file_version_pattern, new_content):
        new_content = re.sub(file_version_pattern, rf'\g<1>{version}\g<2>', new_content)
        changed = True

    if changed:
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(new_content)
        print(f"Updated {file_path} to version {version}")
    else:
        print(f"No version tags found or added to {file_path}")

def update_pom(file_path, version):
    if not os.path.exists(file_path):
        print(f"Warning: File not found: {file_path}")
        return

    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    new_content = content
    # Update parent version if present
    new_content = re.sub(r'(<parent>[\s\S]*?<version>).*?(</version>)', rf'\g<1>{version}\g<2>', new_content, count=1)
    
    # Also update project version if it's there (some might have it explicitly)
    # Match <version> that is a direct child of <project>
    # We look for <version> after <artifactId> but NOT inside <parent> or <dependency>
    # Given the simplicity needed, we'll try to match <version> that is not preceded by <parent> or <dependency>
    
    # A simple way for these specific files is to look for the version tag that is NOT the parent one we just replaced.
    # Actually, let's just use a more robust regex for project version.
    project_version_pattern = r'(</artifactId>\s*<version>).*?(</version>)'
    
    # We want to avoid matching <version> inside <dependency> blocks.
    # In the provided pom files, the project version (if present) comes before <dependencies>.
    
    parts = re.split(r'(<dependencies>)', new_content, maxsplit=1)
    if len(parts) > 1:
        before_deps = parts[0]
        after_deps = parts[1:]
        
        if re.search(project_version_pattern, before_deps):
            before_deps = re.sub(project_version_pattern, rf'\g<1>{version}\g<2>', before_deps, count=1)
            new_content = before_deps + "".join(after_deps)
    else:
        # No dependencies block, just try to update
        new_content = re.sub(project_version_pattern, rf'\g<1>{version}\g<2>', new_content, count=1)

    if new_content != content:
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(new_content)
        print(f"Updated {file_path} to version {version}")

def main():
    if len(sys.argv) != 2:
        print("Usage: python set_version.py <version>")
        sys.exit(1)

    version = sys.argv[1]
    validate_version(version)

    dotnet_projects = [
        "Admin.BFF", "Account", "Audit", "Census", "DataAcquisition", 
        "Normalization", "QueryDispatch", "Report", "Submission", 
        "Tenant", "Terminology"
    ]
    java_projects = ["measureeval", "validation"]

    dotnet_root = "DotNet"
    for project in dotnet_projects:
        csproj_path = os.path.join(dotnet_root, project, f"{project}.csproj")
        update_csproj(csproj_path, version)

    java_root = "Java"
    for project in java_projects:
        pom_path = os.path.join(java_root, project, "pom.xml")
        update_pom(pom_path, version)

if __name__ == "__main__":
    main()
