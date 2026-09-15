"""
This script automates the process of building, pushing, and optionally deploying Docker images for Link services.

It performs the following steps for each selected service:
1. Builds the Docker image using the appropriate Dockerfile and context.
2. Pushes the built image to an Azure Container Registry (ACR).
3. (Optional) Updates a Kubernetes deployment in a specified namespace with the new image.
   - Before updating, it retrieves and prints the current image used by the deployment.

Usage:
    python Scripts/build_and_push_and_set.py --link-acr <ACR_URL> --tag <TAG> [options]

Arguments:
    --link-acr <ACR_URL>    The URL of the Azure Container Registry (e.g., nhsnlinkprem.azurecr.io).
                            Can also be set via the LINK_ACR environment variable.
    --tag <TAG>             The tag to apply to the Docker images (Required).
    --service <SERVICE>     Specific service to process. Can be specified multiple times.
    --all-services          Process all recognized services.
    --kube-namespace <NS>   The Kubernetes namespace where the deployments are located. 
                            If provided, the script will update the deployments with the new images.

If neither --service nor --all-services is specified, the script will interactively prompt the user for each service.

Recognized services:
    account, admin-bff, admin-ui, audit, census, dataacq, dataacq-worker,
    measureeval, mock-dmrp-api, normalization, querydispatch, report,
    submission, tenant, terminology, validation
"""

import argparse
import json
import os
import subprocess
import sys

# Define the service mapping based on docker-compose.yml and the requested ACR repo names
# Map service names (matching docker-compose.yml) to their ACR repo names and Dockerfile paths
SERVICES = {
    "account": {"repo": "link-account", "dockerfile": "DotNet/Account/Dockerfile", "context": ".", "deployment": "account-deploy", "container": "account"},
    "admin-bff": {"repo": "link-admin", "dockerfile": "DotNet/Admin.BFF/Dockerfile", "context": ".", "deployment": "admin-deploy", "container": "admin"},
    "admin-ui": {"repo": "link-admin-ui", "dockerfile": "Web/Admin.UI/Dockerfile", "context": "Web/Admin.UI", "deployment": "admin-ui-deploy", "container": "admin-ui"},
    "audit": {"repo": "link-audit", "dockerfile": "DotNet/Audit/Dockerfile", "context": ".", "deployment": "audit-deploy", "container": "audit"},
    "census": {"repo": "link-census", "dockerfile": "DotNet/Census/Dockerfile", "context": ".", "deployment": "census-deploy", "container": "census"},
    "dataacq": {"repo": "link-dataacquisition", "dockerfile": "DotNet/DataAcquisition/Dockerfile", "context": ".", "deployment": "data-acquisition-deploy", "container": "data"},
    "dataacq-worker": {"repo": "link-dataacquisition-worker", "dockerfile": "DotNet/DataAcquisition.AcquisitionWorker/Dockerfile", "context": ".", "deployment": "data-acquisition-worker-deploy", "container": "data-worker"},
    "measureeval": {"repo": "link-measureeval", "dockerfile": "Java/measureeval/Dockerfile", "context": "Java", "deployment": "measure-deploy", "container": "measure"},
    # Stand-in for the CDC DMRP API, deployed to the lower environments only. Selecting it
    # against a namespace that has no mock-dmrp-deploy fails the kubectl step, and because
    # that failure exits the script, it would stop any services queued behind it.
    "mock-dmrp-api": {"repo": "link-mock-dmrp", "dockerfile": "DotNet/MockDmrpApi/Dockerfile", "context": ".", "deployment": "mock-dmrp-deploy", "container": "mock-dmrp"},
    "normalization": {"repo": "link-normalization", "dockerfile": "DotNet/Normalization/Dockerfile", "context": ".", "deployment": "normalization-deploy", "container": "normalization"},
    "querydispatch": {"repo": "link-querydispatch", "dockerfile": "DotNet/QueryDispatch/Dockerfile", "context": ".", "deployment": "query-dispatch-deploy", "container": "query-dispatch"},
    "report": {"repo": "link-report", "dockerfile": "DotNet/Report/Dockerfile", "context": ".", "deployment": "report-deploy", "container": "report"},
    "submission": {"repo": "link-submission", "dockerfile": "DotNet/Submission/Dockerfile", "context": ".", "deployment": "submission-deploy", "container": "submission"},
    "tenant": {"repo": "link-tenant", "dockerfile": "DotNet/Tenant/Dockerfile", "context": ".", "deployment": "tenant-deploy", "container": "tenant"},
    "terminology": {"repo": "link-terminology", "dockerfile": "DotNet/Terminology/Dockerfile", "context": ".", "deployment": "terminology-deploy", "container": "terminology"},
    "validation": {"repo": "link-validation", "dockerfile": "Java/validation/Dockerfile", "context": "Java", "deployment": "validation-deploy", "container": "validation"}
}

def run_command(command):
    """Runs a shell command and prints its output."""
    print(f"Executing: {' '.join(command)}")
    # We use check=True to raise an exception on failure
    subprocess.run(command, check=True)

def main():
    parser = argparse.ArgumentParser(description="Build and push Link service Docker images to an Azure Container Registry.")
    
    # ACR parameter
    parser.add_argument("--link-acr", help="The ACR URL (e.g. nhsnlinkprem.azurecr.io). Can also be set via LINK_ACR environment variable.")
    
    # Kubernetes parameter
    parser.add_argument("--kube-namespace", help="The Kubernetes namespace to update images in.")
    
    # Service selection
    parser.add_argument("--service", action="append", help="Specific service to build and push. Can be specified multiple times.")
    parser.add_argument("--all-services", action="store_true", help="Build and push all services defined in the mapping.")
    
    # Tag parameter
    parser.add_argument("--tag", required=True, help="The tag to apply to the Docker images.")
    
    args = parser.parse_args()

    # 1. Determine ACR
    acr = args.link_acr or os.environ.get("LINK_ACR")
    if not acr:
        print("Error: ACR must be specified via --link-acr or the LINK_ACR environment variable.", file=sys.stderr)
        sys.exit(1)

    # 2. Determine services to process
    selected_services = []
    if args.all_services:
        selected_services = list(SERVICES.keys())
    elif args.service:
        for s in args.service:
            if s in SERVICES:
                selected_services.append(s)
            else:
                print(f"Error: Service '{s}' is not recognized. Recognized services: {', '.join(sorted(SERVICES.keys()))}", file=sys.stderr)
                sys.exit(1)
    else:
        # Prompt user for each service
        print("No services specified. Please indicate which services to build and push:")
        for service_name in sorted(SERVICES.keys()):
            try:
                response = input(f"Build and push {service_name}? (y/N): ").lower()
                if response == 'y':
                    selected_services.append(service_name)
            except EOFError:
                break

    if not selected_services:
        print("No services selected. Exiting.")
        sys.exit(0)

    # 3. Build and push each service
    for service_name in selected_services:
        info = SERVICES[service_name]
        repo = info["repo"]
        dockerfile = info["dockerfile"]
        context = info["context"]
        
        # Use backslashes in Dockerfile path if on Windows to match the issue description template
        # although forward slashes generally work fine with docker CLI.
        if os.name == 'nt':
            dockerfile = dockerfile.replace('/', '\\')
            context = context.replace('/', '\\')

        image_tag = f"{acr}/{repo}:{args.tag}"
        
        print(f"\n{'='*60}")
        print(f"Processing service: {service_name}")
        print(f"Image: {image_tag}")
        print(f"{'='*60}")
        
        # Build command
        build_cmd = ["docker", "build", "--tag", image_tag, "-f", dockerfile, context]
        
        try:
            print(f"Building {service_name}...")
            run_command(build_cmd)
            
            print(f"Pushing {service_name}...")
            push_cmd = ["docker", "push", image_tag]
            run_command(push_cmd)
            
            if args.kube_namespace:
                deployment = info["deployment"]
                container = info["container"]
                
                print(f"Updating Kubernetes deployment for {service_name}...")
                
                # Try to output the current image before updating it
                try:
                    get_img_cmd = [
                        "kubectl", "-n", args.kube_namespace, 
                        "get", f"deployment/{deployment}", 
                        "-o", "json"
                    ]
                    # We use subprocess.run directly here because we need to capture stdout
                    result = subprocess.run(get_img_cmd, capture_output=True, text=True)
                    if result.returncode == 0:
                        dep_data = json.loads(result.stdout)
                        containers = dep_data.get("spec", {}).get("template", {}).get("spec", {}).get("containers", [])
                        current_image = next((c.get("image") for c in containers if c.get("name") == container), "Unknown")
                        print(f"Current image: {current_image}")
                    else:
                        print(f"Note: Could not retrieve current image (deployment/container might not exist yet).")
                except Exception as e:
                    print(f"Note: Could not retrieve current image: {e}")

                # Example command: kubectl -n my-namespace set image deployment/my-deployment my-container=repo/my-app:1.2.3
                kube_cmd = [
                    "kubectl", "-n", args.kube_namespace, 
                    "set", "image", f"deployment/{deployment}", 
                    f"{container}={image_tag}"
                ]
                run_command(kube_cmd)
            
        except subprocess.CalledProcessError as e:
            print(f"Error: Command failed for {service_name} with exit code {e.returncode}", file=sys.stderr)
            sys.exit(1)
        except Exception as e:
            print(f"An unexpected error occurred: {e}", file=sys.stderr)
            sys.exit(1)

    print("\nSuccessfully built and pushed all selected services.")

if __name__ == "__main__":
    main()
