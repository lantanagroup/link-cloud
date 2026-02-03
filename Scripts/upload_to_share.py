"""
Azure File Share Upload Script

This script uploads the contents of a local directory to an Azure File Share.
It is designed to handle both files and directories, but it is restricted to
a depth of 1 (top-level directory and its immediate sub-directories).

Intent:
    - Facilitate easy uploading of local data to Azure File Shares.
    - Support interactive selection of destination shares.
    - Ensure necessary directory structures are created in the cloud.

Usage:
    python upload_to_share.py "<connection_string>" ["<share_name>"] ["<local_path>"]

Arguments:
    connection_string: The connection string for the Azure Storage Account.
    share_name:        (Optional) The name of the target file share. 
                       If omitted, the script will list available shares for selection.
    local_path:        (Optional) The local directory path to upload.
                       If omitted, the script will prompt for it.

Features:
    - Interactive Share Selection: If share_name is not provided, it fetches and lists all 
      available shares in the account.
    - Recursive Directory Creation: Automatically creates sub-directories in Azure to match
      the local structure (up to the allowed depth).
    - Depth-Limited: Processes files in the root and first-level sub-directories only.

Requirements:
    - azure-storage-file-share (pip install azure-storage-file-share)
"""

import argparse
import os
import sys

try:
    from azure.storage.fileshare import ShareServiceClient
except ImportError:
    print("Error: The 'azure-storage-file-share' package is required.")
    print("Please install it using: pip install azure-storage-file-share")
    sys.exit(1)

def create_directory_recursive(share_client, azure_dir_path):
    """
    Creates a directory and all its parent directories in the Azure File Share if they don't exist.
    """
    parts = azure_dir_path.split('/')
    current_path = ""
    for part in parts:
        if not part:
            continue
        current_path = f"{current_path}/{part}" if current_path else part
        dir_client = share_client.get_directory_client(current_path)
        try:
            dir_client.create_directory()
            print(f"  Created directory: {current_path}")
        except Exception as e:
            # If the directory already exists, we can ignore the error
            if 'ResourceAlreadyExists' not in str(e):
                raise

def upload_file(share_client, local_file_path, azure_file_path):
    """
    Uploads a single file to the Azure File Share.
    """
    file_client = share_client.get_file_client(azure_file_path)
    with open(local_file_path, "rb") as data:
        file_client.upload_file(data)

def select_share(service_client):
    """
    Lists available shares and prompts the user to select one.
    """
    print("Fetching available file shares...")
    try:
        shares = list(service_client.list_shares())
    except Exception as e:
        print(f"Error listing shares: {e}")
        sys.exit(1)

    if not shares:
        print("Error: No file shares found in this storage account.")
        sys.exit(1)

    print("\nAvailable file shares:")
    for i, share in enumerate(shares, 1):
        print(f"{i}. {share.name}")

    while True:
        try:
            choice = input(f"\nSelect a share by number (1-{len(shares)}): ")
            index = int(choice) - 1
            if 0 <= index < len(shares):
                return shares[index].name
            else:
                print(f"Please enter a number between 1 and {len(shares)}.")
        except ValueError:
            print("Invalid input. Please enter a number.")
        except (KeyboardInterrupt, EOFError):
            print("\nOperation cancelled.")
            sys.exit(0)

def upload_to_share(connection_string, share_name, local_path):
    """
    Uploads the contents of local_path (top-level and first-level sub-directories) to the specified Azure File Share.
    """
    try:
        service_client = ShareServiceClient.from_connection_string(connection_string)
        
        if not share_name:
            share_name = select_share(service_client)
            
        share_client = service_client.get_share_client(share_name)

        # Check if the share exists
        try:
            share_client.get_share_properties()
        except Exception as e:
            print(f"Error: File share '{share_name}' does not exist or is not accessible.")
            print(f"Details: {e}")
            sys.exit(1)

        # Prompt for local path if not provided
        if not local_path:
            local_path = input("\nEnter the local directory path to upload: ").strip()
            if not local_path:
                print("Error: Local path is required.")
                sys.exit(1)

        # Check if the local path exists and is a directory
        if not os.path.exists(local_path):
            print(f"Error: Local path '{local_path}' does not exist.")
            sys.exit(1)
        if not os.path.isdir(local_path):
             print(f"Error: Local path '{local_path}' is not a directory.")
             sys.exit(1)

        print(f"Starting upload from {local_path} to file share '{share_name}'...")

        for root, dirs, files in os.walk(local_path):
            # Calculate the relative path from the source directory
            relative_root = os.path.relpath(root, local_path)
            if relative_root == ".":
                relative_root = ""
                depth = 0
            else:
                depth = relative_root.count(os.path.sep) + 1
            
            # Only process top-level and first-level sub-directories
            if depth > 1:
                continue
            
            # Normalize path for Azure (always uses forward slashes)
            azure_dir_path = relative_root.replace(os.path.sep, '/')
            
            if azure_dir_path:
                create_directory_recursive(share_client, azure_dir_path)

            for file_name in files:
                if file_name.startswith("."):
                    continue
                    
                local_file_path = os.path.join(root, file_name)
                # Construct the full path for the file in Azure
                if azure_dir_path:
                    azure_file_path = f"{azure_dir_path}/{file_name}"
                else:
                    azure_file_path = file_name
                
                print(f"  Uploading: {azure_file_path}")
                upload_file(share_client, local_file_path, azure_file_path)

            # Prevent os.walk from descending into deeper sub-directories
            if depth >= 1:
                dirs[:] = []

        print("Upload completed successfully.")

    except Exception as e:
        print(f"Error during upload: {e}")
        sys.exit(1)

def main():
    parser = argparse.ArgumentParser(description="Upload contents of a directory to an Azure File Share.")
    parser.add_argument("connection_string", help="Azure Storage connection string (ABS connection string)")
    parser.add_argument("arg2", nargs='?', help="Name of the Azure File Share OR Local path")
    parser.add_argument("arg3", nargs='?', help="Local directory path to upload")

    args = parser.parse_args()

    share_name = None
    local_path = None

    if args.arg3 is not None:
        # 3 positional arguments: connection_string share_name path
        share_name = args.arg2
        local_path = args.arg3
    elif args.arg2 is not None:
        # 2 positional arguments: connection_string path
        share_name = None
        local_path = args.arg2
    else:
        # 1 positional argument: connection_string
        share_name = None
        local_path = None

    upload_to_share(args.connection_string, share_name, local_path)

if __name__ == "__main__":
    main()
